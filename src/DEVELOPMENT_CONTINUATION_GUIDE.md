# NursingCareProject: Development Continuation Guide

Last reviewed: 2026-03-05

This guide covers the three active projects in `NursingCareProject`:

- Backend API: `NursingCareBackend`
- Web app: `NursingCareWeb/nursing_care_web_react`
- Mobile app: `NursingCareMobile/nursing-care-mobile`

## 1. Current State Summary

### Backend API (`NursingCareBackend`)

- Layered architecture is in place (`Api`, `Application`, `Domain`, `Infrastructure`).
- Implemented endpoint: `POST /api/care-requests`.
- Health endpoint exists: `GET /api/health`.
- EF Core migration exists and startup auto-migrates DB.
- Docker support exists (`Dockerfile`, `docker-compose.yml`).

### Web App (`NursingCareWeb/nursing_care_web_react`)

- Basic form to create care requests is implemented.
- API call exists and can post to backend.
- Project has both Axios client setup and direct `fetch` usage.

### Mobile App (`NursingCareMobile/nursing-care-mobile`)

- Create Care Request screen exists with required-field validation.
- Service layer exists for `POST /api/care-requests`.
- Submission flow is not wired yet (currently logs to console).

## 2. Critical Gaps to Fix First

1. Web config/runtime mismatch:
- CRA (`react-scripts`) project uses Vite-style config (`import.meta.env`, `vite/client` type, `env.local` naming). This is unstable and can break builds.

2. Mobile API connectivity mismatch:
- Mobile API base URL is `http://localhost:5000`, while backend currently runs on `http://localhost:5050`.
- For real devices, `localhost` will not reach your machine backend.

3. Backend environment config inconsistency:
- `appsettings.Development.json` and `appsettings.Docker.json` place `ConnectionStrings` (and `Cors` in Docker) under `Logging`, which is incorrect structure.

4. End-to-end contract maturity:
- Only create endpoint exists. No read/list/update workflows yet, limiting web/mobile feature completion.

## 3. Project-Specific Fix Plan

### Backend

1. Normalize environment configuration files:
- Move `ConnectionStrings` and `Cors` out of `Logging` in:
  - `src/NursingCareBackend.Api/appsettings.Development.json`
  - `src/NursingCareBackend.Api/appsettings.Docker.json`

2. Secure secrets:
- Remove plaintext SQL password from tracked files.
- Use environment variables or secret manager.

3. Expand API for product flow:
- Add `GET /api/care-requests` (list/filter).
- Add `GET /api/care-requests/{id}` (details).
- Add status transition endpoint(s) if required by workflow.

4. Strengthen API validation and error contract:
- Validate request DTO shape before domain creation.
- Return consistent problem details for bad input.

5. Improve tests:
- Keep integration tests but isolate DB dependencies where possible.
- Add API validation/error-path tests.

### Web

1. Choose one toolchain and align config:
- If staying with CRA:
  - Replace `import.meta.env` with `process.env.REACT_APP_*`.
  - Replace `types: [\"vite/client\"]` in `tsconfig.json`.
  - Rename env files to `.env.local` / `.env.production`.
- Or migrate fully to Vite and remove `react-scripts`.

2. Unify HTTP strategy:
- Use one client style (recommended: Axios `httpClient`) for all calls.
- Remove duplicated fetch/axios logic.

3. Centralize API base URL:
- Stop hardcoding `http://localhost:5050` in service files.
- Use environment config consistently.

4. UX and validation:
- Add GUID format validation for `residentId`.
- Add loading state + button disable during submit.
- Improve backend error display.

5. Tests:
- Replace default CRA template test with real form/API tests.

### Mobile

1. Wire form to service:
- In `app/create-care-request.tsx`, call `createCareRequest(dto)` in `onSubmit`.

2. Fix API config:
- Update default base URL to backend port (`5050`) for local simulator workflows.
- Add environment-based config for local LAN, simulator, and production.

3. Navigation cleanup:
- `app/_layout.tsx` currently renders custom UI and directly includes `CreateCareRequestScreen`.
- Decide on standard Expo Router flow (Stack/Tabs) and remove duplicated template routing.

4. UX improvements:
- Add submit loading state.
- Show success feedback and form reset.
- Show normalized error messages from service.

5. Testing:
- Add component tests for validation and submission flow.

## 4. Cross-Project Integration Contract

Define a shared, explicit API contract used by web and mobile:

1. Request model:
- `residentId: string (GUID)`
- `description: string`

2. Response model for create:
- `201 Created`
- body: `{ "id": "guid" }`

3. Error model:
- Standardized structure (for example ProblemDetails with `title`, `status`, `detail`, `errors`).

4. Environments:
- `local`, `docker`, `staging`, `production`
- Each frontend should resolve API URL per environment with no hardcoded URLs in feature modules.

## 5. Suggested Delivery Sequence (Pragmatic)

1. Stabilize configuration:
- Fix backend `appsettings.*` structure.
- Align web env strategy (CRA or Vite, not mixed).
- Align mobile API base URL strategy.

2. Complete MVP create flow on all clients:
- Backend create endpoint validated.
- Web create flow with robust states.
- Mobile create flow fully wired.

3. Add read/list API + client consumption:
- Build list screen on web and mobile.

4. Harden quality:
- Add focused automated tests.
- Add CI checks (build + tests).

## 6. Definition of Done for MVP Completion

- API runs locally and in Docker without manual config edits.
- Web app can create care requests against API with clear success/error UX.
- Mobile app can create care requests from simulator/device with environment-correct API URL.
- At least one read/list flow implemented end-to-end.
- No plaintext production secrets in repo.
- Basic automated tests pass for domain + application + API happy/error paths.
