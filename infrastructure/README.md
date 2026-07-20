# Verixora local infrastructure

This stack starts isolated local instances of SQL Server, PostgreSQL, Redis, and Mosquitto.
It is intended for development only. All host ports bind to `127.0.0.1`, so they are not available to other devices on the network.

## First run

1. Copy `.env.example` to `.env` in this directory.
2. Replace every password with a unique local development password. SQL Server requires a strong password.
3. From this directory, run:

   ```powershell
   docker compose up -d
   docker compose ps
   ```

## Ports

| Service | Host address |
|---|---|
| SQL Server | `127.0.0.1,14333` |
| PostgreSQL | `127.0.0.1:5433` |
| Redis | `127.0.0.1:6379` |
| MQTT | `127.0.0.1:1883` |

Do not expose this compose stack publicly. Production services will use managed networking, secrets management, TLS, authentication, database roles, and per-device MQTT certificates.

## API container image

Build the API image from the repository root. The image runs as an unprivileged user on port `8080` and exposes `GET /health/live` for the deployment platform's liveness probe.

```powershell
docker build --pull -f api-host/ApiHost/Dockerfile -t verixora-api:local .
```

The API is intentionally not included in the local infrastructure compose file: it must receive its database/Redis/JWT/OTP/biometric secrets and production data-protection certificate through the deployment platform's secret store. Deployment platforms should use `GET /health/live` only to determine whether the process is alive, and `GET /health/ready` to determine whether both the configured database and Redis are reachable. Neither endpoint returns exception details.

## Production data-protection keys

The API deliberately refuses to start outside `Development` unless its ASP.NET Core data-protection keys are persisted and encrypted with a private-key certificate. Inject these values through the deployment secret store, never through source-controlled JSON:

```text
DataProtection__KeyRingPath=/var/lib/verixora/data-protection
DataProtection__CertificatePath=/run/secrets/verixora-data-protection.pfx
DataProtection__CertificatePassword=<secret>
```

The key-ring directory must be persistent and writable by the API service account. The PFX must contain a private key and be mounted read-only with restrictive permissions. Development intentionally uses ephemeral keys so no local credential or stale Windows DPAPI key can affect the API.
