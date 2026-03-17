# Docker Setup Guide - SQL Server

Complete guide for setting up and managing SQL Server in Docker for local development.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Installation](#installation)
3. [Running SQL Server](#running-sql-server)
4. [Verification](#verification)
5. [Management](#management)
6. [Troubleshooting](#troubleshooting)

---

## Prerequisites

- **Docker Desktop** installed and running
  - macOS: https://docs.docker.com/desktop/install/mac-install/
  - Windows: https://docs.docker.com/desktop/install/windows-install/
  - Linux: https://docs.docker.com/engine/install/
- **Command line access** (Terminal, PowerShell, or Command Prompt)

Verify Docker is installed:

```bash
docker --version
docker ps
```

---

## Installation

### Step 1: Pull SQL Server Image

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
```

This downloads the official Microsoft SQL Server 2022 image. The download is ~1.5GB.

### Step 2: Create Container

**IMPORTANT:** Choose a strong password and remember it. This will be your SA account password.

**Choose your password and replace `YourStrong!Passw0rd` in the command below:**

#### macOS/Linux:

```bash
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

#### Windows (PowerShell):

```powershell
docker run -d `
  --name nursingcare-sql `
  -e "ACCEPT_EULA=Y" `
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" `
  -p 1433:1433 `
  mcr.microsoft.com/mssql/server:2022-latest
```

#### Windows (Command Prompt):

```cmd
docker run -d ^
  --name nursingcare-sql ^
  -e "ACCEPT_EULA=Y" ^
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" ^
  -p 1433:1433 ^
  mcr.microsoft.com/mssql/server:2022-latest
```

**Parameters explained:**

| Parameter | Description |
|-----------|-------------|
| `-d` | Run in detached mode (background) |
| `--name nursingcare-sql` | Container name (use this to reference the container) |
| `-e "ACCEPT_EULA=Y"` | Accept SQL Server EULA (required) |
| `-e "MSSQL_SA_PASSWORD=..."` | SA account password (min 8 chars, must include uppercase, lowercase, digit, special char) |
| `-p 1433:1433` | Map port 1433 (container) to 1433 (host machine) |
| `mcr.microsoft.com/mssql/server:2022-latest` | Image to use |

---

## Running SQL Server

### Start Container

**If container exists but is stopped:**

```bash
docker start nursingcare-sql
```

**If container doesn't exist:**

Follow the installation steps above.

### Wait for Startup

SQL Server takes 10-15 seconds to initialize. Check logs:

```bash
docker logs nursingcare-sql --tail 20
```

Look for this message:

```
SQL Server is now ready for client connections. This is an informational message; no user action is required.
```

---

## Verification

### Check Running Containers

```bash
docker ps
```

**Expected output:**

```
CONTAINER ID   IMAGE                                              COMMAND                  CREATED      STATUS      PORTS                    NAMES
abc123def456   mcr.microsoft.com/mssql/server:2022-latest        "/opt/mssql/bin/sqlse…" 5 minutes ago   Up 5 minutes   0.0.0.0:1433->1433/tcp   nursingcare-sql
```

### Test Connection

**Option 1: Using sqlcmd (if installed)**

```bash
sqlcmd -S localhost,1433 -U sa -P YourStrong!Passw0rd -Q "SELECT 1"
```

**Option 2: Using Docker exec**

```bash
docker exec nursingcare-sql /opt/mssql-tools/bin/sqlcmd \
  -S localhost \
  -U sa \
  -P YourStrong!Passw0rd \
  -Q "SELECT @@VERSION"
```

**Option 3: Using Azure Data Studio or SQL Server Management Studio**

- Host: `localhost,1433`
- User: `sa`
- Password: `YourStrong!Passw0rd`
- Trust Certificate: Yes

### View Container Logs

```bash
# Last 50 lines
docker logs nursingcare-sql --tail 50

# Follow logs in real-time
docker logs -f nursingcare-sql
```

---

## Management

### Stop Container

```bash
docker stop nursingcare-sql
```

### Restart Container

```bash
docker restart nursingcare-sql
```

### Remove Container

**Warning:** This deletes the container and all databases!

```bash
docker rm nursingcare-sql
```

### Remove Image

**Warning:** This deletes the SQL Server image!

```bash
docker rmi mcr.microsoft.com/mssql/server:2022-latest
```

### View Container Information

```bash
# Detailed container info
docker inspect nursingcare-sql

# Resource usage
docker stats nursingcare-sql
```

### Access SQL Server Shell

```bash
docker exec -it nursingcare-sql /opt/mssql-tools/bin/sqlcmd \
  -S localhost \
  -U sa \
  -P YourStrong!Passw0rd
```

Then run SQL commands:

```sql
-- View all databases
SELECT name FROM sys.databases;

-- View SQL Server version
SELECT @@VERSION;

-- Exit
EXIT
```

---

## Docker Compose (Optional)

For a more permanent setup, use Docker Compose:

Create `docker-compose.yml` in project root:

```yaml
version: '3.8'

services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: nursingcare-sql
    environment:
      ACCEPT_EULA: 'Y'
      MSSQL_SA_PASSWORD: 'YourStrong!Passw0rd'
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    networks:
      - nursing-care-network

volumes:
  sqlserver-data:

networks:
  nursing-care-network:
    driver: bridge
```

Then use:

```bash
# Start
docker-compose up -d

# Stop
docker-compose down

# View logs
docker-compose logs -f sql-server
```

---

## Environment Variables

### Set Password in Environment Variable

**macOS/Linux:**

```bash
export DB_PASSWORD="YourStrong!Passw0rd"
echo $DB_PASSWORD
```

**Windows PowerShell:**

```powershell
$env:DB_PASSWORD = "YourStrong!Passw0rd"
echo $env:DB_PASSWORD
```

**Windows Command Prompt:**

```cmd
set DB_PASSWORD=YourStrong!Passw0rd
echo %DB_PASSWORD%
```

### Create .env File

```bash
# .env (in project root, don't commit!)
DB_SERVER=localhost,1433
DB_NAME=NursingCareDb
DB_USER=sa
DB_PASSWORD=YourStrong!Passw0rd
JWT_KEY=dev-secret-key
```

Load before running app:

```bash
# macOS/Linux
set -a
source .env
set +a
dotnet run --project src/NursingCareBackend.Api

# Or in one line:
export $(grep -v '^#' .env | xargs) && dotnet run --project src/NursingCareBackend.Api
```

---

## Troubleshooting

### Port 1433 Already in Use

**macOS/Linux:**

```bash
# Find process using port 1433
lsof -i :1433

# Kill process by ID
kill -9 <PID>

# Or stop the Docker container
docker stop nursingcare-sql
```

**Windows PowerShell:**

```powershell
# Find process using port 1433
netstat -ano | findstr :1433

# Kill process by ID
taskkill /PID <PID> /F

# Or stop Docker container
docker stop nursingcare-sql
```

### Connection Refused

1. **Verify container is running:**

```bash
docker ps | grep nursingcare-sql
```

2. **Wait for SQL Server to start:**

```bash
# Watch logs
docker logs -f nursingcare-sql
```

3. **Check password matches:**

Ensure the password used in `docker run` command matches the password in your environment variable or connection string.

### Login Failed for User 'sa'

**Cause:** Wrong password

**Solution:**

1. Stop container: `docker stop nursingcare-sql`
2. Remove container: `docker rm nursingcare-sql`
3. Create new container with correct password
4. Update environment variables

### Out of Memory / Slow Performance

**Check resource allocation:**

```bash
docker stats nursingcare-sql
```

**Increase Docker resources:**

1. Open Docker Desktop
2. Go to Settings → Resources
3. Increase Memory and CPU
4. Restart Docker

### Database Won't Connect After Restart

**Cause:** Password expired or configuration changed

**Solution:**

```bash
# Stop and remove container
docker stop nursingcare-sql
docker rm nursingcare-sql

# Verify no process on 1433
lsof -i :1433

# Create new container
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

---

## Data Persistence

By default, Docker containers are ephemeral (data is lost when container is removed).

### Option 1: Named Volume (Recommended)

```bash
# Create volume
docker volume create sqlserver-data

# Use in container
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  -v sqlserver-data:/var/opt/mssql \
  mcr.microsoft.com/mssql/server:2022-latest
```

### Option 2: Bind Mount

```bash
# Use host directory
docker run -d \
  --name nursingcare-sql \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 \
  -v /path/to/sqldata:/var/opt/mssql \
  mcr.microsoft.com/mssql/server:2022-latest
```

---

## Performance Tips

### 1. Allocate Sufficient Resources

SQL Server requires:
- **Minimum:** 2 CPU cores, 2GB RAM
- **Recommended:** 4 CPU cores, 4GB RAM

### 2. Monitor Container Performance

```bash
docker stats nursingcare-sql
```

### 3. Optimize SQL Queries

Use the API's query performance monitoring.

### 4. Use Connection Pooling

The .NET driver automatically uses connection pooling.

---

## Quick Reference

| Task | Command |
|------|---------|
| Start SQL Server | `docker run -d --name nursingcare-sql -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest` |
| Check status | `docker ps \| grep nursingcare-sql` |
| View logs | `docker logs -f nursingcare-sql` |
| Test connection | `docker exec nursingcare-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P <password> -Q "SELECT 1"` |
| Stop container | `docker stop nursingcare-sql` |
| Start container | `docker start nursingcare-sql` |
| Remove container | `docker rm nursingcare-sql` |
| Remove image | `docker rmi mcr.microsoft.com/mssql/server:2022-latest` |

---

**Last Updated:** 2026-03-16

**SQL Server Version:** 2022

**Docker Image:** mcr.microsoft.com/mssql/server:2022-latest
