# Documentation Index

Complete guide to all documentation files in this project.

---

## Start Here

### First Time Setup
- **[README_SETUP.md](README_SETUP.md)** - Complete setup guide (20 minutes)
  - System requirements
  - 3-step setup process
  - Architecture overview
  - Troubleshooting basics

### Quick Start
- **[QUICK_START.md](QUICK_START.md)** - Quick reference (5 minutes)
  - Key commands
  - Environment variables
  - Endpoints summary
  - Troubleshooting quick fixes

---

## Detailed Guides

### Database & Docker
- **[DOCKER_SETUP.md](DOCKER_SETUP.md)** - Docker & SQL Server guide (30 minutes)
  - Docker installation
  - Running SQL Server in Docker
  - Container management
  - Data persistence
  - Performance tuning
  - Docker Compose setup

### Running & Testing
- **[src/RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md)** - Comprehensive guide (45 minutes)
  - Step-by-step setup
  - Environment variable configuration (3 methods)
  - SQL Server setup
  - API startup
  - Endpoint testing with curl
  - Postman setup
  - Swagger testing
  - Complete workflow examples
  - Troubleshooting solutions

### Testing Scenarios
- **[TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)** - Real-world examples (40 minutes)
  - Scenario 1: Registration & Login
  - Scenario 2: Creating Care Requests
  - Scenario 3: Authorization & Security
  - Scenario 4: Error Handling
  - Scenario 5: Complete Workflow
  - Scenario 6: Token Expiration
  - Bash script examples
  - Test case summary table

---

## API Tools & Collections

### Postman
- **[Postman_Collection.json](Postman_Collection.json)** - Pre-built API requests
  - Import into Postman
  - Pre-configured endpoints
  - Variable templates
  - Ready to use

### Swagger UI
- **Built-in:** http://localhost:5050/swagger (when API is running)
  - Interactive API documentation
  - Try it out functionality
  - Request/response examples
  - Authorization flow

---

## Architecture Documentation

### Project Structure
- **[src/README.md](src/README.md)** - Architecture documentation (15 minutes)
  - Project layers (API, Application, Infrastructure, Domain)
  - Entity models
  - Service architecture
  - Database schema
  - Authentication flow

### Source Code Files

#### Authentication
- `src/NursingCareBackend.Api/Controllers/Auth/AuthController.cs`
  - `/api/auth/register` endpoint
  - `/api/auth/login` endpoint

- `src/NursingCareBackend.Application/Identity/Services/AuthenticationService.cs`
  - Registration logic
  - Login logic
  - Token generation

- `src/NursingCareBackend.Infrastructure/Authentication/PasswordHasher.cs`
  - PBKDF2-SHA256 hashing
  - Password verification

- `src/NursingCareBackend.Infrastructure/Authentication/TokenGenerator.cs`
  - JWT token creation
  - Claims management

#### Repositories
- `src/NursingCareBackend.Infrastructure/Identity/UserRepository.cs`
  - User CRUD operations
  - Email lookup

- `src/NursingCareBackend.Infrastructure/Identity/RoleRepository.cs`
  - Role management
  - Role lookup

#### Configuration
- `src/NursingCareBackend.Infrastructure/ConnectionStringResolver.cs`
  - Environment variable substitution
  - Default value fallback

---

## Configuration Files

### appsettings.json
- `src/NursingCareBackend.Api/appsettings.json`
  - Placeholder values: `{DB_SERVER}`, `{DB_NAME}`, `{DB_USER}`, `{DB_PASSWORD}`, `{JWT_KEY}`
  - Replaced with environment variables at runtime
  - Includes CORS configuration

### launchSettings.json
- `src/NursingCareBackend.Api/Properties/launchSettings.json`
  - Development profile configuration
  - Port settings (5050)
  - Environment variables

---

## Database & Migrations

### Migration Files
- `src/NursingCareBackend.Infrastructure/Migrations/`
  - `20260121213422_InitialCreate.cs` - Initial schema
  - `20260306165531_AddUserAndRoleEntities.cs` - Auth tables
  - `20260316225309_SeedDefaultRoles.cs` - Default roles (Admin, Nurse, User)

