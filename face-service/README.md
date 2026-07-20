# Verixora Face Recognition Service

This is a stateless, private-network recognition worker. It accepts an image only
for the duration of a request and never stores images, user IDs, or embeddings.
The .NET API encrypts each embedding with AES-256-GCM and stores it through the
`identity` database routines.

## Local stack

```powershell
docker compose -f infrastructure/docker-compose.yml up -d --build face-service
```

The API uses `FaceService:BaseUrl=http://localhost:8000` in local development.
Create `api-host/ApiHost/appsettings.Local.json` from the example or set .NET
user secrets. `FaceBiometrics:EncryptionKeyBase64` must be a Base64-encoded
32-byte key. It is deliberately not in source control.

## Security boundary

- `/extract` returns 128-dimensional embeddings for three to five images with
  exactly one face each.
- `/verify` receives decrypted templates transiently from the API and returns
  a match result. The templates are not retained by this service.
- Image size, MIME type, decode errors, multiple faces, invalid dimensions, and
  non-finite values are rejected.
- This worker is **not** a liveness / presentation-attack-detection system.
  It explicitly returns `livenessPassed: false`. Production API requests remain
  fail-closed unless a dedicated liveness verifier supplies a successful proof.
  `FaceBiometrics:AllowPassiveDevelopmentVerification` is a local-development
  flag only and must never be enabled in staging or production.

## Production requirements

Run this service in a private subnet behind mTLS; do not expose it to the public
internet. Integrate an evaluated mobile liveness/PAD provider before enabling
biometric unlock in production, rotate the API encryption key through a managed
secret store, and retain only consent/audit metadata according to the applicable
privacy law. A face match is an additional access factor, not a replacement for
the ESP32 presence proof, controller signature, or mechanical emergency exit.
