# NursingCareProject: Development Continuation Guide (Pending Work Only)

Last reviewed: 2026-03-06

This guide intentionally excludes items already implemented in code and lists only the remaining work to complete the product flow.

## 1. Current Baseline (Already Done)

- Backend has create/list/get-by-id care request endpoints.
- Backend has JWT auth + authorization policy on care request endpoints.
- Backend has centralized exception middleware returning `application/problem+json`.
- Backend config structure has been corrected in `appsettings.*`.
- Backend identity entities (`User`, `Role`, `UserRole`) and migration were added.
- API tests were expanded for protected endpoints and validation paths.
- CI workflow exists for restore/build/test.

## 2. Top Remaining Priorities

### P0 - Must Be Done First

1. Web toolchain alignment (CRA vs Vite mismatch).
Deliverables:
- Decide one runtime: keep CRA or migrate fully to Vite.
- Remove incompatible env/type configuration from the non-selected runtime.
- Ensure `npm start`, `npm run build`, and tests run with no env/runtime errors.
Acceptance criteria:
- Web app starts locally and can call API without config hacks.
- No `import.meta.env` usage if CRA is kept.
- No `react-scripts` usage if Vite is selected.

2. Mobile create flow completion.
Deliverables:
- Wire form submit to `createCareRequest`.
- Add loading state, success state, and error handling.
- Prevent duplicate submissions while request is in flight.
Acceptance criteria:
- Submitting valid data creates a record via API.
- Failed requests show user-friendly error.
- Screen does not silently fail.

3. Shared environment strategy for web and mobile.
Deliverables:
- Define API base URL resolution rules for `local`, `docker`, `staging`, `production`.
- Remove hardcoded URLs from feature/service files.
- Document expected values per environment.
Acceptance criteria:
- Both clients switch environments by config only.
- No code edits required when switching local/device/docker.

### P1 - Core Product Completion

4. Care request lifecycle endpoints and UI actions.
Deliverables:
- Add at least one transition endpoint (`approve`, `reject`, or `complete`).
- Add corresponding UI action(s) in web and mobile.
- Enforce role-based authorization for transition operations.
Acceptance criteria:
- Authorized role can transition status.
- Unauthorized role receives correct 403 response.
- UI reflects updated status after operation.

5. Auth flow completion for real users.
Deliverables:
- Add login endpoint and token issuance pipeline.
- Add role bootstrap/management flow.
- Add token handling on web and mobile clients.
Acceptance criteria:
- User can sign in and access protected endpoints.
- Invalid token and expired token paths are handled correctly.

### P2 - Hardening and Quality

6. Security and secret management cleanup.
Deliverables:
- Replace placeholder/static secrets in tracked config.
- Move environment secrets to secure configuration sources.
Acceptance criteria:
- No plaintext production-grade secrets in committed config.

7. Test and CI hardening.
Deliverables:
- Add auth and lifecycle transition tests.
- Add web/mobile behavior tests for create/list/error handling.
- Keep CI green on restore/build/test.
Acceptance criteria:
- CI validates backend and frontend critical flows.

## 3. Remaining Work by Project

### Backend (`NursingCareBackend`)

1. Implement authentication domain/application flow:
Tasks:
- Add `POST /api/auth/login`.
- Add password hashing/verification service abstraction.
- Add JWT token service with issuer/audience/expiry/role claims.
Acceptance criteria:
- Valid credentials return token payload.
- Invalid credentials return consistent `401`/problem response.

2. Implement authorization model operations:
Tasks:
- Create role seed/bootstrap strategy for `Nurse` and `Admin`.
- Add user-role assignment flow (endpoint or seed command).
- Add admin-only user/role management endpoints if needed.
Acceptance criteria:
- Roles exist in DB across clean environments.
- Protected policies map correctly to expected roles.

3. Expand care request lifecycle:
Tasks:
- Add command + handler + endpoint for at least one transition.
- Enforce transition rules in domain (invalid transition throws controlled error).
- Persist transition timestamp and actor (if available).
Acceptance criteria:
- Transition updates record correctly.
- Invalid transitions return consistent error contract.

