# NursingCareProject: Development Continuation Guide

Last reviewed: 2026-03-17

This guide now reflects the current state of the code across:

- `NursingCareBackend`
- `NursingCareWeb/nursing_care_web_react`
- `NursingCareMobile/nursing-care-mobile`

Status legend:

- `[Completed]` Implemented in the current codebase
- `[Partial]` Started, but still missing meaningful work
- `[Pending]` Not implemented yet

## 1. Current Baseline

### Backend

- `[Completed]` Create, list, and get-by-id care request endpoints exist.
- `[Completed]` JWT auth and role-based authorization are wired.
- `[Completed]` Auth endpoints exist for register, login, setup-admin, and assign-role.
- `[Completed]` Centralized exception middleware returns `application/problem+json`.
- `[Completed]` Structured logging, correlation IDs, and user-enriched request logs are in place.
- `[Completed]` Swagger supports Bearer auth.
- `[Completed]` Local HTTPS reverse-proxy setup and setup docs exist.

### Web

- `[Completed]` Web runtime is standardized on Vite.
- `[Completed]` API access goes through centralized client modules.
- `[Completed]` Web request logging with correlation IDs is implemented.
- `[Completed]` Create care request flow is wired to the backend.

### Mobile

- `[Completed]` Mobile create care request flow is wired to the backend.
- `[Completed]` Mobile login/token flow is implemented.
- `[Completed]` Shared mobile HTTP client adds correlation IDs and client metadata.
- `[Completed]` Info tab exposes auth, diagnostics, backend connectivity test, and client logs.

## 2. Priority Review

### P0 - Must Be Done First

1. Web toolchain alignment.
- Status: `[Completed]`
- Notes:
  - The web app now runs on Vite.
  - `package.json` uses `vite` for `start` and `build`.
  - The old CRA mismatch is no longer the main blocker.
  - One follow-up remains: `src/react-app-env.d.ts` is still present and should be cleaned up to fully remove CRA leftovers.

2. Mobile create flow completion.
- Status: `[Completed]`
- Notes:
  - Submit is wired to `createCareRequest`.
  - Loading, success, error handling, and duplicate-submit protection are present.
  - Logs include correlation IDs.

3. Shared environment strategy for web and mobile.
- Status: `[Completed]`
- Notes:
  - Web uses `VITE_API_BASE_URL`.
  - Mobile uses `EXPO_PUBLIC_API_BASE_URL`.
  - Backend local HTTPS/dev proxy flow is documented.
  - Hardcoded feature-level URLs were removed from current request flows.
  - The shared cross-project matrix now lives in `ENVIRONMENT_MATRIX.md`.

### P1 - Core Product Completion

4. Care request lifecycle endpoints and UI actions.
- Status: `[Pending]`
- Notes:
  - Create/list/get-by-id exist.
  - No approve/reject/complete endpoint exists yet.
  - No web or mobile transition UI exists yet.

5. Auth flow completion for real users.
- Status: `[Partial]`
- Notes:
  - Backend login/token issuance is implemented.
  - Role bootstrap and role assignment are implemented.
  - Mobile token handling is implemented.
  - Web does not yet have a user login/token flow for protected endpoints.
  - Expired-token and refresh/session persistence behavior are not fully rounded out.

### P2 - Hardening and Quality

6. Security and secret management cleanup.
- Status: `[Completed]`
- Notes:
  - Local-only artifacts and generated cert material are ignored.
  - Example env files and setup docs were added.
  - Launch settings were sanitized for publishable defaults.
  - Shared sync automation now uses safe development placeholders instead of project-specific secret values.
  - Production/staging secrets are expected to come from deployment environment configuration rather than committed files.

7. Test and CI hardening.
- Status: `[Completed]`
- Notes:
  - Backend API tests now cover create/list/get-by-id plus auth/login and authorization paths.
  - Web has feature tests for create success and error rendering.
  - Mobile has service/logger tests for HTTP error mapping and correlation-aware logging.
  - Backend, web, and mobile each have CI workflow coverage for their current build/test checks.

## 3. Remaining Work by Project

### Backend (`NursingCareBackend`)

1. Authentication domain/application flow.
- Status: `[Completed]`
- Notes:
  - `POST /api/auth/login` exists.
  - Password verification and JWT token generation are implemented.
  - Tokens include user identity and roles.

2. Authorization model operations.
- Status: `[Completed]`
- Notes:
  - Role bootstrap/setup-admin flow exists.
  - Role assignment endpoint exists.
  - Policies are used for care request access.

3. Expand care request lifecycle.
- Status: `[Pending]`
- Notes:
  - No transition command/handler/endpoint is implemented yet.
  - No persisted actor/timestamp transition workflow exists yet.