### Database Schema
- **Users Table**
  - Id (GUID primary key)
  - Email (unique, indexed)
  - PasswordHash
  - IsActive
  - CreatedAtUtc

- **Roles Table**
  - Id (GUID primary key)
  - Name (unique, indexed)
  - Predefined: Admin, Nurse, User

- **UserRoles Table**
  - UserId (FK to Users)
  - RoleId (FK to Roles)
  - Composite primary key

- **CareRequests Table**
  - Id (GUID primary key)
  - ResidentId (GUID)
  - Description
  - Status (Pending/Active/Completed)
  - CreatedAtUtc

---

## Environment Variables

### All Variables

| Variable | Purpose | Default | Required |
|----------|---------|---------|----------|
| `DB_SERVER` | SQL Server host:port | `localhost,1433` | No |
| `DB_NAME` | Database name | `NursingCareDb` | No |
| `DB_USER` | SQL user | `sa` | No |
| `DB_PASSWORD` | SQL password | `YourStrong!Passw0rd` | **Yes** |
| `JWT_KEY` | JWT signing key | `ChangeThisDevelopmentKeyToARealSecret` | No |

### Setting Methods

1. **Terminal Export** (macOS/Linux)
   ```bash
   export DB_PASSWORD="YourStrong!Passw0rd"
   ```

2. **.env File**
   ```bash
   DB_PASSWORD=YourStrong!Passw0rd
   ```

3. **Windows PowerShell**
   ```powershell
   $env:DB_PASSWORD = "YourStrong!Passw0rd"
   ```

4. **System Environment Variables**
   - Windows: Control Panel → Environment Variables
   - macOS: Terminal or .zshrc/.bash_profile
   - Linux: /etc/environment or .bashrc

---

## API Endpoints Reference

### Authentication Endpoints

```
POST /api/auth/register
POST /api/auth/login
```

