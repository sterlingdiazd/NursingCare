# Testing Scenarios - Complete Workflow Examples

Real-world test scenarios for the Nursing Care Backend API.

---

## Table of Contents

1. [Scenario 1: Basic User Registration & Login](#scenario-1-basic-user-registration--login)
2. [Scenario 2: Creating Care Requests](#scenario-2-creating-care-requests)
3. [Scenario 3: Authorization & Security](#scenario-3-authorization--security)
4. [Scenario 4: Error Handling](#scenario-4-error-handling)
5. [Scenario 5: Complete Workflow](#scenario-5-complete-workflow)
6. [Scenario 6: Token Expiration](#scenario-6-token-expiration)

---

## Scenario 1: Basic User Registration & Login

### Objective
Register a new user and login to get a JWT token.

### Steps

#### Step 1: Register a New User

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "SecurePass123!"
  }'
```

**Expected Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI5Y2Q5N2M3ZC1hYzNkLTRiZWYtODc0Yi0wM2M1YjY1ODU0YzEiLCJlbWFpbCI6ImpvaG4uZG9lQGV4YW1wbGUuY29tIiwicm9sZSI6IlVzZXIiLCJpYXQiOjE3MTAyNDU0MzAsImV4cCI6MTcxMDI0OTAzMH0.abc...",
  "email": "john.doe@example.com",
  "roles": ["User"]
}
```

**Save the token** for use in protected endpoints.

#### Step 2: Login with Same Credentials

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePass123!"
  }'
```

**Expected Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "john.doe@example.com",
  "roles": ["User"]
}
```

**Note:** Token will be different each time due to new `iat` (issued at) timestamp.

#### Step 3: Verify Tokens are Different

Tokens obtained from registration and login endpoints should be different because they have different timestamps.

#### Step 4: Decode and Inspect Token

Go to https://jwt.io and paste your token:

**Expected Payload:**
```json
{
  "sub": "9cd97c7d-ac3d-4bef-874b-03c5b658542c",
  "email": "john.doe@example.com",
  "role": "User",
  "iat": 1710245430,
  "exp": 1710249030
}
```

**Explanation:**
- `sub`: User ID (unique identifier)
- `email`: User's email address
- `role`: User's role
- `iat`: Issued at timestamp (seconds since epoch)
- `exp`: Expiration timestamp (1 hour = 3600 seconds after iat)

---

## Scenario 2: Creating Care Requests

### Objective
Create multiple care requests and verify they're stored correctly.

### Prerequisites
- Registered user with valid token
- Token not expired

### Steps

#### Step 1: Create First Care Request

**Request:**
```bash
TOKEN="your-token-from-login"

curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Patient needs assistance with morning hygiene routine."
  }'
```

**Expected Response (201 Created):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440101"
}
```

**Save the ID** for later queries.

#### Step 2: Create Second Care Request

**Request:**
```bash
curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "22222222-2222-2222-2222-222222222222",
    "description": "Medication administration at 2 PM."
  }'
```

**Expected Response (201 Created):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440102"
}
```

#### Step 3: List All Care Requests

**Request:**
```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests
```

**Expected Response (200 OK):**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440101",
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Patient needs assistance with morning hygiene routine.",
    "status": "Pending",
    "createdAtUtc": "2026-03-16T22:55:00Z"
  },
  {
    "id": "550e8400-e29b-41d4-a716-446655440102",
    "residentId": "22222222-2222-2222-2222-222222222222",
    "description": "Medication administration at 2 PM.",
    "status": "Pending",
    "createdAtUtc": "2026-03-16T22:55:05Z"
  }
]
```

#### Step 4: Get Specific Care Request

**Request:**
```bash
REQUEST_ID="550e8400-e29b-41d4-a716-446655440101"

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests/$REQUEST_ID
```

**Expected Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440101",
  "residentId": "11111111-1111-1111-1111-111111111111",
  "description": "Patient needs assistance with morning hygiene routine.",
  "status": "Pending",
  "createdAtUtc": "2026-03-16T22:55:00Z"
}
```

---

## Scenario 3: Authorization & Security

### Objective
Verify that authorization works correctly and security is enforced.

### Prerequisites
- Multiple registered users (or at least one)
- Valid tokens from login

### Step 1: Access Protected Endpoint Without Token

**Request:**
```bash
curl -X GET http://localhost:5050/api/care-requests
```

