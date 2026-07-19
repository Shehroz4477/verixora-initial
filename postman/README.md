# Verixora Postman suite

Import the collection and **Verixora Local** environment, then run folders in numeric order. Set a unique `phoneNumber`, a strong test `password`, `deviceId`, and `deviceFingerprint` before registration.

OTP codes are deliberately never returned by API responses. For local development only, copy each code from the API console into its matching collection variable; use real SMS/email delivery in every non-development environment. The suite includes success, device-binding, invalid-OTP, authorization, and email/web-login regression cases for the endpoints implemented so far.

The local API profile listens on `http://localhost:5166`; copy [the local API example configuration](../api-host/ApiHost/appsettings.Local.example.json) to `appsettings.Local.json`, set the local secrets, then run the API.
