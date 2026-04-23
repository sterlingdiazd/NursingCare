# Gemini Backend Bootloader

Use `../AGENTS.md` as the workspace contract.

Before changing backend behavior, load the relevant guides from `../NursingCareDocumentation/`:
- `guides/PROJECT_GUIDE.md`
- `guides/ARCHITECTURE_AND_PATTERNS.md`
- `specs/BUSINESS_RULES_AND_VALIDATION.md`
- `specs/AUTH_SECURITY_AND_DATA_ACCESS_RULES.md`

Backend-specific rules:
- preserve Clean Architecture and Domain-Driven Design boundaries
- keep authorization and protected-data rules enforced in backend code
- do not modify `.env` files
- run targeted backend tests for changed behavior

Software Development Life Cycle (SDLC) state remains under `../NursingCareDocumentation/docs/sdlc/`.
Keep handoffs compact and prefer referenced artifacts for large outputs.
