# Run and Test Guide

This guide explains how to run and validate the current project locally, with SQL Server in Docker, and with Postman.

## 1. Prerequisites

- .NET SDK 10.x (`dotnet --version`)
- Docker Desktop running
- Postman

## 2. Start SQL Server (Docker)

Use the same credentials currently configured in `appsettings.json`.

Create and run container:

```bash
docker run -d --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=1202lingSter89*" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

If the container already exists:

```bash
docker start nursingcare-sql
```

Optional checks:

```bash
docker ps
docker logs nursingcare-sql --tail 50
```

## 3. Run the API Locally

From `src`:

```bash
dotnet restore src.sln
dotnet run --project NursingCareBackend.Api --launch-profile http
```

Expected URL:

- API base URL: `http://localhost:5050`
- Swagger UI: `http://localhost:5050/swagger`

Notes:

- The app applies EF Core migrations automatically on startup.
- Database connection string is read from `NursingCareBackend.Api/appsettings.json`.

## 4. Verify with Postman

### 4.1 Health Check

- Method: `GET`
- URL: `http://localhost:5050/api/health`

Expected:

- `200 OK` with status payload when DB is reachable.
- `503 Service Unavailable` if DB is down or credentials/connection are wrong.

### 4.2 Create Care Request

- Method: `POST`
- URL: `http://localhost:5050/api/care-requests`
- Header: `Content-Type: application/json`
- Body:

```json
{
  "residentId": "11111111-1111-1111-1111-111111111111",
  "description": "Patient needs assistance with medication administration."
}
```

Expected:

- `201 Created`
- Response body:

```json
{
  "id": "generated-guid"
}
```

## 5. Troubleshooting

- Port `1433` in use:
Stop conflicting container/service or map SQL Server to another port and update `DefaultConnection`.
- `Login failed for user 'sa'`:
Ensure password matches `appsettings.json` and container environment.
- API starts but `/api/health` returns `503`:
Check SQL container status and startup logs.
- CORS errors from frontend:
Update `Cors:WebOrigins` and `Cors:MobileOrigins` in `appsettings.json`.

## 6. Stop Services

Stop API: `Ctrl + C` in the terminal running `dotnet run`.

Stop SQL Server container:

```bash
docker stop nursingcare-sql
```
