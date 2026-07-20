#include <Arduino.h>
#include <ArduinoJson.h>
#include <BLEDevice.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <esp_system.h>
#include <time.h>
#include <mbedtls/base64.h>
#include <mbedtls/ctr_drbg.h>
#include <mbedtls/entropy.h>
#include <mbedtls/pk.h>
#include <mbedtls/sha256.h>

#include "controller_config.example.h"

namespace {
constexpr char kConfigNamespace[] = "verixora";
constexpr char kLastCommandKey[] = "last-command";
constexpr char kPresenceServiceUuid[] = "6f9c0001-4f46-4a39-9b47-71a4cf173601";
constexpr char kPresenceChallengeCharacteristicUuid[] = "6f9c0002-4f46-4a39-9b47-71a4cf173601";
constexpr char kPresenceProofCharacteristicUuid[] = "6f9c0003-4f46-4a39-9b47-71a4cf173601";

Preferences preferences;
WiFiClientSecure mqttTlsClient;
PubSubClient mqtt(mqttTlsClient);

struct ControllerConfig {
  String deviceId;
  String lockId;
  String mqttHost;
  uint16_t mqttPort = 8883;
  String mqttTopic;
  String acknowledgementUrl;
  String wifiSsid;
  String wifiPassword;
  String caCertificatePem;
  String mqttClientCertificatePem;
  String mqttClientPrivateKeyPem;
  String acknowledgementPrivateKeyPem;
  bool requireVerifiedPhonePresence = true;
};

ControllerConfig config;
BLEServer *bleServer = nullptr;
BLECharacteristic *presenceChallengeCharacteristic = nullptr;
BLECharacteristic *presenceProofCharacteristic = nullptr;

struct PendingUnlock {
  bool active = false;
  bool presenceValidated = false;
  String commandId;
  String trustedMobileDeviceId;
  String trustedMobilePublicKeySpkiBase64;
  String challenge;
  int64_t expiresAtEpoch = 0;
};

PendingUnlock pendingUnlock;

bool loadConfig() {
  preferences.begin(kConfigNamespace, true);
  config.deviceId = preferences.getString("device-id");
  config.lockId = preferences.getString("lock-id");
  config.mqttHost = preferences.getString("mqtt-host");
  config.mqttPort = preferences.getUShort("mqtt-port", 8883);
  config.mqttTopic = preferences.getString("mqtt-topic");
  config.acknowledgementUrl = preferences.getString("ack-url");
  config.wifiSsid = preferences.getString("wifi-ssid");
  config.wifiPassword = preferences.getString("wifi-password");
  config.caCertificatePem = preferences.getString("ca-pem");
  config.mqttClientCertificatePem = preferences.getString("mqtt-cert");
  config.mqttClientPrivateKeyPem = preferences.getString("mqtt-key");
  config.acknowledgementPrivateKeyPem = preferences.getString("ack-key");
  config.requireVerifiedPhonePresence = preferences.getBool("presence", true);
  preferences.end();

  return !config.deviceId.isEmpty() && !config.lockId.isEmpty() && !config.mqttHost.isEmpty() &&
         !config.mqttTopic.isEmpty() && !config.acknowledgementUrl.isEmpty() &&
         !config.wifiSsid.isEmpty() && !config.caCertificatePem.isEmpty() &&
         !config.mqttClientCertificatePem.isEmpty() && !config.mqttClientPrivateKeyPem.isEmpty() &&
         !config.acknowledgementPrivateKeyPem.isEmpty();
}

bool isTimeSynchronized() {
  return time(nullptr) > 1700000000;
}

String iso8601Now() {
  time_t now = time(nullptr);
  struct tm utc {};
  gmtime_r(&now, &utc);
  char value[25];
  strftime(value, sizeof(value), "%Y-%m-%dT%H:%M:%SZ", &utc);
  return String(value);
}

String randomNonce() {
  uint8_t bytes[16];
  for (auto &byte : bytes) byte = static_cast<uint8_t>(esp_random());
  char encoded[32];
  snprintf(encoded, sizeof(encoded), "%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x",
    bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
  return String(encoded);
}

bool wasCommandAlreadyHandled(const String &commandId) {
  preferences.begin(kConfigNamespace, true);
  const bool duplicate = preferences.getString(kLastCommandKey) == commandId;
  preferences.end();
  return duplicate;
}

void recordHandledCommand(const String &commandId) {
  preferences.begin(kConfigNamespace, false);
  preferences.putString(kLastCommandKey, commandId);
  preferences.end();
}

bool signAcknowledgement(const String &canonicalPayload, String &signatureBase64) {
  mbedtls_pk_context key;
  mbedtls_entropy_context entropy;
  mbedtls_ctr_drbg_context random;
  mbedtls_pk_init(&key);
  mbedtls_entropy_init(&entropy);
  mbedtls_ctr_drbg_init(&random);
  const char personalization[] = "verixora-controller-ack";
  bool succeeded = false;

  do {
    if (mbedtls_ctr_drbg_seed(&random, mbedtls_entropy_func, &entropy,
        reinterpret_cast<const unsigned char *>(personalization), strlen(personalization)) != 0) break;
    if (mbedtls_pk_parse_key(&key,
        reinterpret_cast<const unsigned char *>(config.acknowledgementPrivateKeyPem.c_str()), config.acknowledgementPrivateKeyPem.length() + 1,
        nullptr, 0) != 0) break;
    if (!mbedtls_pk_can_do(&key, MBEDTLS_PK_ECKEY)) break;

    uint8_t hash[32];
    if (mbedtls_sha256_ret(reinterpret_cast<const unsigned char *>(canonicalPayload.c_str()), canonicalPayload.length(), hash, 0) != 0) break;
    uint8_t signature[144];
    size_t signatureLength = 0;
    if (mbedtls_pk_sign(&key, MBEDTLS_MD_SHA256, hash, sizeof(hash), signature, &signatureLength,
        mbedtls_ctr_drbg_random, &random) != 0) break;
    unsigned char encoded[220];
    size_t encodedLength = 0;
    if (mbedtls_base64_encode(encoded, sizeof(encoded), &encodedLength, signature, signatureLength) != 0) break;
    signatureBase64 = String(reinterpret_cast<char *>(encoded)).substring(0, encodedLength);
    succeeded = true;
  } while (false);

  mbedtls_pk_free(&key);
  mbedtls_ctr_drbg_free(&random);
  mbedtls_entropy_free(&entropy);
  return succeeded;
}

bool postAcknowledgement(const String &commandId, const String &outcome, const String &details) {
  if (!isTimeSynchronized()) return false;
  const String occurredAt = iso8601Now();
  const String nonce = randomNonce();
  const String canonical = "Verixora.ControllerAck.v1|" + config.deviceId + "|" + commandId + "|" + outcome + "|" + occurredAt + "|" + nonce;
  String signature;
  if (!signAcknowledgement(canonical, signature)) return false;

  WiFiClientSecure apiTls;
  apiTls.setCACert(config.caCertificatePem.c_str());
  HTTPClient request;
  if (!request.begin(apiTls, config.acknowledgementUrl)) return false;
  request.addHeader("Content-Type", "application/json");
  StaticJsonDocument<768> body;
  body["deviceId"] = config.deviceId;
  body["commandId"] = commandId;
  body["outcome"] = outcome;
  body["occurredAtUtc"] = occurredAt;
  body["nonce"] = nonce;
  body["signatureBase64"] = signature;
  body["details"] = details;
  String payload;
  serializeJson(body, payload);
  const int status = request.POST(payload);
  request.end();
  return status == HTTP_CODE_OK;
}

bool verifyPresenceSignature(const String &canonicalPayload, const String &publicKeySpkiBase64, const String &signatureBase64) {
  mbedtls_pk_context key;
  mbedtls_pk_init(&key);
  bool succeeded = false;
  do {
    const size_t decodedKeyCapacity = publicKeySpkiBase64.length();
    unsigned char decodedKey[512];
    if (decodedKeyCapacity > sizeof(decodedKey)) break;
    size_t decodedKeyLength = 0;
    if (mbedtls_base64_decode(decodedKey, sizeof(decodedKey), &decodedKeyLength,
        reinterpret_cast<const unsigned char *>(publicKeySpkiBase64.c_str()), publicKeySpkiBase64.length()) != 0) break;
    if (mbedtls_pk_parse_public_key(&key, decodedKey, decodedKeyLength) != 0 || !mbedtls_pk_can_do(&key, MBEDTLS_PK_ECKEY)) break;

    unsigned char signature[144];
    size_t signatureLength = 0;
    if (signatureBase64.length() > 220 || mbedtls_base64_decode(signature, sizeof(signature), &signatureLength,
        reinterpret_cast<const unsigned char *>(signatureBase64.c_str()), signatureBase64.length()) != 0) break;
    uint8_t hash[32];
    if (mbedtls_sha256_ret(reinterpret_cast<const unsigned char *>(canonicalPayload.c_str()), canonicalPayload.length(), hash, 0) != 0) break;
    succeeded = mbedtls_pk_verify(&key, MBEDTLS_MD_SHA256, hash, sizeof(hash), signature, signatureLength) == 0;
  } while (false);
  mbedtls_pk_free(&key);
  return succeeded;
}

void clearPendingUnlock() {
  pendingUnlock = PendingUnlock{};
  if (bleServer != nullptr) BLEDevice::getAdvertising()->stop();
}

void publishPresenceChallenge() {
  if (presenceChallengeCharacteristic == nullptr || !pendingUnlock.active) return;
  StaticJsonDocument<512> challenge;
  challenge["version"] = 1;
  challenge["deviceId"] = config.deviceId;
  challenge["commandId"] = pendingUnlock.commandId;
  challenge["challenge"] = pendingUnlock.challenge;
  challenge["expiresAtUnixTimeSeconds"] = pendingUnlock.expiresAtEpoch;
  String serialized;
  serializeJson(challenge, serialized);
  presenceChallengeCharacteristic->setValue(serialized.c_str());
  BLEDevice::getAdvertising()->start();
}

void beginPendingUnlock(const String &commandId, const String &trustedMobileDeviceId, const String &trustedMobilePublicKeySpkiBase64, int64_t expiresAtEpoch) {
  clearPendingUnlock();
  pendingUnlock.active = true;
  pendingUnlock.commandId = commandId;
  pendingUnlock.trustedMobileDeviceId = trustedMobileDeviceId;
  pendingUnlock.trustedMobilePublicKeySpkiBase64 = trustedMobilePublicKeySpkiBase64;
  pendingUnlock.challenge = randomNonce();
  pendingUnlock.expiresAtEpoch = expiresAtEpoch;
  publishPresenceChallenge();
}

class PresenceProofCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic *characteristic) override {
    if (!pendingUnlock.active || pendingUnlock.presenceValidated || !isTimeSynchronized() || time(nullptr) >= pendingUnlock.expiresAtEpoch) return;
    const String rawProof = String(characteristic->getValue().c_str());
    StaticJsonDocument<768> proof;
    if (deserializeJson(proof, rawProof) != DeserializationError::Ok) return;
    const String deviceId = proof["deviceId"] | "";
    const String commandId = proof["commandId"] | "";
    const String challenge = proof["challenge"] | "";
    const String signatureBase64 = proof["signatureBase64"] | "";
    if (deviceId != pendingUnlock.trustedMobileDeviceId || commandId != pendingUnlock.commandId || challenge != pendingUnlock.challenge || signatureBase64.isEmpty()) return;
    const String canonical = "Verixora.BlePresence.v1|" + config.deviceId + "|" + commandId + "|" + challenge + "|" + String(pendingUnlock.expiresAtEpoch);
    if (!verifyPresenceSignature(canonical, pendingUnlock.trustedMobilePublicKeySpkiBase64, signatureBase64)) return;
    pendingUnlock.presenceValidated = true;
  }
};