**Expected Response (401 Unauthorized):**
```json
{
  "title": "Unauthorized",
  "status": 401,
  "detail": "No authorization token provided"
}
```

### Step 2: Access Protected Endpoint with Invalid Token

**Request:**
```bash
curl -X GET http://localhost:5050/api/care-requests \
  -H "Authorization: Bearer invalid-token-xyz"
```

**Expected Response (401 Unauthorized):**
```json
{
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid token"
}
```

### Step 3: Access Protected Endpoint with Expired Token

**Request (with a token that's over 1 hour old):**
```bash
curl -X GET http://localhost:5050/api/care-requests \
  -H "Authorization: Bearer <old-token>"
```

**Expected Response (401 Unauthorized):**
```json
{
  "title": "Unauthorized",
  "status": 401,
  "detail": "Token expired"
}
```

### Step 4: Access Protected Endpoint with Valid Token

**Request:**
```bash
TOKEN="valid-token-from-login"

curl -X GET http://localhost:5050/api/care-requests \
  -H "Authorization: Bearer $TOKEN"
```

**Expected Response (200 OK):**
Returns list of care requests (may be empty)

### Step 5: Test Role-Based Access (Requires "Nurse" or "Admin" Role)

**Note:** Default role assignment is "User". To test role-based access:

1. Manually update user role in database:

```sql
USE NursingCareDb;

-- Find user ID
SELECT Id, Email FROM Users WHERE Email = 'john.doe@example.com';

-- Get Nurse role ID
SELECT Id FROM Roles WHERE Name = 'Nurse';

-- Insert user-role mapping
INSERT INTO UserRoles (UserId, RoleId)
VALUES ('your-user-id', 'nurse-role-id');
```

2. Re-login to get new token with "Nurse" role

3. Create care request should now succeed

---

## Scenario 4: Error Handling

### Objective
Test error handling and validation.

### Step 1: Registration - Missing Required Field

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Pass123!"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "confirmPassword": ["The confirmPassword field is required."]
  }
}
```

### Step 2: Registration - Password Too Short

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Pass1",
    "confirmPassword": "Pass1"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "Password must be at least 6 characters long."
}
```

### Step 3: Registration - Passwords Don't Match

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "DifferentPass123!"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "Passwords do not match."
}
```

### Step 4: Registration - Duplicate Email

**Request (after already registering with this email):**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "SecurePass123!"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "User with this email already exists."
}
```

### Step 5: Login - Invalid Credentials

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "WrongPassword123!"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid email or password."
}
```

### Step 6: Create Care Request - Invalid Request Body

**Request:**
```bash
TOKEN="valid-token"

curl -X POST http://localhost:5050/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "description": "Missing resident ID"
  }'
```

**Expected Response (400 Bad Request):**
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "residentId": ["The residentId field is required."]
  }
}
```

### Step 7: Get Non-Existent Care Request

**Request:**
```bash
TOKEN="valid-token"
FAKE_ID="00000000-0000-0000-0000-000000000000"

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5050/api/care-requests/$FAKE_ID
```

**Expected Response (404 Not Found):**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Care request not found."
}
```

---

## Scenario 5: Complete Workflow

### Objective
Execute a complete end-to-end workflow.

### Complete Script

```bash
#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

BASE_URL="http://localhost:5050"
EMAIL="workflow-test-$(date +%s)@example.com"
PASSWORD="SecurePass123!"

echo -e "${BLUE}=== Nursing Care API - Complete Workflow ===${NC}\n"

# Step 1: Register User
echo -e "${BLUE}1. Registering user...${NC}"
REGISTER_RESPONSE=$(curl -s -X POST $BASE_URL/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"$EMAIL\",
    \"password\": \"$PASSWORD\",
    \"confirmPassword\": \"$PASSWORD\"
  }")

TOKEN=$(echo $REGISTER_RESPONSE | jq -r '.token')
echo -e "${GREEN}✓ User registered${NC}"
echo "Email: $EMAIL"
echo "Token: ${TOKEN:0:50}..."

# Step 2: Create Care Request 1
echo -e "\n${BLUE}2. Creating care request 1...${NC}"
CREATE_RESPONSE_1=$(curl -s -X POST $BASE_URL/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "11111111-1111-1111-1111-111111111111",
    "description": "Morning hygiene assistance needed"
  }')

