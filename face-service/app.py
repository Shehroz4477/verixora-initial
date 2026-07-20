import io
import json
from typing import Annotated

import face_recognition
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from PIL import Image, UnidentifiedImageError

app = FastAPI(title="Verixora Face Service", version="1.0.0")

MINIMUM_ENROLLMENT_IMAGES = 3
MAXIMUM_ENROLLMENT_IMAGES = 5
MAXIMUM_IMAGE_BYTES = 5 * 1024 * 1024
EMBEDDING_DIMENSIONS = 128
MATCH_THRESHOLD = 0.48


async def extract_single_embedding(image_file: UploadFile) -> list[float]:
    if image_file.content_type not in {"image/jpeg", "image/png"}:
        raise HTTPException(status_code=400, detail="Face images must be JPEG or PNG")

    payload = await image_file.read()
    if not payload or len(payload) > MAXIMUM_IMAGE_BYTES:
        raise HTTPException(status_code=400, detail="Face image must be between 1 byte and 5 MB")

    try:
        image = Image.open(io.BytesIO(payload)).convert("RGB")
        pixels = np.array(image)
    except (UnidentifiedImageError, OSError, ValueError) as exc:
        raise HTTPException(status_code=400, detail="Face image cannot be decoded") from exc

    locations = face_recognition.face_locations(pixels)
    if len(locations) != 1:
        raise HTTPException(status_code=400, detail="Each image must contain exactly one face")

    encodings = face_recognition.face_encodings(pixels, known_face_locations=locations)
    if len(encodings) != 1 or len(encodings[0]) != EMBEDDING_DIMENSIONS:
        raise HTTPException(status_code=400, detail="A face embedding could not be extracted")

    embedding = encodings[0]
    if not np.isfinite(embedding).all():
        raise HTTPException(status_code=400, detail="Face embedding is invalid")
    return embedding.astype(float).tolist()


@app.post("/extract")
async def extract(images: Annotated[list[UploadFile], File(...)]):
    """Extract templates only; never persist an image or embedding in this service."""
    if not MINIMUM_ENROLLMENT_IMAGES <= len(images) <= MAXIMUM_ENROLLMENT_IMAGES:
        raise HTTPException(status_code=400, detail="Provide three to five images")

    embeddings = [await extract_single_embedding(image) for image in images]
    return {"embeddings": embeddings, "face_count": len(embeddings)}


@app.post("/verify")
async def verify(
    image: Annotated[UploadFile, File(...)],
    reference_embeddings_json: Annotated[str, Form(alias="referenceEmbeddingsJson")],
):
    """Compare a live capture with API-supplied templates; retain no biometric data."""
    try:
        supplied_embeddings = json.loads(reference_embeddings_json)
        known_embeddings = np.asarray(supplied_embeddings, dtype=np.float64)
    except (TypeError, ValueError, json.JSONDecodeError) as exc:
        raise HTTPException(status_code=400, detail="Reference templates are invalid") from exc

    if (
        known_embeddings.ndim != 2
        or not MINIMUM_ENROLLMENT_IMAGES <= len(known_embeddings) <= MAXIMUM_ENROLLMENT_IMAGES
        or known_embeddings.shape[1] != EMBEDDING_DIMENSIONS
        or not np.isfinite(known_embeddings).all()
    ):
        raise HTTPException(status_code=400, detail="Reference templates are invalid")

    candidate = np.asarray(await extract_single_embedding(image), dtype=np.float64)
    distances = face_recognition.face_distance(known_embeddings, candidate)
    minimum_distance = float(np.min(distances))
    confidence = max(0.0, min(1.0, 1.0 - minimum_distance))

    return {
        "match": minimum_distance <= MATCH_THRESHOLD,
        "confidence": round(confidence, 4),
        # This self-hosted recognizer intentionally does not claim passive
        # comparison is liveness/PAD. Production unlock stays fail-closed until
        # a dedicated liveness verifier is connected.
        "livenessPassed": False,
    }