void initializePresenceBle() {
  BLEDevice::init(("Verixora-" + config.deviceId.substring(0, 8)).c_str());
  bleServer = BLEDevice::createServer();
  BLEService *service = bleServer->createService(kPresenceServiceUuid);
  presenceChallengeCharacteristic = service->createCharacteristic(kPresenceChallengeCharacteristicUuid, BLECharacteristic::PROPERTY_READ);
  presenceProofCharacteristic = service->createCharacteristic(kPresenceProofCharacteristicUuid, BLECharacteristic::PROPERTY_WRITE);
  presenceProofCharacteristic->setCallbacks(new PresenceProofCallbacks());
  service->start();
  BLEDevice::getAdvertising()->addServiceUUID(kPresenceServiceUuid);
}

void unlockDoorPulse() {
  digitalWrite(kLockRelayPin, HIGH);
  delay(kUnlockPulseMilliseconds);
  digitalWrite(kLockRelayPin, LOW);
}

void processPendingUnlock() {
  if (!pendingUnlock.active) return;
  if (!isTimeSynchronized() || time(nullptr) >= pendingUnlock.expiresAtEpoch) {
    postAcknowledgement(pendingUnlock.commandId, "Failed", "Verified phone presence was not established before command expiry");
    recordHandledCommand(pendingUnlock.commandId);
    clearPendingUnlock();
    return;
  }
  if (!pendingUnlock.presenceValidated) return;
  if (digitalRead(kTamperPin) == LOW) {
    postAcknowledgement(pendingUnlock.commandId, "Failed", "Controller tamper input is active");
    recordHandledCommand(pendingUnlock.commandId);
    clearPendingUnlock();
    return;
  }
  unlockDoorPulse();
  postAcknowledgement(pendingUnlock.commandId, "Unlocked", "Controller validated Android Keystore BLE presence and pulsed lock relay");
  recordHandledCommand(pendingUnlock.commandId);
  clearPendingUnlock();
}