REQUEST_ID_1=$(echo $CREATE_RESPONSE_1 | jq -r '.id')
echo -e "${GREEN}✓ Care request created${NC}"
echo "ID: $REQUEST_ID_1"

# Step 3: Create Care Request 2
echo -e "\n${BLUE}3. Creating care request 2...${NC}"
CREATE_RESPONSE_2=$(curl -s -X POST $BASE_URL/api/care-requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "residentId": "22222222-2222-2222-2222-222222222222",
    "description": "Medication reminder at 2 PM"
  }')

REQUEST_ID_2=$(echo $CREATE_RESPONSE_2 | jq -r '.id')
echo -e "${GREEN}✓ Care request created${NC}"
echo "ID: $REQUEST_ID_2"

# Step 4: Get All Care Requests
echo -e "\n${BLUE}4. Getting all care requests...${NC}"
LIST_RESPONSE=$(curl -s -H "Authorization: Bearer $TOKEN" \
  $BASE_URL/api/care-requests)

COUNT=$(echo $LIST_RESPONSE | jq 'length')
echo -e "${GREEN}✓ Retrieved $COUNT care requests${NC}"

# Step 5: Get Specific Care Request
echo -e "\n${BLUE}5. Getting specific care request...${NC}"
GET_RESPONSE=$(curl -s -H "Authorization: Bearer $TOKEN" \
  $BASE_URL/api/care-requests/$REQUEST_ID_1)

DESCRIPTION=$(echo $GET_RESPONSE | jq -r '.description')
echo -e "${GREEN}✓ Retrieved care request${NC}"
echo "Description: $DESCRIPTION"

# Step 6: Health Check
echo -e "\n${BLUE}6. Checking API health...${NC}"
HEALTH=$(curl -s $BASE_URL/health)
echo -e "${GREEN}✓ API is healthy${NC}"

echo -e "\n${GREEN}=== Workflow Complete ===${NC}"
```

**To run:**

```bash
chmod +x test-workflow.sh
./test-workflow.sh
```

---

## Scenario 6: Token Expiration

### Objective
Understand token expiration and renewal process.

### Steps

#### Step 1: Get Current Time

**Request:**
```bash
date +%s
```

**Note the current timestamp (seconds since epoch)**

#### Step 2: Register and Get Token

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "expiration-test@example.com",
    "password": "SecurePass123!",
    "confirmPassword": "SecurePass123!"
  }'
```

**Save the token and current time**

#### Step 3: Decode Token to See Expiration

Go to https://jwt.io and paste your token.

**Look for the `exp` claim (expiration time in seconds since epoch)**

**Example:**
```json
{
  "exp": 1710249030  // This is 3600 seconds (1 hour) after iat
}
```

#### Step 4: Calculate Expiration Time

```bash
# Linux/macOS
date -r 1710249030

# Or convert to human readable
date -d @1710249030
```

#### Step 5: Wait for Expiration (Optional)

1. Wait until expiration time
2. Try to use token: `curl -H "Authorization: Bearer <token>" http://localhost:5050/api/care-requests`
3. Expect 401 Unauthorized

#### Step 6: Renew Token by Logging In

**Request:**
```bash
curl -X POST http://localhost:5050/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "expiration-test@example.com",
    "password": "SecurePass123!"
  }'
```

**Response:** New token with new expiration time

---

## Summary of Test Cases

| Scenario | Endpoint | Method | Auth | Status | Notes |
|----------|----------|--------|------|--------|-------|
| Register user | `/api/auth/register` | POST | No | 200 | Returns token |
| Login | `/api/auth/login` | POST | No | 200 | Returns token |
| Create request | `/api/care-requests` | POST | Yes | 201 | Requires token |
| List requests | `/api/care-requests` | GET | Yes | 200 | Requires token |
| Get request | `/api/care-requests/{id}` | GET | Yes | 200 | Requires token |
| No token | `/api/care-requests` | GET | No | 401 | Unauthorized |
| Invalid token | `/api/care-requests` | GET | Invalid | 401 | Unauthorized |
| Expired token | `/api/care-requests` | GET | Expired | 401 | Unauthorized |

---

**Last Updated:** 2026-03-16

**Recommended Tools:**
- cURL (command line)
- Postman (desktop/web)
- Insomnia (alternative to Postman)
- VS Code REST Client extension
- Swagger UI (built-in)