4. Strengthen validation and contract quality.
- Status: `[Partial]`
- Notes:
  - ProblemDetails handling is consistent for central exception flows.
  - Login invalid-credential handling returns proper auth responses.
  - Current create/list/detail contracts are predictable enough for current clients.
  - Still missing: stronger explicit validation coverage and standardized DTO shape for future transition/lifecycle operations.

5. Improve test depth.
- Status: `[Completed]`
- Notes:
  - Care request API tests exist.
  - Auth success/failure and authorization coverage now exist.
  - Transition tests are still naturally deferred until the lifecycle feature exists.

### Web (`NursingCareWeb/nursing_care_web_react`)

1. Resolve runtime/toolchain mismatch.
- Status: `[Completed]`
- Notes:
  - Vite is selected and working.
  - Build works.
  - A small CRA cleanup pass is still worth doing, but the core migration is complete.

2. Unify API client usage.
- Status: `[Completed]`
- Notes:
  - Shared HTTP client and API modules are in place.
  - Request logging and correlation IDs are wired centrally.

3. Harden user experience.
- Status: `[Partial]`
- Notes:
  - Backend errors are surfaced and logged.
  - Still missing:
    - client-side GUID validation
    - pending/disabled submit handling
    - more polished structured error rendering

4. Build missing feature screens.
- Status: `[Pending]`
- Notes:
  - Current web UI supports create only.
  - List, detail, and lifecycle action screens are still missing.

5. Add tests aligned with real behavior.
- Status: `[Completed]`
- Notes:
  - The current create flow now has behavior-aligned success and error tests.
  - Additional tests will still be needed when list/detail/auth UI is added.

### Mobile (`NursingCareMobile/nursing-care-mobile`)

1. Wire submit action.
- Status: `[Completed]`
- Notes:
  - The create flow works end to end from UI to API.

2. Fix API environment strategy.
- Status: `[Partial]`
- Notes:
  - Base URL is environment-driven with `EXPO_PUBLIC_API_BASE_URL`.
  - Device testing and local HTTPS steps are documented.
  - Still missing: a no-code-change environment matrix that fully covers every local/docker/staging/production case.

3. Improve navigation structure.
- Status: `[Partial]`
- Notes:
  - The routing/layout is much closer to the actual product shape now.
  - Auth diagnostics live in the Info tab and create flow has its own screen.
  - Still worth cleaning any leftover template-era structure that is not part of the real feature set.

4. Add UX robustness.
- Status: `[Partial]`
- Notes:
  - Pending state and submit guard are implemented.
  - User-facing error messaging exists.
  - Still missing: stronger field validation, especially GUID validation before submit.

5. Add tests.
- Status: `[Completed]`
- Notes:
  - Core mobile support code now has regression coverage for HTTP error mapping and correlation-aware logging.
  - UI-level form tests can be added later as the feature surface grows.

## 4. Cross-Project Contract Work

1. Publish a single source of truth for API contracts.
- Status: `[Pending]`
- Notes:
  - The actual contract exists in backend code and Swagger.
  - There is not yet a dedicated shared contract package or canonical published contract workflow for all clients.

2. Align frontend consumption with backend contract.
- Status: `[Partial]`
- Notes:
  - Web and mobile both consume current create/auth flows and surface backend errors.
  - Naming/types are aligned for the implemented flows.
  - This is still incomplete for list/detail/lifecycle features not yet built on the clients.

3. Define environment matrix.
- Status: `[Completed]`
- Notes:
  - Local HTTPS/device setup is documented.
  - Client env variables exist.
  - The consolidated environment matrix now covers `local`, `docker`, `staging`, and `production`.

## 5. Suggested Next Sprint Scope

1. Build one care request lifecycle transition end to end:
   backend endpoint, authorization, tests, web UI action, mobile UI action.
2. Finish web auth/token handling for protected flows.
3. Add list/detail screens on web and mobile.
4. Expand automated coverage for lifecycle transitions and future list/detail UI.
5. Consolidate environment documentation into one cross-project matrix.

## 6. Updated MVP Done Criteria

- `[Partial]` Web app can create care requests against the backend using environment-based config.
- `[Pending]` Web app can list and inspect care requests end to end.
- `[Completed]` Mobile app can create care requests from a physical device using environment-based API config.
- `[Partial]` Backend supports authenticated user flow.
- `[Pending]` Backend supports at least one role-guarded status transition.
- `[Partial]` Error contracts are mostly consistent and are surfaced on both clients for implemented flows.
- `[Completed]` CI/build validation now exists across backend, web, and mobile for the currently implemented flows.
