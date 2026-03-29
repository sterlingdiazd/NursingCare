# Nursing Care Backend API - Complete Setup Guide

Welcome! This guide will help you get started with the Nursing Care Backend API. Everything is documented here.

---

## Quick Navigation

### For First-Time Setup
1. **New to the project?** → Read **[QUICK_START.md](QUICK_START.md)** (5 minutes)
2. **Need Docker help?** → See **[DOCKER_SETUP.md](DOCKER_SETUP.md)**
3. **Want detailed instructions?** → Follow **[src/RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md)**

### For Testing
1. **Quick test?** → See **[QUICK_START.md](QUICK_START.md)** section 4
2. **Specific scenarios?** → Check **[TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)**
3. **Postman setup?** → Import **[Postman_Collection.json](Postman_Collection.json)**
4. **Swagger documentation?** → Open <http://localhost:5050/swagger> when API is running

---

## What's Included?

This backend API provides:

### Authentication System
-  User registration with email validation
-  User login with password verification
-  JWT token generation and validation
-  Role-based access control (Admin, Nurse, User)
-  Secure password hashing (PBKDF2-SHA256)

### Care Request Management
-  Create care requests
-  View all care requests
-  View specific care request by ID
-  Role-based authorization (Nurses and Admins only)

### Developer Tools
-  Swagger/OpenAPI documentation
-  Environment variable configuration
-  Docker support for SQL Server
-  Comprehensive error handling
-  Automatic database migrations

---

## Architecture

```
NursingCareBackend/
├── src/
│   ├── NursingCareBackend.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   ├── Auth/AuthController.cs       # Login/Register endpoints
│   │   │   └── CareRequests/                # Care request endpoints
│   │   ├── appsettings.json                 # Config with env variable placeholders
│   │   └── Program.cs                       # Service registration & middleware
│   ├── NursingCareBackend.Application/      # Business logic layer
│   │   └── Identity/                        # Auth services
│   ├── NursingCareBackend.Infrastructure/   # Data access layer
│   │   ├── Authentication/                  # Password hashing, token generation
│   │   ├── Identity/                        # Repositories for User/Role
│   │   ├── Persistence/                     # EF Core DbContext
│   │   └── Migrations/                      # Database migrations
│   ├── NursingCareBackend.Domain/           # Domain models
│   │   └── Identity/                        # User, Role, UserRole entities
│   └── README.md                            # Architecture documentation
├── tests/                                   # Unit and integration tests
├── QUICK_START.md                           # 5-minute quick start
├── DOCKER_SETUP.md                          # Docker & SQL Server guide
├── TESTING_SCENARIOS.md                     # Real-world test examples
└── README_SETUP.md                          # This file
```

---

## System Requirements

### Development Machine
- **.NET SDK 10.x** - Download from <https://dotnet.microsoft.com/download>
- **Docker Desktop** - Download from <https://www.docker.com/products/docker-desktop>
- **Postman** (optional) - For API testing
- **Git** - Version control

### Minimum Hardware
- **Processor:** 2 cores
- **RAM:** 4GB
- **Disk:** 2GB free space

### Operating System
- macOS 10.14+
- Windows 10/11
- Linux (Ubuntu 18.04+ recommended)

---

## 3-Step Setup

### Step 1: Clone Repository

```bash
# Clone the project
git clone <repository-url>
cd NursingCareProject/NursingCareBackend

# Restore dependencies
dotnet restore
```

### Step 2: Start SQL Server

```bash
# Start Docker SQL Server
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Wait 10-15 seconds for startup
sleep 15
```

### Step 3: Run the API

```bash
# Set environment variables
export DB_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="dev-secret-key"

# Run the API
dotnet run --project src/NursingCareBackend.Api
```

**API is running at:** <http://localhost:5050>

---

## API Endpoints

### Authentication (No Auth Required)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login & get token |

### Care Requests (Auth Required)

| Method | Endpoint | Purpose | Roles |
|--------|----------|---------|-------|
| POST | `/api/care-requests` | Create request | Nurse, Admin |
| GET | `/api/care-requests` | List all requests | Nurse, Admin |
| GET | `/api/care-requests/{id}` | Get specific request | Nurse, Admin |

### System

| Method | Endpoint | Purpose | Auth |
|--------|----------|---------|------|
| GET | `/health` | Health check | No |
| GET | `/swagger` | API documentation | No |

---

## Environment Variables

Required environment variables:

```bash
DB_SERVER="localhost,1433"          # SQL Server host:port
DB_NAME="NursingCareDb"             # Database name
DB_USER="sa"                        # SQL user (default sa)
DB_PASSWORD="YourStrong!Passw0rd"   # SQL password (must match Docker)
JWT_KEY="secret-key-min-32-chars"   # JWT signing key
```

### Setting Variables

**Option 1: Terminal (macOS/Linux)**
```bash
export DB_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="dev-secret-key"
dotnet run --project src/NursingCareBackend.Api
```

**Option 2: .env file**
Create `.env` in project root:
```bash
DB_SERVER=localhost,1433
DB_PASSWORD=YourStrong!Passw0rd
JWT_KEY=dev-secret-key
```

Then load:
```bash
set -a; source .env; set +a
dotnet run --project src/NursingCareBackend.Api
```

**Option 3: Windows PowerShell**
```powershell
$env:DB_PASSWORD = "YourStrong!Passw0rd"
$env:JWT_KEY = "dev-secret-key"
dotnet run --project src/NursingCareBackend.Api
```

---

## Testing the API

### Method 1: Using Swagger UI (Easiest)

1. Run the API: `dotnet run --project src/NursingCareBackend.Api`
2. Open browser: <http://localhost:5050/swagger>
3. Click "Try it out" on any endpoint
4. For protected endpoints: Click **Authorize** and paste token

