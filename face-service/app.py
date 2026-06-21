import io
import json
import os
from fastapi import FastAPI, File, UploadFile, Form
from PIL import Image
import numpy as np

app = FastAPI(title="Verixora Face Service")

# Store embeddings in memory for demo (use DB in production)
embeddings_db = {}  # user_id -> list of face encodings

@app.post("/enroll")
async def enroll(
    userId: str = Form(...),
    images: list[UploadFile] = File(...)
):
    if len(images) < 3:
        return {"status": "error", "message": "At least 3 images required"}

    encodings = []
    for img_file in images:
        img = Image.open(io.BytesIO(await img_file.read()))
        img = np.array(img)
        # The face_recognition library will extract encodings
        import face_recognition
        face_locations = face_recognition.face_locations(img)
        if not face_locations:
            continue
        face_enc = face_recognition.face_encodings(img, face_locations)[0]
        encodings.append(face_enc.tolist())

    if len(encodings) < 1:
        return {"status": "error", "message": "No face detected in any image"}

    embeddings_db[userId] = encodings
    return {"status": "enrolled", "face_count": len(encodings)}


@app.post("/verify")
async def verify(
    userId: str = Form(...),
    image: UploadFile = File(...)
):
    if userId not in embeddings_db:
        return {"match": False, "confidence": 0.0, "error": "User not enrolled"}

    img = Image.open(io.BytesIO(await image.read()))
    img = np.array(img)

    import face_recognition
    face_locations = face_recognition.face_locations(img)
    if not face_locations:
        return {"match": False, "confidence": 0.0, "error": "No face detected"}

    face_enc = face_recognition.face_encodings(img, face_locations)[0]
    known_encs = [np.array(e) for e in embeddings_db[userId]]

    distances = face_recognition.face_distance(known_encs, face_enc)
    min_distance = float(np.min(distances))
    confidence = 1.0 - min_distance
    match = min_distance < 0.6  # standard threshold

    return {"match": match, "confidence": round(confidence, 4)}
