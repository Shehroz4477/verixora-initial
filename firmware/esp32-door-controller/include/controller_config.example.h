#pragma once

// Do not place production credentials in source control. The installer writes these values to
// NVS after a physically-local, one-time provisioning session. Production deployment must enable
// ESP-IDF Flash Encryption so these at-rest values are not readable from flash.
constexpr int kLockRelayPin = 26;
constexpr int kDoorContactPin = 27;
constexpr int kTamperPin = 33;
constexpr int kRequestToExitPin = 32;
constexpr uint32_t kUnlockPulseMilliseconds = 1200;
constexpr uint32_t kCommandMaxAgeSeconds = 30;

// The physical REX button and mechanical/key override must bypass this MCU electrically.
// No firmware setting may remove either life-safety path.
