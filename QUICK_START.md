# Quick Start Guide - Nursing Care Backend API

**Quick reference for running and testing the API locally**

---

## 1. Start SQL Server (Docker)

```bash
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Wait 10-15 seconds for startup
sleep 15
```

---

## 2. Set Environment Variables

### macOS/Linux:
```bash
export DB_SERVER="localhost,1433"
export DB_NAME="NursingCareDb"
export DB_USER="sa"
export DB_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="dev-secret-key-change-in-production"
```

### Windows PowerShell:
```powershell
$env:DB_SERVER = "localhost,1433"
$env:DB_NAME = "NursingCareDb"
$env:DB_USER = "sa"
$env:DB_PASSWORD = "YourStrong!Passw0rd"
$env:JWT_KEY = "dev-secret-key-change-in-production"
```

---

## 3. Run the API

```bash
cd /Users/sterlingdiazd/Projects/NursingCareProject/NursingCareBackend
dotnet run --project src/NursingCareBackend.Api
```

**API URL:** http://localhost:5050

**Swagger:** http://localhost:5050/swagger

---

## 4. Test Endpoints

### Register User
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Pass123!",
    "confirmPassword": "Pass123!"
  }'
```

**Save the token from response**

### Login
```bash
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Pass123!"
  }'
```

### Create Care Request (Protected)
```bash
TOKEN="your-token-from-login"

curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Test request"
  }'
```

### Get Care Requests (Protected)
```bash
TOKEN="your-token-from-login"

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests
```

---

## 5. Testing Workflow

### Option A: Using Postman
1. Create new collection
2. Add requests from **Testing with Postman** section in `RUN_AND_TEST_GUIDE.md`
3. Set `Authorization` header with Bearer token from login

### Option B: Using Swagger UI
1. Open http://localhost:5050/swagger
2. Test `/api/auth/register` endpoint
3. Copy token from response
4. Click **Authorize** button and paste token
5. Test protected endpoints

### Option C: Using curl (See commands above)

---

## 6. Stop Services

```bash
# Stop API
Ctrl + C (in terminal running API)

# Stop SQL Server
docker stop nursingcare-sql

# Remove container (optional)
docker rm nursingcare-sql
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Port 1433 in use | `docker stop nursingcare-sql` or `kill -9 <PID>` |
| Connection refused | Wait 10-15 seconds for SQL Server to start |
| 401 Unauthorized | Add `Authorization: Bearer <token>` header |
| 403 Forbidden | User needs "Nurse" or "Admin" role |
| API won't start | Check `DB_PASSWORD` env var matches Docker password |

---

## Key Files

| File | Purpose |
|------|---------|
| `src/RUN_AND_TEST_GUIDE.md` | Comprehensive testing guide |
| `src/NursingCareBackend.Api/appsettings.json` | Configuration with env var placeholders |
| `src/NursingCareBackend.Api/Controllers/Auth/AuthController.cs` | Authentication endpoints |
| `src/NursingCareBackend.Api/Controllers/CareRequests/CareRequestsController.cs` | Care request endpoints |

---

## Default Credentials

| Variable | Default Value |
|----------|---------------|
| SQL Server Host | `localhost,1433` |
| Database Name | `NursingCareDb` |
| SQL User | `sa` |
| SQL Password | `YourStrong!Passw0rd` |
| JWT Key | `ChangeThisDevelopmentKeyToARealSecret` |

---

## Endpoints Summary

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| POST | `/api/auth/register` | No | Register new user |
| POST | `/api/auth/login` | No | Login & get token |
| POST | `/api/care-requests` | Yes | Create care request |
| GET | `/api/care-requests` | Yes | List care requests |
| GET | `/api/care-requests/{id}` | Yes | Get care request by ID |
| GET | `/health` | No | Health check |

---

**For detailed instructions, see `src/RUN_AND_TEST_GUIDE.md`**

**Last Updated:** 2026-03-16