### Method 2: Using Postman

1. Import collection: **[Postman_Collection.json](Postman_Collection.json)**
2. Set variables: `BASE_URL`, `TOKEN`, `REQUEST_ID`
3. Execute requests in order

### Method 3: Using cURL

```bash
# Register
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Pass123!","confirmPassword":"Pass123!"}'

# Login
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Pass123!"}'

# Use token in protected requests
TOKEN="<token-from-login>"
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests
```

### Method 4: Using VS Code REST Client

Install "REST Client" extension, then create `.rest` file:

```rest
### Register User
POST http://localhost:5050/api/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Pass123!",
  "confirmPassword": "Pass123!"
}

### Login
POST http://localhost:5050/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Pass123!"
}
```

---

## Troubleshooting

### Problem: "Connection refused" on startup

**Cause:** SQL Server not running or not ready

**Solution:**
```bash
# Check Docker container
docker ps | grep nursingcare-sql

# Check logs
docker logs nursingcare-sql --tail 50

# Restart container
docker restart nursingcare-sql

# Wait 15 seconds and try again
```

### Problem: "Login failed for user 'sa'"

**Cause:** Wrong password in environment variable

**Solution:**
```bash
# Verify environment variable
echo $DB_PASSWORD

# Should match Docker password from run command
# If not, restart container with correct password:
docker rm nursingcare-sql
docker run -d --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=<your-password>" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

### Problem: Port 1433 already in use

**Solution:**
```bash
# Kill existing process
lsof -i :1433 | grep -v COMMAND | awk '{print $2}' | xargs kill -9

# Or stop Docker container
docker stop nursingcare-sql
docker rm nursingcare-sql
```

### Problem: 401 Unauthorized on protected endpoints

**Cause:** Missing or invalid token

**Solution:**
- Ensure token is in `Authorization` header
- Use format: `Authorization: Bearer <token>`
- Check token not expired (valid for 1 hour)
- Get new token by logging in again

### Problem: 403 Forbidden on care requests

**Cause:** User doesn't have required role

**Solution:**
- Current users get "User" role by default
- Care requests require "Nurse" or "Admin" role
- Manually update user role in database or modify AuthenticationService.cs

---

## Documentation Files

| File | Purpose | Read Time |
|------|---------|-----------|
| **QUICK_START.md** | Quick reference for running API | 5 min |
| **DOCKER_SETUP.md** | Complete Docker & SQL Server guide | 15 min |
| **src/RUN_AND_TEST_GUIDE.md** | Detailed setup & testing guide | 30 min |
| **TESTING_SCENARIOS.md** | Real-world test examples | 20 min |
| **Postman_Collection.json** | Pre-built API requests | - |
| **src/README.md** | Architecture documentation | 10 min |

---

## Next Steps

### For Development
1. Read [src/README.md](src/README.md) for architecture
2. Explore the code structure
3. Run unit tests: `dotnet test`

### For Testing
1. Start with [TESTING_SCENARIOS.md](TESTING_SCENARIOS.md)
2. Use [Postman_Collection.json](Postman_Collection.json) for quick testing
3. Try [QUICK_START.md](QUICK_START.md) workflow

### For Production
1. Change JWT_KEY to a strong secret
2. Use environment variables for all secrets
3. Configure HTTPS/SSL
4. Set up CI/CD pipeline
5. Configure proper database backups

---

## Key Features Implemented

###  Authentication
- User registration with validation
- Secure password hashing (PBKDF2-SHA256)
- JWT token generation (1 hour expiration)
- Login with email and password

###  Authorization
- Role-based access control (RBAC)
- Three roles: Admin, Nurse, User
- Protected endpoints requiring "Nurse" or "Admin"
- Token validation on protected routes

###  Care Requests
- Create new care requests
- List all care requests
- Retrieve specific care request
- Timestamps for all requests

###  Database
- SQL Server 2022 support
- Automatic migrations on startup
- Default roles seeded automatically
- Entity relationships and constraints

###  API
- RESTful design
- Swagger/OpenAPI documentation
- Comprehensive error handling
- CORS support
- Environment variable configuration

---

## Support

### Common Issues & Solutions

See [src/RUN_AND_TEST_GUIDE.md](src/RUN_AND_TEST_GUIDE.md#troubleshooting) for comprehensive troubleshooting.

### Documentation Links

- **JWT Tokens:** <https://jwt.io> (paste token to decode)
- **.NET Documentation:** <https://docs.microsoft.com/en-us/dotnet/>
- **Entity Framework:** <https://docs.microsoft.com/en-us/ef/core/>
- **Docker:** <https://docs.docker.com/>
- **SQL Server:** <https://docs.microsoft.com/en-us/sql/>

---

## Quick Reference

```bash
# Start SQL Server
docker run -d --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Set environment variables
export DB_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="dev-secret-key"

# Run API
dotnet run --project src/NursingCareBackend.Api

# Test API (in new terminal)
curl http://localhost:5050/health

# Register user
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Pass123!","confirmPassword":"Pass123!"}'

# View Swagger
# Open http://localhost:5050/swagger in browser

# Stop API
# Press Ctrl+C

# Stop SQL Server
docker stop nursingcare-sql
```

---

## Version Information

| Component | Version |
|-----------|---------|
| .NET SDK | 10.x |
| SQL Server | 2022 |
| Docker | Latest |
| Entity Framework | 10.x |
| ASP.NET Core | 10.x |

---

## Created: March 16, 2026

**Status:**  Ready for Development & Testing

**Last Updated:** 2026-03-16

**Maintained by:** Development Team

---

**Need more help?** Check the specific documentation files listed above or contact the development team.