void handleCommand(const uint8_t *payload, unsigned int length) {
  StaticJsonDocument<2048> command;
  if (deserializeJson(command, payload, length) != DeserializationError::Ok) return;
  const String action = command["command"] | "";
  const String commandId = command["commandId"] | "";
  const String lockId = command["lockId"] | "";
  const int64_t expiresAtEpoch = command["expiresAtUnixTimeSeconds"] | 0;
  const String trustedMobileDeviceId = command["trustedMobileDeviceId"] | "";
  const String trustedMobilePublicKeySpkiBase64 = command["trustedMobilePublicKeySpkiBase64"] | "";
  if (action != "unlock" || commandId.isEmpty() || lockId != config.lockId || !isTimeSynchronized() || expiresAtEpoch <= time(nullptr) || expiresAtEpoch - time(nullptr) > kCommandMaxAgeSeconds || wasCommandAlreadyHandled(commandId)) {
    return;
  }

  if (config.requireVerifiedPhonePresence) {
    if (trustedMobileDeviceId.isEmpty() || trustedMobilePublicKeySpkiBase64.isEmpty()) {
      postAcknowledgement(commandId, "Failed", "Unlock command lacks a trusted mobile presence key");
      recordHandledCommand(commandId);
      return;
    }
    beginPendingUnlock(commandId, trustedMobileDeviceId, trustedMobilePublicKeySpkiBase64, expiresAtEpoch);
    return;
  }

  // Only development configurations may disable verified phone presence.
  beginPendingUnlock(commandId, "", "", expiresAtEpoch);
  pendingUnlock.presenceValidated = true;
}

