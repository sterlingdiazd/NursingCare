# Development HTTPS And Logging Setup

Shared cross-project source of truth:

- [Environment Matrix](./ENVIRONMENT_MATRIX.md)

This backend is designed to run behind a local Nginx reverse proxy so the web app, Swagger, and Expo Go can all use the same stable public endpoint:

- Public API: `https://<your-lan-ip>:5050`
- Internal API container: `http://api:8080`
- SQL Server: `localhost:1433`

## What Is Committed

The repo includes the publishable parts of the setup:

- `docker-compose.yml`
- `nginx/default.conf`
- `.env.example`
- `scripts/generate-dev-cert.sh`
- API logging, correlation ID, Swagger auth, and forwarded-header support

The repo does **not** commit local-only artifacts:

- `.env`
- `nginx/certs/*`
- `nginx/ca/*`
- `.caddy/*`
- generated log files

## 1. Create Local Environment Variables

Copy the example file and set values for your machine:

```bash
cp .env.example .env
```

Update these values in `.env`:

- `PUBLIC_API_HOST`: your Mac LAN IP, for example `10.0.0.33`
- `PUBLIC_API_PORT`: usually `5050`
- `API_INTERNAL_PORT`: usually `8080`
- `SQL_PASSWORD`
- `JWT_KEY`

## 2. Generate Local TLS Assets

Generate a local root CA and server certificate:

```bash
chmod +x ./scripts/generate-dev-cert.sh
./scripts/generate-dev-cert.sh 10.0.0.33 5050
```

This creates:

- Root CA: `nginx/ca/rootCA.pem`
- Server certificate: `nginx/certs/server.crt`
- Server key: `nginx/certs/server.key`

## 3. Trust The Root Certificate

### macOS

1. Open `nginx/ca/rootCA.pem`
2. Import it into Keychain Access
3. Set it to `Always Trust`

### iPhone

1. Send `nginx/ca/rootCA.pem` to the phone
2. Install the profile
3. Enable it in:
   `Settings > General > About > Certificate Trust Settings`

If Safari on the phone cannot open `https://<your-lan-ip>:5050/api/health`, Expo Go will not be able to call the backend either.

## 4. Start The Stack

```bash
docker compose --env-file .env up -d --build
```

Then verify:

```bash
curl https://10.0.0.33:5050/api/health
curl https://10.0.0.33:5050/swagger/index.html
```

## 5. What The Proxy Does

Nginx owns the public port and forwards traffic to the API container:

- Public HTTPS: `:5050`
- Upstream API: `api:8080`

This avoids the old conflict where `dotnet run` and Docker both tried to own the same public port.

## 6. Backend Logging

The backend writes structured logs to:

- Console
- `src/NursingCareBackend.Api/Logs/backend-YYYYMMDD.log`

Request logs include:

- `CorrelationId`
- `RequestHost`
- `RequestScheme`
- `ClientApp`
- `ClientPlatform`
- `Authenticated`
- `User`
- `UserEmail`
- `UserId`
- `Roles`

## 7. Swagger Auth

Swagger is configured with a Bearer security scheme. Use the `Authorize` button and paste only the raw JWT token value.

## 8. Local Run Modes

There are two supported modes:

### Preferred

Use Docker + Nginx:

- stable public port
- same URL for Swagger, web, and mobile
- TLS works for physical-device testing

### Direct API debugging

Use `dotnet run` only when you want to debug the API process itself:

- HTTP: `http://localhost:8080`
- HTTPS: `https://localhost:8443`

Do not run `dotnet run` and the Docker public proxy on the same public port.
