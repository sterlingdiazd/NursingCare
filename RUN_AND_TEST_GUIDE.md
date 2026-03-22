# Run and Test Guide

This guide explains how to run and test the Nursing Care Backend API locally with SQL Server in Docker, and validate endpoints using Postman and Swagger.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Variables Setup](#environment-variables-setup)
3. [Start SQL Server (Docker)](#start-sql-server-docker)
4. [Run the API Locally](#run-the-api-locally)
5. [Run the Apps Remotely](#run-the-apps-remotely)
6. [Testing the Endpoints](#testing-the-endpoints)
7. [Troubleshooting](#troubleshooting)
8. [Stop Services](#stop-services)

---

## Prerequisites

- **.NET SDK 10.x** - Check version: `dotnet --version`
- **Docker Desktop** - Running and accessible
- **Postman** - For API testing (optional, can also use curl or Swagger UI)
- **curl** - For command-line testing

---

## Environment Variables Setup

The application uses environment variables for all sensitive configuration. Set these before running the API:

### Option 1: Set Environment Variables in Terminal

**On macOS/Linux:**

```bash
# Database Configuration
export DB_SERVER="localhost,1433"
export DB_NAME="NursingCareDb"
export DB_USER="sa"
export DB_PASSWORD="YourStrong!Passw0rd"

# JWT Configuration
export JWT_KEY="your-super-secret-key-change-in-production"

# Then run the app
dotnet run --project src/NursingCareBackend.Api
```

**On Windows (PowerShell):**

```powershell
# Database Configuration
$env:DB_SERVER = "localhost,1433"
$env:DB_NAME = "NursingCareDb"
$env:DB_USER = "sa"
$env:DB_PASSWORD = "YourStrong!Passw0rd"

# JWT Configuration
$env:JWT_KEY = "your-super-secret-key-change-in-production"

# Then run the app
dotnet run --project src/NursingCareBackend.Api
```

**On Windows (Command Prompt):**

```cmd
set DB_SERVER=localhost,1433
set DB_NAME=NursingCareDb
set DB_USER=sa
set DB_PASSWORD=YourStrong!Passw0rd
set JWT_KEY=your-super-secret-key-change-in-production

dotnet run --project src/NursingCareBackend.Api
```

### Option 2: Create a `.env` File (Recommended for Development)

Create a `.env` file in the project root (not committed to git):

```bash
# .env
DB_SERVER=localhost,1433
DB_NAME=NursingCareDb
DB_USER=sa
DB_PASSWORD=YourStrong!Passw0rd
JWT_KEY=your-super-secret-key-change-in-production
```

Then load it before running:

```bash
# macOS/Linux
set -a
source .env
set +a
dotnet run --project src/NursingCareBackend.Api

# Or use a tool like direnv
```

### Option 3: Using `.env.local` with launchSettings.json

The `launchSettings.json` already supports environment variable loading. Set variables in your system environment.

### Default Values

If environment variables are not set, the app uses these defaults:

| Variable | Default |
|----------|---------|
| `DB_SERVER` | `localhost,1433` |
| `DB_NAME` | `NursingCareDb` |
| `DB_USER` | `sa` |
| `DB_PASSWORD` | `YourStrong!Passw0rd` |
| `JWT_KEY` | `ChangeThisDevelopmentKeyToARealSecret` |

---

## Start SQL Server (Docker)

### Step 1: Create and Run the SQL Server Container

**Important:** Use the same password you set in the `DB_PASSWORD` environment variable.

```bash
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

**Note:** Replace `YourStrong!Passw0rd` with your chosen password. This must match the `DB_PASSWORD` environment variable.

### Step 2: Wait for SQL Server to Start

SQL Server takes 10-15 seconds to initialize. Check the logs:

```bash
docker logs nursingcare-sql --tail 20
```

Wait for a message like:
```
SQL Server is now ready for client connections. This is an informational message; no user action is required.
```

### Step 3: Verify Connection

Test the connection:

```bash
# Using sqlcmd (if installed)
sqlcmd -S localhost,1433 -U sa -P YourStrong!Passw0rd -Q "SELECT 1"

# Or using a simple curl check (optional)
docker exec nursingcare-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -Q "SELECT 1"
```

### Step 4: Check Docker Status

View running containers:

```bash
docker ps
```

Stop the container later:

```bash
docker stop nursingcare-sql
```

Restart it:

```bash
docker start nursingcare-sql
```

Remove it completely:

```bash
docker rm nursingcare-sql
```

---

## Run the API Locally

### Step 1: Set Environment Variables

Follow the [Environment Variables Setup](#environment-variables-setup) section above.

### Step 2: Restore Dependencies

```bash
cd /Users/sterlingdiazd/Projects/NursingCareProject/NursingCareBackend
dotnet restore
```

### Step 3: Run the Application

```bash
dotnet run --project src/NursingCareBackend.Api --launch-profile http
```

### Expected Output

```
Using launch settings from src/NursingCareBackend.Api/Properties/launchSettings.json...
Building...
info: Startup[0]
      Database connection ready. HasUnresolvedPlaceholder=False
info: Microsoft.EntityFrameworkCore.Migrations[20411]
      Applying migration '20260316225309_SeedDefaultRoles'.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand - INSERT INTO Roles...
Database created and migrations applied successfully.
info: Microsoft.AspNetCore.Hosting.Hosting[14]
      Now listening on: http://localhost:5050
```

### Step 4: Verify the API is Running

Open a new terminal and run:

```bash
curl http://localhost:5050/health
```

Expected response: `200 OK`

---

## Run the Apps Remotely

Use these production links when you want to validate the deployed Azure environment instead of your local machine.

### Production URLs

- **Backend health:** [https://nursingcarebackend-f9f7h3gafhg2gjc9.centralus-01.azurewebsites.net/api/health](https://nursingcarebackend-f9f7h3gafhg2gjc9.centralus-01.azurewebsites.net/api/health)
- **Web app:** [https://witty-forest-00b6eb010.2.azurestaticapps.net](https://witty-forest-00b6eb010.2.azurestaticapps.net)
- **Mobile web app:** [https://thankful-forest-0e5e45410.2.azurestaticapps.net](https://thankful-forest-0e5e45410.2.azurestaticapps.net)

### Quick Remote Checks

Verify the backend health endpoint:

```bash
curl https://nursingcarebackend-f9f7h3gafhg2gjc9.centralus-01.azurewebsites.net/api/health
```

Open the frontend apps in your browser:

```text
https://witty-forest-00b6eb010.2.azurestaticapps.net
https://thankful-forest-0e5e45410.2.azurestaticapps.net
```

If login or data loading fails remotely, confirm that:

- the latest push to `main` finished successfully in GitHub Actions
- the backend health URL returns a healthy response
- you sign out and sign back in after role or auth changes

---

## Testing the Endpoints

The API provides authentication and care request management endpoints.

### API Base URLs

- **Local:** [http://localhost:5050](http://localhost:5050)
- **Swagger UI:** [http://localhost:5050/swagger](http://localhost:5050/swagger)
- **Health Check:** [http://localhost:5050/health](http://localhost:5050/health)

### 1. Authentication Endpoints

#### 1.1 Register a New User

**Endpoint:** `POST /api/auth/register`

**Request:**

```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "nurse1@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "SecurePass123!"
  }'
```

**Expected Response (201 Created):**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJudXJzZTEiLCJlbWFpbCI6Im51cnNlMUBleGFtcGxlLmNvbSIsImlhdCI6MTY0NzQ3OTU4MCwiZXhwIjoxNjQ3NDgzMTgwfQ.abcdef...",
  "email": "nurse1@example.com",
  "roles": ["User"]
}
```

**Success Criteria:**
- Status code: `200 OK`
- Response contains JWT token
- Email is returned
- Roles list includes "User" (default role)

**Error Cases:**
- `400 Bad Request` - Invalid email format, password too short, or passwords don't match
- `400 Bad Request` - User with email already exists

#### 1.2 Login

**Endpoint:** `POST /api/auth/login`

**Request:**

```bash
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "nurse1@example.com",
    "password": "SecurePass123!"
  }'
```

**Expected Response (200 OK):**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "nurse1@example.com",
  "roles": ["User"]
}
```

**Success Criteria:**
- Status code: `200 OK`
- Response contains JWT token
- Token is valid for protected endpoints

**Error Cases:**
- `400 Bad Request` - Invalid email or password
- `400 Bad Request` - User account is inactive

### 2. Care Request Endpoints (Protected)

These endpoints require JWT authentication. Use the token from login/register.

#### 2.1 Create Care Request

**Endpoint:** `POST /api/care-requests`

**Authorization Required:** Yes (Bearer token)

**Request:**

```bash
# Save the token from login/register
TOKEN="your-jwt-token-from-login-response"

curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Patient needs assistance with medication administration."
  }'
```

**Expected Response (201 Created):**

```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

**Success Criteria:**
- Status code: `201 Created`
- Response contains generated ID

**Error Cases:**
- `401 Unauthorized` - No token provided or invalid token
- `403 Forbidden` - User doesn't have required role (Nurse or Admin)
- `400 Bad Request` - Missing or invalid request fields

#### 2.2 Get All Care Requests

**Endpoint:** `GET /api/care-requests`

**Authorization Required:** Yes (Bearer token)

**Request:**

```bash
TOKEN="your-jwt-token-from-login-response"

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests
```

**Expected Response (200 OK):**

```json
[
  {
    "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Patient needs assistance with medication administration.",
    "status": "Pending",
    "createdAtUtc": "2026-03-16T22:55:00Z"
  }
]
```

#### 2.3 Get Care Request by ID

**Endpoint:** `GET /api/care-requests/{id}`

**Authorization Required:** Yes (Bearer token)

**Request:**

```bash
TOKEN="your-jwt-token-from-login-response"
REQUEST_ID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests/$REQUEST_ID
```

**Expected Response (200 OK):**

```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "residentId": "11111111-1111-1111-1111-111111111111",
  "description": "Patient needs assistance with medication administration.",
  "status": "Pending",
  "createdAtUtc": "2026-03-16T22:55:00Z"
}
```

#### 2.4 Health Check

**Endpoint:** `GET /health`

**Authorization Required:** No

**Request:**

```bash
curl http://localhost:5050/health
```

**Expected Response:**
- `200 OK` - Database is accessible and running normally
- `503 Service Unavailable` - Database connection failed

---

## Testing with Postman

### Step 1: Import Collection

Create a new Postman collection with these requests:

### Request 1: Register User

```
Method: POST
URL: http://localhost:5050/api/auth/register
Headers:
  Content-Type: application/json
Body (raw):
{
  "email": "nurse1@example.com",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!"
}
```

**Save the token from the response** - you'll need it for protected endpoints.

### Request 2: Login

```
Method: POST
URL: http://localhost:5050/api/auth/login
Headers:
  Content-Type: application/json
Body (raw):
{
  "email": "nurse1@example.com",
  "password": "SecurePass123!"
}
```

### Request 3: Create Care Request

```
Method: POST
URL: http://localhost:5050/api/care-requests
Headers:
  Content-Type: application/json
  Authorization: Bearer {{TOKEN}}
Body (raw):
{
  "residentId": "11111111-1111-1111-1111-111111111111",
  "description": "Patient needs assistance with medication administration."
}
```

**Note:** Replace `{{TOKEN}}` with the actual token from login response.

### Request 4: Get All Care Requests

```
Method: GET
URL: http://localhost:5050/api/care-requests
Headers:
  Authorization: Bearer {{TOKEN}}
```

### Request 5: Get Care Request by ID

```
Method: GET
URL: http://localhost:5050/api/care-requests/{{REQUEST_ID}}
Headers:
  Authorization: Bearer {{TOKEN}}
```

### Using Postman Variables

To make testing easier, set these variables in Postman:

1. Open **Postman** → Click **Environment** (gear icon)
2. Create a new environment named "Nursing Care - Local"
3. Add variables:

| Variable | Initial Value | Current Value |
|----------|---------------|---------------|
| `BASE_URL` | [http://localhost:5050](http://localhost:5050) | [http://localhost:5050](http://localhost:5050) |
| `TOKEN` | `` | (set after login/register) |
| `REQUEST_ID` | `` | (set after create request) |

4. In requests, use `{{BASE_URL}}`, `{{TOKEN}}`, `{{REQUEST_ID}}`

---

## Testing with Swagger UI

The API includes Swagger documentation for easier exploration.

### Step 1: Open Swagger UI

Navigate to: **<http://localhost:5050/swagger**>

### Step 2: Test Registration

1. Find `POST /api/auth/register`
2. Click "Try it out"
3. Enter example data:
   ```json
   {
     "email": "nurse1@example.com",
     "password": "SecurePass123!",
     "confirmPassword": "SecurePass123!"
   }
   ```
4. Click "Execute"
5. Copy the token from the response

### Step 3: Authorize for Protected Endpoints

1. Click the green **"Authorize"** button at the top
2. Paste the token (without "Bearer" prefix) in the "Value" field
3. Click "Authorize"

### Step 4: Test Protected Endpoints

Now you can test:
- `POST /api/care-requests` - Create care request
- `GET /api/care-requests` - List all care requests
- `GET /api/care-requests/{id}` - Get specific care request

---

## Complete Testing Workflow

### Quick Start (5 minutes)

```bash
# Terminal 1: Start SQL Server
docker run -d --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Wait 10-15 seconds for SQL Server to start
sleep 15

# Terminal 2: Set environment variables and start API
export DB_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="dev-secret-key"

cd /Users/sterlingdiazd/Projects/NursingCareProject/NursingCareBackend
dotnet run --project src/NursingCareBackend.Api

# Terminal 3: Test the endpoints
# Register
REGISTER_RESPONSE=$(curl -s -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Pass123!","confirmPassword":"Pass123!"}')

echo "Registration Response:"
echo $REGISTER_RESPONSE | jq .

# Extract token (requires jq)
TOKEN=$(echo $REGISTER_RESPONSE | jq -r '.token')

# Create care request
curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"residentId":"11111111-1111-1111-1111-111111111111","description":"Test care request"}'
```

---

## Troubleshooting

### SQL Server Issues

#### Port 1433 Already in Use

```bash
# Find process using port 1433
lsof -i :1433

# Stop the conflicting container
docker stop nursingcare-sql

# Or kill the process directly
kill -9 <PID>
```

#### Connection Refused

```bash
# Check if Docker container is running
docker ps

# Check container logs
docker logs nursingcare-sql --tail 50

# Ensure password matches your environment variable
echo $DB_PASSWORD
```

#### Login Failed for User 'sa'

Ensure the password in your environment variable matches the one used when creating the container:

```bash
# In Docker command:
-e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd"

# In environment variable:
export DB_PASSWORD="YourStrong!Passw0rd"
```

### API Issues

#### API Won't Start

```bash
# Check if port 5050 is in use
lsof -i :5050

# Kill existing process if needed
kill -9 <PID>

# Ensure environment variables are set
env | grep -E "DB_|JWT_"
```

#### 401 Unauthorized on Protected Endpoints

- Ensure token is included in Authorization header: `Authorization: Bearer <token>`
- Verify token is not expired (tokens expire after 1 hour)
- Token must include "Bearer " prefix in curl requests

#### 403 Forbidden on Care Requests

User must have "Nurse" or "Admin" role. Current implementation assigns "User" role by default.

To test with proper role:
1. Manually update user role in database
2. Or modify seeding to assign "Nurse" role by default (see `src/NursingCareBackend.Application/Identity/Services/AuthenticationService.cs`)

#### 500 Internal Server Error

Check API logs for detailed error message. Common causes:
- Database connection failed
- Migration failed to apply
- Missing or invalid JWT key

```bash
# Restart with verbose logging
LOGGING__LOGLEVEL__DEFAULT=Debug dotnet run --project src/NursingCareBackend.Api
```

---

## Stop Services

### Stop API

In the terminal running the API, press:

```
Ctrl + C
```

### Stop SQL Server Container

```bash
docker stop nursingcare-sql
```

### Remove SQL Server Container (Optional)

```bash
docker rm nursingcare-sql
```

---

## Additional Resources

### Database Management

To connect directly to SQL Server:

```bash
# Using Docker exec
docker exec -it nursingcare-sql /opt/mssql-tools/bin/sqlcmd \
  -S localhost \
  -U sa \
  -P YourStrong!Passw0rd

# View database
USE NursingCareDb;
SELECT * FROM Roles;
SELECT * FROM Users;
SELECT * FROM CareRequests;
```

### JWT Token Inspection

Decode and inspect JWT tokens at: **<https://jwt.io**>

Paste your token to see:
- Subject (sub)
- Email
- Roles
- Expiration (exp)

### Environment Variable Reference

All placeholders that can be replaced with environment variables:

```
Appsettings.json placeholders:
{DB_SERVER}     → Environment variable: DB_SERVER
{DB_NAME}       → Environment variable: DB_NAME
{DB_USER}       → Environment variable: DB_USER
{DB_PASSWORD}   → Environment variable: DB_PASSWORD
{JWT_KEY}       → Environment variable: JWT_KEY
```

---

## Summary

| Task | Command |
|------|---------|
| Start SQL Server | `docker run -d --name nursingcare-sql -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest` |
| Set environment variables | `export DB_PASSWORD="YourStrong!Passw0rd" && export JWT_KEY="dev-key"` |
| Run API | `dotnet run --project src/NursingCareBackend.Api` |
| Test registration | `curl -X POST <http://localhost:5050/api/auth/register> ...` |
| Test protected endpoint | `curl -H "Authorization: Bearer $TOKEN" <http://localhost:5050/api/care-requests>` |
| View Swagger | Open <http://localhost:5050/swagger> in browser |
| Stop API | Press Ctrl+C |
| Stop SQL Server | `docker stop nursingcare-sql` |

---

**Last Updated:** 2026-03-16

**API Version:** 1.0

**Status:** Ready for testing