4. Strengthen validation and contract quality:
Tasks:
- Add explicit request validation where model-binding is not enough.
- Standardize response DTOs for list/detail/create/transition.
- Keep error payload shape uniform for all controller failures.
Acceptance criteria:
- API responses are predictable and documented.
- Consumers do not parse ad-hoc error formats.

5. Improve test depth:
Tasks:
- Add integration tests for login success/failure.
- Add authorization tests for protected endpoints.
- Add transition tests (happy path + invalid transition + forbidden role).
Acceptance criteria:
- New backend features are covered by automated tests in CI.

### Web (`NursingCareWeb/nursing_care_web_react`)

1. Resolve runtime/toolchain mismatch:
Tasks:
- Decide CRA or Vite in a technical decision note.
- Remove incompatible config artifacts from the non-selected stack.
- Confirm scripts and environment variable loading are consistent.
Acceptance criteria:
- Local start and production build both succeed.

2. Unify API client usage:
Tasks:
- Use a shared HTTP client for all endpoints.
- Move endpoint paths into centralized API modules.
- Remove hardcoded base URLs from components/services.
Acceptance criteria:
- One networking strategy across the app.

3. Harden user experience:
Tasks:
- Add client-side GUID validation.
- Add submit loading state and disable button while pending.
- Parse and render problem details (`title`, `detail`, validation errors).
Acceptance criteria:
- User gets clear validation and error feedback.

4. Build missing feature screens:
Tasks:
- Add care request list view consuming `GET /api/care-requests`.
- Add details view consuming `GET /api/care-requests/{id}`.
- Add status action controls when backend transition endpoint is ready.
Acceptance criteria:
- Web app supports create + list + detail end-to-end.

5. Add tests aligned with real behavior:
Tasks:
- Replace starter tests with feature tests.
- Add tests for create success/error and list rendering.
Acceptance criteria:
- Tests validate real user flows, not template scaffolding.

### Mobile (`NursingCareMobile/nursing-care-mobile`)

1. Wire submit action:
Tasks:
- Connect form submit to service call with `try/catch`.
- Add success alert/toast and reset fields.
- Handle and display API failure messages.
Acceptance criteria:
- Create flow works end-to-end from app UI.

2. Fix API environment strategy:
Tasks:
- Introduce environment-based base URL config.
- Handle simulator/emulator/physical-device cases explicitly.
- Document required local setup (LAN IP, ports, Docker mode).
Acceptance criteria:
- Mobile app can hit API from emulator and physical device without code edits.

3. Improve navigation structure:
Tasks:
- Finalize routing approach and simplify root layout.
- Remove template routes/screens not part of product flow.
Acceptance criteria:
- Navigation matches real feature structure and is maintainable.

4. Add UX robustness:
Tasks:
- Add pending state and submit guard.
- Add GUID and required-field validation messaging.
Acceptance criteria:
- User cannot accidentally submit duplicate requests.

5. Add tests:
Tasks:
- Add component tests for form validation and submit states.
- Add service tests for HTTP error mapping.
Acceptance criteria:
- Core create flow has regression protection in tests.

## 4. Cross-Project Contract Work Still Needed

1. Publish a single source of truth for API contracts:
- Endpoint list, request/response DTOs, and error model.

2. Align frontend consumption with backend contract:
- Parse and show structured problem details.
- Keep naming and types consistent across web/mobile.

3. Define environment matrix:
- `local`, `docker`, `staging`, `production`.
- Document API URL and auth settings per environment for each client.

## 5. Suggested Next Sprint Scope

1. Finalize web toolchain alignment and env strategy.
2. Complete mobile create flow wiring and environment handling.
3. Implement backend login/token issuance and role bootstrap.
4. Implement one status transition endpoint with tests.
5. Deliver web + mobile list view consuming current read endpoints.

## 6. Updated MVP Done Criteria

- Web app can create and list care requests against the backend using environment-based API config.
- Mobile app can create care requests from simulator/device without manual code edits per environment.
- Backend supports authenticated user flow and at least one role-guarded status transition.
- Error contracts are consistent and displayed clearly on both clients.
- CI passes restore/build/tests with no plaintext production credentials in tracked config.
