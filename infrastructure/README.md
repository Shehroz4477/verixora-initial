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
