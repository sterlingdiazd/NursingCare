# NursingCare Backend API

A robust, enterprise-grade backend service built with .NET 8, designed to manage an enterprise of on-demand nursing care services and home residential care.

## Architecture & Patterns

The backend follows the principles of **Clean Architecture** and **Domain-Driven Design (DDD)**:

- **Api Layer**: Controllers for HTTP endpoints, authentication bootstrap, CORS, and migration management.
- **Application Layer**: Use cases, CQRS commands/queries, and handlers for orchestration.
- **Domain Layer**: Core business entities (e.g., CareRequest) and business logic.
- **Infrastructure Layer**: EF Core implementation, repository implementations, and external integrations.

---

## Key Features

### Authentication & Identity
- **JWT-based Authentication**: Secure user sessions.
- **Google OAuth2**: Integrated social login.
- **Registration & Profile Completion**: Managed user onboarding.
- **Role-Based Access Control (RBAC)**: Supports roles like Admin, Nurse, and Client.
- **Admin Setup**: Secure bootstrap of the initial admin user.

### Service Management
- **Full Lifecycle**: Request creation, listing, detailed views, and status transitions for nursing and residential care.
- **Workflow Transitions**: Built-in support for Approve, Reject, and Complete.
- **Nurse Assignment**: Facility for admins to manually assign nurses to service requests.
- **Price Management**: Automated price calculations with override capabilities.

### Admin Portal Support
- **Admin Accounts & Users Management**: Tools for managing the system's users.
- **Audit Logs**: Comprehensive tracking of system changes.
- **Dashboards & Notifications**: Real-time insights and alerts for admins.
- **Catalog & Options**: Management of system-wide catalog prices and service options.

### Observability & Reliability
- **Serilog Logging**: Comprehensive request and application logs.
- **Correlation IDs**: Trace requests end-to-end across services.
- **Health Checks**: Database connectivity and system health probes.
- **Automated Migrations**: Database schema updates applied on startup.

---

## Prerequisites

- .NET 8 SDK
- SQL Server reachable from the local machine (or Docker container)
- NuGet packages (automatically restored)

---

## Running the API

### Local Development

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

2. **Configure environment variables**:
   Set DB_PASSWORD or DB_SERVER to match your SQL instance.

3. **Start the API**:
   ```bash
   cd src/NursingCareBackend.Api
   dotnet run
   ```

### URLs
- **Swagger UI**: http://localhost:5050/swagger
- **Health Check**: http://localhost:5050/api/health

---

## Testing

**Mandatory: All tests must pass before committing any changes.**

To run the full test suite:
```bash
dotnet test
```

For more details on test configuration, see [NursingCareDocumentation/P0_TESTING_REPORT.md](../NursingCareDocumentation/P0_TESTING_REPORT.md).

---

## Security Note

- **Environment-based Secrets**: All production and shared secrets (JWT keys, DB passwords) must be provided via environment variables.
- **No Hardcoded Credentials**: Placeholders like <YOUR_SECURE_PASSWORD> are used in examples; NEVER commit real passwords.