void mqttCallback(char *topic, uint8_t *payload, unsigned int length) {
  if (String(topic) == config.mqttTopic + "/commands") handleCommand(payload, length);
}

bool connectMqtt() {
  if (mqtt.connected()) return true;
  const String clientId = "verixora-" + config.deviceId;
  return mqtt.connect(clientId.c_str());
}

void connectWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(config.wifiSsid.c_str(), config.wifiPassword.c_str());
  const uint32_t deadline = millis() + 20000;
  while (WiFi.status() != WL_CONNECTED && millis() < deadline) delay(250);
}
} // namespace

void setup() {
  Serial.begin(115200);
  pinMode(kLockRelayPin, OUTPUT);
  digitalWrite(kLockRelayPin, LOW); // fail locked on boot
  pinMode(kDoorContactPin, INPUT_PULLUP);
  pinMode(kTamperPin, INPUT_PULLUP);
  pinMode(kRequestToExitPin, INPUT_PULLUP);

  if (!loadConfig()) {
    Serial.println("Controller is not provisioned; relay remains disabled.");
    return;
  }
  connectWiFi();
  configTime(0, 0, "pool.ntp.org", "time.nist.gov");
  mqttTlsClient.setCACert(config.caCertificatePem.c_str());
  mqttTlsClient.setCertificate(config.mqttClientCertificatePem.c_str());
  mqttTlsClient.setPrivateKey(config.mqttClientPrivateKeyPem.c_str());
  mqtt.setServer(config.mqttHost.c_str(), config.mqttPort);
  // The command includes a P-256 SPKI public key for BLE presence verification.
  // PubSubClient defaults to 256 bytes, which would silently reject that packet.
  mqtt.setBufferSize(1024);
  mqtt.setCallback(mqttCallback);
  initializePresenceBle();
}

void loop() {
  if (config.deviceId.isEmpty()) { delay(1000); return; }
  if (WiFi.status() != WL_CONNECTED) connectWiFi();
  if (connectMqtt()) {
    mqtt.subscribe((config.mqttTopic + "/commands").c_str(), 1);
    mqtt.loop();
  }
  processPendingUnlock();
  delay(25);
}
