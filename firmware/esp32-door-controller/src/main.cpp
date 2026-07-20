#include <Arduino.h>
#include <ArduinoJson.h>
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

bool hasRecentVerifiedPhonePresence(const String &commandId) {
  // This hook is deliberately fail-closed. The companion Android app must establish a BLE
  // challenge/response session using an Android Keystore-bound key before this returns true.
  // A MAC address, RSSI, motion sensor, or static device fingerprint is never acceptable proof.
  (void)commandId;
  return false;
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

void unlockDoorPulse() {
  digitalWrite(kLockRelayPin, HIGH);
  delay(kUnlockPulseMilliseconds);
  digitalWrite(kLockRelayPin, LOW);
}

void handleCommand(const uint8_t *payload, unsigned int length) {
  StaticJsonDocument<768> command;
  if (deserializeJson(command, payload, length) != DeserializationError::Ok) return;
  const String action = command["command"] | "";
  const String commandId = command["commandId"] | "";
  const String lockId = command["lockId"] | "";
  const int64_t expiresAtEpoch = command["expiresAtUnixTimeSeconds"] | 0;
  if (action != "unlock" || commandId.isEmpty() || lockId != config.lockId || !isTimeSynchronized() || expiresAtEpoch <= time(nullptr) || expiresAtEpoch - time(nullptr) > kCommandMaxAgeSeconds || wasCommandAlreadyHandled(commandId)) {
    return;
  }

  if (config.requireVerifiedPhonePresence && !hasRecentVerifiedPhonePresence(commandId)) {
    postAcknowledgement(commandId, "Failed", "Verified phone presence was not established");
    recordHandledCommand(commandId);
    return;
  }

  // Tamper and door contact are audit inputs. The mechanical egress path must never be disabled.
  if (digitalRead(kTamperPin) == LOW) {
    postAcknowledgement(commandId, "Failed", "Controller tamper input is active");
    recordHandledCommand(commandId);
    return;
  }

  unlockDoorPulse();
  postAcknowledgement(commandId, "Unlocked", "Controller pulsed lock relay");
  recordHandledCommand(commandId);
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
  mqtt.setCallback(mqttCallback);
}

void loop() {
  if (config.deviceId.isEmpty()) { delay(1000); return; }
  if (WiFi.status() != WL_CONNECTED) connectWiFi();
  if (connectMqtt()) {
    mqtt.subscribe((config.mqttTopic + "/commands").c_str(), 1);
    mqtt.loop();
  }
  delay(25);
}
