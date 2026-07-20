# Verixora ESP32 exterior-door controller

This firmware is deliberately fail-closed. It accepts an unlock only when all of these are true:

- encrypted NVS contains a one-time-provisioned controller identity and mTLS credentials;
- time is synchronized and the server command has not expired;
- the MQTT topic is reached over mTLS with a per-controller broker ACL;
- the command id has not already been handled;
- the tamper input is normal; and
- verified phone-presence proof is supplied by the companion Android app.

For every short-lived unlock command, the controller advertises a BLE GATT service only while the command is pending. The Android app reads a random controller challenge, signs the canonical payload with its non-exportable Android Keystore P-256 key, and writes the DER signature back. The controller verifies it against the public key delivered with the authenticated command, then and only then pulses the relay. A MAC address, RSSI, motion sensor, or static fingerprint is never accepted as proof.

## Required production hardware

- ESP32 DevKit V1 only for development; production should add an ATECC608B (or equivalent) secure element for the acknowledgement and mTLS private keys.
- A listed 12 V electric strike/maglock controller, isolated relay or MOSFET driver, fused power supply, door-contact reed switch, tamper switch, and suitable enclosure.
- A dedicated wired request-to-exit (REX) device and a mechanical keyed override. Both must bypass the MCU electrically so they still operate if firmware, network, backend, or power management fails. Follow local fire and building codes.

## Manufacturing and provisioning gates

1. Enable ESP32 secure boot v2 and flash encryption in release mode before customer deployment.
2. Generate a unique P-256 acknowledgement key and unique MQTT client certificate for each controller; never duplicate an image with shared credentials.
3. Put only the public P-256 SPKI in the backend during the one-time pairing session. The backend stores the pairing token hash, never the token itself.
4. Configure a broker ACL so a controller subscribes only to `verixora/{deviceId}/commands` and publishes only its own acknowledgement channel/gateway.
5. Do not expose the local development MQTT configuration or the `local-development-only` attestation setting outside a development environment.

The backend accepts both P-256 DER signatures emitted by mbedTLS and 64-byte IEEE-P1363 signatures. The canonical acknowledgement payload is:

`Verixora.ControllerAck.v1|{deviceId:N}|{commandId:N}|{Unlocked|Failed}|{occurredAtUtc:O}|{nonce}`

The Android BLE presence payload is:

`Verixora.BlePresence.v1|{controllerDeviceId:N}|{commandId:N}|{randomChallenge}|{expiresAtUnixTimeSeconds}`

BLE proves possession of the trusted device key in the controller's radio range. It is not cryptographic distance-bounding; deployment sites with high relay-attack risk should add a UWB/NFC physical-tap policy or a supervised local reader.

Do not deploy this firmware until the BLE phone-presence proof and the physical installation have been independently tested on the selected lock hardware.
