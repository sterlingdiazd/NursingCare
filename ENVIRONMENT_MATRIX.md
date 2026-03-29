# NursingCareProject Environment Matrix

Last reviewed: 2026-03-17

This document is the single source of truth for environment configuration across:

- `NursingCareBackend`
- `NursingCareWeb/nursing_care_web_react`
- `NursingCareMobile/nursing-care-mobile`

The goal is that web and mobile switch environments by configuration only, with no feature-level code edits.

## Core Rules

1. Backend owns the public API endpoint definition.
2. Web and mobile consume that endpoint only through environment variables.
3. Local physical-device testing uses the HTTPS reverse proxy endpoint.
4. Docker and direct `dotnet run` are different run modes and should not share the same public port.

## Canonical Variables

### Backend

- `PUBLIC_API_HOST`
- `PUBLIC_API_PORT`
- `API_INTERNAL_PORT`
- `ASPNETCORE_ENVIRONMENT`
- `DB_HOST`
- `DB_PORT`
- `DB_NAME`
- `DB_USER`
- `DB_PASSWORD`
- `JWT_KEY`

### Web

- `VITE_API_BASE_URL`
- `VITE_API_PROXY_TARGET`

### Mobile

- `EXPO_PUBLIC_API_BASE_URL`

## Environment Matrix

| Environment | Backend public URL | Backend run mode | Web variables | Mobile variables | Notes |
| --- | --- | --- | --- | --- | --- |
| `local` | `https://<lan-ip>:5050` | Docker Compose + Nginx reverse proxy | `VITE_API_BASE_URL=<https://<lan-ip>>:5050/api` `VITE_API_PROXY_TARGET=<https://<lan-ip>>:5050` | `EXPO_PUBLIC_API_BASE_URL=<https://<lan-ip>>:5050` | Preferred day-to-day dev mode. Works for Swagger, web, Expo Go, and device testing. |
| `docker` | `https://<lan-ip>:5050` | Same as local | Same as local | Same as local | In this project, `docker` is the same public endpoint strategy as local. The difference is just whether you are using the composed stack as your main runtime. |
| `staging` | `https://api-staging.<your-domain>` | Hosted environment | `VITE_API_BASE_URL=<https://api-staging.<your-domain>>/api` `VITE_API_PROXY_TARGET=<https://api-staging.<your-domain>>` | `EXPO_PUBLIC_API_BASE_URL=<https://api-staging.<your-domain>>` | No local cert trust steps should be required in staging. |
| `production` | `https://api.<your-domain>` | Hosted environment | `VITE_API_BASE_URL=<https://api.<your-domain>>/api` `VITE_API_PROXY_TARGET=<https://api.<your-domain>>` | `EXPO_PUBLIC_API_BASE_URL=<https://api.<your-domain>>` | Production secrets must come from secure environment configuration, not committed files. |

## Local Development Setup

### Backend

Backend local configuration lives in:

- `NursingCareBackend/.env`

Start from:

- `NursingCareBackend/.env.example`

Important local values:

```env
PUBLIC_API_HOST=<LAN_IP>
PUBLIC_API_PORT=5050
API_INTERNAL_PORT=8080
ASPNETCORE_ENVIRONMENT=Development
```

### Web

Web local configuration lives in:

- `NursingCareWeb/nursing_care_web_react/.env.local`

Start from:

- `NursingCareWeb/nursing_care_web_react/.env.example`

Example:

```env
VITE_API_BASE_URL=https://<LAN_IP>:5050/api
VITE_API_PROXY_TARGET=https://<LAN_IP>:5050
```

### Mobile

Mobile local configuration lives in:

- `NursingCareMobile/nursing-care-mobile/.env.local`

Start from:

- `NursingCareMobile/nursing-care-mobile/.env.example`

Example:

```env
EXPO_PUBLIC_API_BASE_URL=https://<LAN_IP>:5050
```

## Current Local Automation

Use:

```bash
./scripts/sync-dev-endpoints.sh
```

This script:

- detects the current LAN IP
- updates `NursingCareBackend/.env`
- updates `NursingCareWeb/nursing_care_web_react/.env.local`
- updates `NursingCareMobile/nursing-care-mobile/.env.local`
- regenerates the local TLS server cert for the current IP

Then start the backend stack with:

```bash
./scripts/dev-up.sh
```

## Direct API Debugging

If you need to debug the API process outside the proxy, use `dotnet run` on its debug-only ports and do not repoint web/mobile to those ports unless you intentionally want that temporary mode.

The shared default for web/mobile remains the reverse-proxy endpoint.

## Staging And Production Guidance

### Backend

- Set public host, DB config, and JWT settings through deployment environment variables or secret managers.
- Do not commit real staging/production secrets.

### Web

- Provide `VITE_API_BASE_URL` and `VITE_API_PROXY_TARGET` from the deployment environment or CI pipeline.

### Mobile

- Provide `EXPO_PUBLIC_API_BASE_URL` through the build profile or release environment.
- Do not hardcode staging or production endpoints in feature code.

## Acceptance Checklist

- Web switches environments by env vars only.
- Mobile switches environments by env vars only.
- No service file contains environment-specific feature URLs.
- Local physical-device testing uses the same public API URL as Swagger and web.
- Staging and production values can be supplied without source edits.
