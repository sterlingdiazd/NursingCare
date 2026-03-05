# NursingCareBackend

Backend service for managing nursing care requests. This project is implemented in ASP.NET Core with a layered architecture (`Api`, `Application`, `Domain`, `Infrastructure`) and SQL Server persistence via Entity Framework Core.

## Overview

- Framework: .NET `net10.0`
- API style: REST controllers
- Database: SQL Server
- ORM: Entity Framework Core
- API docs: Swagger UI enabled in all environments
- Migration strategy: Pending EF Core migrations are applied automatically on application startup

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

Current implemented flow:

- `POST /api/care-requests`
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

Main settings file: `NursingCareBackend.Api/appsettings.json`

Key sections:

- `ConnectionStrings:DefaultConnection`
- `Cors:WebOrigins`
- `Cors:MobileOrigins`

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

From the `src` directory:

```bash
dotnet restore src.sln
dotnet run --project NursingCareBackend.Api
```

By default (launch profiles), API URLs are:

- `http://localhost:5050` (http profile)
- `https://localhost:5050` and `http://localhost:5051` (https profile)

Swagger UI:

- `http://localhost:5050/swagger` (or active host/port)

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

- Automated tests
- Authentication/authorization
- Update/read/list/delete endpoints for care requests
- Input validation pipeline beyond domain constructor checks
- Observability setup (structured logging, tracing, metrics)

## Security Note

Current configuration files include a SQL Server credential in plaintext. For production and shared environments, move secrets to secure configuration providers (for example: environment variables, user secrets, or a secret manager) and rotate exposed credentials.

## Quick Links

- Run and test steps: `RUN_AND_TEST_GUIDE.md`
- Cross-project continuation plan: `DEVELOPMENT_CONTINUATION_GUIDE.md`
