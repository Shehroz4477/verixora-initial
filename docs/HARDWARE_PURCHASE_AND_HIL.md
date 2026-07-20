# Verixora hardware purchase and hardware-in-loop acceptance

This document is the purchasing and on-site acceptance gate for one exterior door. It is intentionally conservative: a network, API, mobile app, or controller failure must never remove the legally required physical exit path.

> **Safety boundary:** Have a qualified low-voltage/access-control installer select and wire the lock, power supply, fire-alarm interface, emergency egress, and key override for the building and local authority requirements. The ESP32 is not a life-safety controller. Do not connect a lock directly to an ESP32 GPIO pin.

## Purchase list per door

| Item | Quantity | Production requirement | Why it is needed |
|---|---:|---|---|
| ESP32 controller | 1 active + 1 spare | An ESP32 DevKit V1 may be used for bench work only. For a customer installation, use a controlled production board with a known chip revision, secure boot/flash-encryption support, disabled debug access, and a unique identity. | Runs the Verixora door-controller firmware. |
| Hardware root of trust | 1 | ATECC608B-class secure element, integrated on the production board or mounted on a protected I2C design. Each unit must have its own key/certificate material. | Keeps the controller acknowledgement and MQTT private keys out of normal flash. |
| Lock hardware | 1 | A correctly rated, listed electric strike or maglock selected for the door, frame, exit route, and building rules. | The physical locking actuator. The installer chooses fail-safe/fail-secure behaviour; this is not a software setting. |
| Lock driver | 1 | Isolated, appropriately rated relay/MOSFET driver, flyback protection where applicable, and a fuse sized by the installer. | Keeps inductive lock current and electrical noise away from the controller. |
| Power | 1 | Dedicated listed 12 V DC access-control PSU with battery backup/UPS where required, a separately fused lock branch, and a regulated 5 V/3.3 V buck converter for electronics. | Prevents a lock pulse, brownout, or outage from damaging the controller. |
| Door-state sensor | 1 | Wired magnetic reed/contact switch, tamper-resistant mounting. | Distinguishes an unlock acknowledgement from an actually opened/closed door. It is monitoring evidence, not an unlock factor. |
| Enclosure tamper switch | 1 | Normally-closed wired switch inside a locked enclosure. | Causes remote unlock rejection when the controller enclosure is opened. |
| Emergency exit (REX) | 1 | Dedicated, hard-wired, normally-closed request-to-exit device wired to the lock/power circuit, not through an ESP32. | Allows exit even when firmware, Wi-Fi, broker, API, or controller power management fails. |
| Mechanical override | 1 | A physical key cylinder/lever or other code-compliant mechanical method. | Gives a controlled recovery path if the service is unavailable. Keep key custody outside the application. |
| Fire/life-safety integration | As required | Approved interface to the site fire/alarm system where required by the installer and authority. | Ensures the lock behaves correctly in emergency conditions. |
| Network | 1 | Dedicated IoT SSID/VLAN, WPA2/WPA3 enterprise or a unique long random PSK, no Internet-exposed MQTT broker, and reliable signal at the door. | Isolates the controller and avoids an unauthenticated local network becoming an access path. |
| Installation materials | As required | Fused terminals, strain relief, suitable cable/conduit, labelled wiring, enclosure, and a USB data cable for bench provisioning only. | Supports a repairable, auditable installation. |

Optional but valuable: a supervised door-position sensor, power-failure input, locally stored event buffer, and a sealed enclosure. A PIR/mmWave motion sensor can help operational analytics but must **never** grant access by itself. If relay-attack resistance above BLE proximity is required, add an NFC physical tap or UWB policy after a separate security design review.

## Controller selection decision

The current firmware builds for `esp32dev` so development can start with an ESP32 DevKit V1. It is not by itself a production hardware root of trust: development boards have variable provenance, exposed programming pads, and no per-unit factory attestation chain integrated with the backend.

For production, select a known ESP32 revision that supports Secure Boot v2 and Flash Encryption, then make irreversible eFuse decisions only after the complete signed image and recovery procedure have passed on sacrificial units. Espressif recommends enabling both controls for production and protecting the signing key; its Secure Boot v2 documentation notes the ESP32 revision requirement. The ATECC608B family provides protected P-256 key storage and signing/TLS capabilities suitable for an additional hardware trust boundary. Do not reuse controller private keys, MQTT certificates, provisioning tokens, or signed firmware images between units.

