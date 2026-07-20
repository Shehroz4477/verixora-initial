# Verixora Postman suite

Import the collection and **Verixora Local** environment, then run folders in numeric order. Set a unique E.164 `phoneNumber` and `duplicatePhoneNumber` (for example, `+923001234567`), a strong test `password`, `deviceId`, and `deviceFingerprint` before registration. The two phone numbers must be SMS-capable numbers you control; the duplicate-device regression intentionally uses the same `deviceId` for the second account attempt.

OTP codes are deliberately never returned by API responses. For local development only, copy each code from the API console into its matching collection variable; use real SMS/email delivery in every non-development environment. The suite begins with unauthenticated liveness and readiness checks: liveness proves the API process is running, while readiness proves the configured database and Redis are reachable. It also includes success, device-binding, invalid-OTP, authorization, email/web-login, pending-controller, lock-registration, and audit-trail regression cases for the endpoints implemented so far. A pending controller is deliberately rejected for door registration; it has no path to an unlock command until secure provisioning is completed.

The provisioning request is deliberately development-only when using the `local-development-only` attestation marker. Production must configure a manufacturer/secure-element attestation verifier and MQTT mutual-TLS certificates; do not enable the development switch outside a local machine.

The local API profile listens on `http://localhost:5166`; copy [the local API example configuration](../api-host/ApiHost/appsettings.Local.example.json) to `appsettings.Local.json`, set the local secrets, then run the API.
