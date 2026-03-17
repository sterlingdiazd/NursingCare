# NursingCareBackend

Backend service for managing nursing care requests. This project is implemented in ASP.NET Core with a layered architecture (`Api`, `Application`, `Domain`, `Infrastructure`) and SQL Server persistence via Entity Framework Core.

Additional setup guide:

- `DEV_REVERSE_PROXY_SETUP.md` for the fixed-port HTTPS proxy, local certificate trust, and cross-device development setup

## Overview

- Framework: .NET `net10.0`
- API style: REST controllers
- Database: SQL Server
- ORM: Entity Framework Core
- API docs: Swagger UI enabled in all environments
- Migration strategy: Pending EF Core migrations are applied automatically on application startup
- Observability: Serilog request logging with correlation IDs

## Solution Structure

- `src.sln` - Solution entry point
- `NursingCareBackend.Api` - HTTP API layer (controllers, startup, CORS, migration bootstrap)
- `NursingCareBackend.Application` - Use-case orchestration and application contracts
- `NursingCareBackend.Domain` - Core entities and business rules
- `NursingCareBackend.Infrastructure` - EF Core, repositories, dependency wiring, migrations

## Architecture

The solution follows a clean/layered approach:

1. `Api` receives HTTP requests and maps request DTOs to commands.
2. `Application` executes use cases through handlers.
3. `Domain` enforces invariants in entities.
4. `Infrastructure` persists domain objects and integrates external concerns (SQL Server).

Current implemented flows:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/setup-admin`
- `POST /api/auth/assign-role`
- `POST /api/care-requests`
- `GET /api/care-requests`
- `GET /api/care-requests/{id}`
- Controller creates `CreateCareRequestCommand`
- `CreateCareRequestHandler` builds a `CareRequest` domain entity
- `ICareRequestRepository` persists via `CareRequestRepository`

## Domain Model

### CareRequest

Defined in `NursingCareBackend.Domain/CareRequests/CareRequest.cs`.

Fields:

- `Id` (`Guid`)
- `ResidentId` (`Guid`)
- `Description` (`string`, required, max 1000 chars)
- `Status` (`CareRequestStatus`)
- `CreatedAtUtc` (`DateTime`)

Business rules enforced on creation:

- `ResidentId` must not be empty
- `Description` must not be null/empty/whitespace
- Initial status is `Pending`
- `CreatedAtUtc` set to `DateTime.UtcNow`

### CareRequestStatus

Enum values:

- `Pending = 0`
- `Approved = 1`
- `Rejected = 2`
- `Completed = 3`

## API Endpoints

### Health Check

- Method: `GET`
- Route: `/api/health`
- Behavior:
  - Attempts to open a SQL connection using `ConnectionStrings:DefaultConnection`
  - Returns `200 OK` with status payload if DB connection succeeds
  - Returns `503 Service Unavailable` if DB is unreachable

Example success payload:

```json
{
  "status": "Healthy",
  "timestamp": "2026-03-03T20:00:00Z",
  "database": "Connected"
}
```

### Create Care Request

- Method: `POST`
- Route: `/api/care-requests`
- Authentication: Bearer token required, `Nurse` or `Admin` role required
- Request body:

```json
{
  "residentId": "11111111-1111-1111-1111-111111111111",
  "description": "Patient needs assistance with medication administration."
}
```

- Response:
  - `201 Created`
  - Body:

```json
{
  "id": "generated-guid"
}
```

## Configuration

Main settings files:

- `src/NursingCareBackend.Api/appsettings.json`
- `src/NursingCareBackend.Api/appsettings.Development.json`
- `.env` for Docker/reverse-proxy development

Key sections:

- `ConnectionStrings:DefaultConnection`
- `Cors:WebOrigins`
- `Cors:MobileOrigins`

### Secrets and environment variables

- **Database**:

  - `ConnectionStrings:DefaultConnection` in `appsettings*.json` uses placeholders such as `{DB_SERVER}` and `{DB_PASSWORD}`.
  - At runtime, environment variables are substituted into the connection string in `NursingCareBackend.Infrastructure/ConnectionStringResolver.cs`.
  - Alternatively, you can override the entire connection string via `ConnectionStrings__DefaultConnection` (standard .NET configuration).
  - In CI and local/dev, you must set **either**:
    - `ConnectionStrings__DefaultConnection` **or**
    - the required DB environment variables (`DB_SERVER`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`).

- **Tests**:

  - Integration tests use `NursingCare_TestSqlConnection` to connect to a separate test database.

- **JWT**:
  - JWT settings are configured under `Jwt` in `appsettings.json`.
  - For production, set `Jwt__Key` (and optionally `Jwt__Issuer`, `Jwt__Audience`) via environment variables rather than committing real secrets.

Note: In `appsettings.Development.json` and `appsettings.Docker.json`, `ConnectionStrings` and `Cors` are currently nested under `Logging`. The active default root-level values are defined in `appsettings.json`.

Default CORS policy configured and applied: `AllowAllDev`.

Default allowed origins:

- Web: `http://localhost:3000`
- Mobile: `http://localhost:19006`

## Local Development

### Prerequisites

- .NET SDK supporting `net10.0`
- SQL Server instance reachable from local machine

### Run

Preferred local development uses Docker Compose plus the Nginx reverse proxy documented in `DEV_REVERSE_PROXY_SETUP.md`.

For direct API debugging:

```bash
dotnet restore NursingCareBackend.slnx
dotnet run --project src/NursingCareBackend.Api/NursingCareBackend.Api.csproj
```

Direct launch profile URLs are:

- `http://localhost:8080` (http profile)
- `https://localhost:8443` and `http://localhost:8080` (https profile)

Swagger UI:

- `http://localhost:8080/swagger`
- `https://localhost:8443/swagger`

For shared local development across web and mobile, use the reverse proxy endpoint:

- `https://<lan-ip>:5050/swagger/index.html`

## Database & Migrations

- EF migrations are located in `NursingCareBackend.Infrastructure/Migrations`
- Startup calls `db.Database.Migrate()` via `ApplyMigrations()`
- Initial migration creates table `CareRequests`

## Dependency Injection

Registered in `NursingCareBackend.Infrastructure/DependencyInjection.cs`:

- `NursingCareDbContext` with SQL Server provider
- `ICareRequestRepository -> CareRequestRepository`

Registered in `NursingCareBackend.Api/Program.cs`:

- Controllers
- Swagger
- CORS policy
- `CreateCareRequestHandler`

## Current Scope and Gaps

Implemented:

- Health check endpoint with DB connectivity probe
- Create care request use case
- Persistence for care requests

Not currently implemented:

- Automated tests for the new auth/logging/proxy behavior
- Update/delete care request endpoints
- Metrics and distributed tracing

## Security Note

- App settings and CI workflow files are designed to use **placeholders only** (`{SQL_PASSWORD}`, example passwords) and expect real secrets from environment variables.
- For production and shared environments, always provide:
  - Database secrets via `ConnectionStrings__DefaultConnection` or `SQL_PASSWORD`.
  - JWT signing key via `Jwt__Key`.
  - Test-only connection strings via `NursingCare_TestSqlConnection` (for CI/test environments only).

## Quick Links

- Run and test steps: `RUN_AND_TEST_GUIDE.md`
- Cross-project continuation plan: `DEVELOPMENT_CONTINUATION_GUIDE.md`