Reference material:

- [Espressif: Security feature enablement workflows](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/security/security-features-enablement-workflows.html)
- [Espressif: Secure Boot v2 for ESP32](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/security/secure-boot-v2.html)
- [Microchip: ATECC608B summary data sheet](https://ww1.microchip.com/downloads/en/DeviceDoc/ATECC608B-CryptoAuthentication-Device-Summary-Data-Sheet-DS40002239A.pdf)

## Required wiring separation

The logical access path is deliberately separate from the life-safety path:

```text
Mobile app -> API -> MQTT over mTLS -> ESP32 -> isolated driver -> electric lock
                                      |              |
                                      |              +-- door/tamper sensing (inputs only)
                                      +-- audit acknowledgement

Hard-wired REX / fire interface / mechanical override -> lock power circuit
                                                    (does not pass through the ESP32)
```

The current firmware treats the configured tamper input as active-low and fails closed for remote unlocks. Confirm relay polarity, lock power behaviour, door-contact polarity, and the REX bypass on a de-energized bench before connecting the real door.

## Hardware-in-loop (HIL) acceptance record

Run every item with the exact production configuration: the signed firmware build, Android release candidate, API image, database provider, MQTT certificates, controller serial/device ID, and physical lock. Capture the audit-log ID or evidence for each result. A single failed safety or negative test blocks deployment.

| ID | Test | Expected result |
|---|---|---|
| HIL-01 | Boot an unprovisioned controller. | Relay remains inactive; it cannot connect to MQTT or accept a command. |
| HIL-02 | Attempt provisioning with an expired, altered, or previously consumed token. | Backend and controller reject it; device remains unavailable for a lock. |
| HIL-03 | Provision a unique controller with its public key/certificate. | Backend shows only that controller as active; a second attempt cannot claim the same hardware ID. |
| HIL-04 | Send a legitimate mobile unlock with valid OTP session, trusted device, face/liveness result, and BLE proof while standing at the door. | One pulse only, signed controller acknowledgement, door event, and audit entry are recorded. |
| HIL-05 | Repeat the same command ID/signature, including after a controller reboot. | No additional pulse; replay is rejected locally and/or by the API. |
| HIL-06 | Try a different Android phone, a different account, an invalid/expired BLE proof, a tampered signature, and an expired command. | Each is rejected without operating the relay. |
| HIL-07 | Open the enclosure/tamper circuit during a request. | Remote unlock is rejected and an actionable event is raised. |
| HIL-08 | Disconnect Wi-Fi/MQTT/API, then restore it. | No remote unlock while disconnected; after recovery, controller reconnects with its own certificate and no stale command unlocks the door. |
| HIL-09 | Cut/recover controller power during an access request. | Lock behaviour matches the installer-approved safety design; no duplicate command opens the door. |
| HIL-10 | Trigger REX, use mechanical override, and perform the fire/alarm procedure. | The physical exit/recovery path works with the ESP32 unplugged. |
| HIL-11 | Present printed photo, replayed video, and multiple faces to the face flow. | Production liveness/PAD provider rejects them. The current local face worker alone is not sufficient for this test. |
| HIL-12 | Review portal/system-admin logs. | Requester, home, controller, result, timestamps, and rejection reason are traceable without storing raw face images. |

## Deployment stop conditions

Do not install a customer door until all of the following are true:

- The Android release is signed with the organisation-owned keystore and talks only to HTTPS production endpoints.
- The API runs with managed secrets, a persistent encrypted data-protection key ring, TLS, restricted database roles, Redis authentication, and per-controller MQTT mTLS ACLs.
- The production attestation verifier is enabled; `local-development-only` attestation is disabled.
- The face verification provider returns a real liveness/PAD decision; the development passive-verification switch is disabled.
- Secure Boot/Flash Encryption, private-key handling, and the approved firmware update/revocation process have passed on spare controllers before the first customer controller is fused.
- All HIL acceptance tests above are recorded as passed by the installer and responsible product owner.

Until these gates pass, use the system only as a local development/bench environment, never as the sole access control for an occupied exterior door.