**See:** [Testing with curl](#testing-with-curl) in RUN_AND_TEST_GUIDE.md

### Care Request Endpoints

```
POST /api/care-requests (requires Nurse or Admin role)
GET /api/care-requests (requires Nurse or Admin role)
GET /api/care-requests/{id} (requires Nurse or Admin role)
```

### System Endpoints

```
GET /health
GET /swagger
```

---

## Testing Methods

### 1. Swagger UI (Easiest)
- Run API
- Open http://localhost:5050/swagger
- Use "Try it out" button
- See [RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md#testing-with-swagger-ui)

### 2. Postman
- Import [Postman_Collection.json](Postman_Collection.json)
- Set variables
- Execute requests
- See [RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md#testing-with-postman)

### 3. cURL (Command Line)
- Use curl commands
- Set Bearer token
- See [QUICK_START.md](QUICK_START.md#4-test-endpoints)

### 4. VS Code REST Client
- Install extension
- Create .rest file
- Use predefined requests

### 5. Complete Workflows
- See [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)
- Copy-paste bash scripts
- Real-world examples

---

## Troubleshooting by Topic

### Docker & SQL Server
- **[DOCKER_SETUP.md](DOCKER_SETUP.md#troubleshooting)**
  - Port conflicts
  - Connection issues
  - Password problems
  - Resource allocation

### API Setup & Running
- **[RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md#troubleshooting)**
  - API won't start
  - Database connection failed
  - Port conflicts
  - Environment variable issues

### Testing Endpoints
- **[TESTING_SCENARIOS.md](TESTING_SCENARIOS.md#scenario-4-error-handling)**
  - Authentication errors
  - Validation errors
  - Authorization failures
  - Missing data

### Quick Fixes
- **[QUICK_START.md](QUICK_START.md#troubleshooting)**
  - Port in use
  - Connection refused
  - Unauthorized errors
  - Forbidden errors

---

## Development Workflow

### 1. Initial Setup (One Time)
1. Read [README_SETUP.md](README_SETUP.md)
2. Follow [DOCKER_SETUP.md](DOCKER_SETUP.md)
3. Set environment variables
4. Run API

### 2. Daily Development
1. Start SQL Server: `docker start nursingcare-sql`
2. Set env vars
3. Run API: `dotnet run --project src/NursingCareBackend.Api`
4. Test using Swagger or Postman

### 3. Testing
1. Use [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)
2. Import [Postman_Collection.json](Postman_Collection.json)
3. Follow [RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md)

### 4. Troubleshooting
1. Check [QUICK_START.md](QUICK_START.md) first
2. Review specific guide
3. Search error messages
4. Check [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md) for error handling

---

## Key Files at a Glance

| File | Type | Purpose |
|------|------|---------|
| README_SETUP.md | Guide | Main setup guide |
| QUICK_START.md | Reference | Quick commands |
| DOCKER_SETUP.md | Guide | Docker configuration |
| src/RUN_AND_TEST_GUIDE.md | Guide | Detailed testing |
| TESTING_SCENARIOS.md | Examples | Real-world tests |
| Postman_Collection.json | Tool | API requests |
| DOCUMENTATION_INDEX.md | Index | This file |
| src/README.md | Architecture | Code structure |
| src/NursingCareBackend.Api/appsettings.json | Config | App settings |

---

## Reading Time Guide

| Document | Time | Best For |
|----------|------|----------|
| QUICK_START.md | 5 min | Quick reference |
| README_SETUP.md | 20 min | New users |
| DOCKER_SETUP.md | 30 min | Docker setup |
| src/RUN_AND_TEST_GUIDE.md | 45 min | Complete guide |
| TESTING_SCENARIOS.md | 40 min | Testing examples |
| src/README.md | 15 min | Architecture |
| DOCUMENTATION_INDEX.md | 10 min | Navigation |

---

## Common Tasks

### I want to...

**Run the API for the first time**
→ Read [README_SETUP.md](README_SETUP.md) section "3-Step Setup"

**Start developing**
→ Follow [QUICK_START.md](QUICK_START.md)

**Test endpoints**
→ Use [Postman_Collection.json](Postman_Collection.json) or [QUICK_START.md](QUICK_START.md) section 4

**Understand the architecture**
→ Read [src/README.md](src/README.md)

**Fix a database problem**
→ Check [DOCKER_SETUP.md](DOCKER_SETUP.md#troubleshooting)

**Debug an error**
→ Look in [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md#scenario-4-error-handling)

**Set up Docker**
→ Follow [DOCKER_SETUP.md](DOCKER_SETUP.md) section "Running SQL Server"

**See real-world examples**
→ Check [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)

**Use environment variables**
→ Read [README_SETUP.md](README_SETUP.md#environment-variables)

**Debug authorization issues**
→ See [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md#scenario-3-authorization--security)

---

## Support Resources

### Inside This Project
- All documentation files above
- Swagger UI: http://localhost:5050/swagger
- Code comments in source files

### External Resources
- JWT Documentation: https://jwt.io
- .NET Documentation: https://docs.microsoft.com/dotnet
- Entity Framework: https://docs.microsoft.com/ef
- Docker: https://docs.docker.com
- SQL Server: https://docs.microsoft.com/sql

---

## Version Information

- **Created:** March 16, 2026
- **Last Updated:** March 16, 2026
- **.NET SDK:** 10.x
- **SQL Server:** 2022
- **Status:** ✅ Ready for Development

---

## Quick Links

| What | Link |
|------|------|
| **Start Here** | [README_SETUP.md](README_SETUP.md) |
| **Quick Commands** | [QUICK_START.md](QUICK_START.md) |
| **Docker Setup** | [DOCKER_SETUP.md](DOCKER_SETUP.md) |
| **Detailed Guide** | [src/RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md) |
| **Test Examples** | [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md) |
| **API Requests** | [Postman_Collection.json](Postman_Collection.json) |
| **Architecture** | [src/README.md](src/README.md) |
| **This Index** | [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md) |

---

**All documentation is complete and ready to use!**

**Start with [README_SETUP.md](README_SETUP.md) if you're new to the project.**
