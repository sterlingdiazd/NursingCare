# NursingCareBackend Agent Contract

Agent contract for the `NursingCareBackend` repository. This repo is normally developed inside
the NursingCare workspace (whose umbrella root holds the workspace-wide `AGENTS.md`). This copy
travels with the repo so the `/flow` workflow is available even when the repo is used on its own.

- Preserve Clean Architecture / DDD boundaries; dependencies point inward only.
- Keep Spanish user-facing copy, problem-details behavior, correlation IDs, and auth messaging
  contracts unless the task explicitly changes them.
- Do not modify `.env` files; use environment-variable overrides.
- Local gate for this repo: `dotnet build src/src.sln` + `dotnet test`.
- CI gate for this repo: GitHub Actions `dotnet-ci.yml` (Gitleaks + Trivy -> build -> test).

## The `/flow` Workflow (provider-agnostic)

Trigger: this playbook is the DEFAULT agentic loop — run it end to end for every non-trivial change
in every session, whether or not the message is prefixed with `/flow`. Typing `/flow ...` (for
example `/flow add X`) only invokes the same loop explicitly; never wait for the prefix. Trivial
typo/one-line fixes may skip steps 2-3 but never the step 5 local gate or the step 7-9 git discipline. It is intentionally model-, provider-, and CLI-agnostic: it carries no
model name, API key, or tool-specific setting. The only machinery is this text plus `git` and
`gh` (GitHub CLI). Any agent that reads this file and has a shell runs the same `/flow`.

Always-on rules for `/flow`:
- All `git`, commit, and push actions happen inside this repository, never a parent directory.
- Bounded retries everywhere; never loop indefinitely.
- Stop-and-report rule: if blocked, ambiguous, or a fix would be risky or destructive, stop and
  report findings instead of guessing or thrashing `main`.
- Never make a gate pass by faking it (no skipped tests, no `continue-on-error`, no suppressed
  scanner severity, no force-push).

Steps:

1. Intake & triage — restate the task and its acceptance criteria in one line; identify the
   file scope; classify trivial (skip to step 4) vs non-trivial.
2. Research (non-trivial only) — read the relevant code and the matching guides/rules before
   editing; cite `file:line`. Web search only for external/unknown APIs. Produce a short
   findings note; do not delegate understanding.
3. Plan — approach, exact files to change, existing patterns/utilities to reuse, test strategy,
   and risks. For multi-file or behavior-changing work, write the plan and align before large edits.
4. Develop — implement per the rules above; backend-first then frontend when both apply; keep
   changes cohesive; add/update tests for the changed surface.
5. Local gate (must pass before any push) — `dotnet build src/src.sln` + `dotnet test`
   (targeted, expanding when shared behavior changed). Never push code that is red locally.
6. Quality judge (rubric gate) — self-score the diff against the Design-KPI Rubric (kept in the
   sibling `NursingCareDocumentation` repo at `specs/DESIGN_KPI_RUBRIC.md`): each applicable
   criterion 0-100, PASS floor 85 per criterion (not blended), categories applied by diff type,
   Category 7 always applies, gating criteria hard-fail. If any applicable criterion < 85 or a
   gating criterion fails, remediate and re-judge (max 2 cycles). Record the verdict.
7. Commit — concise message that explains the why. Stage specific files; never stage `.env`,
   secrets, or large binaries (CI runs Gitleaks/Trivy — fix secret/vulnerability issues locally).
   Do not skip hooks.
8. Push to `main` — only if steps 5 and 6 passed. Sync first (`git fetch` then
   `git pull --rebase`) to avoid non-fast-forward rejects, then push to `main`. Never force-push.
9. CI gate + self-heal — watch the run with `gh run watch` (or poll
   `gh run list --branch main --limit 1`). On failure: `gh run view --log-failed`, fix the root
   cause locally, re-run the local gate, commit, and push again. Bounded to 3 self-heal cycles;
   if still red or the fix is non-obvious/risky, stop and report with the failed logs.
10. Report (Definition of Done) — Done = local gate green + rubric >= 85 with no gating fail +
    CI green on `main`. Report what changed, the judge verdict, the CI run URL/status, and
    anything deferred.

The hard gate is GitHub Actions (it genuinely blocks). The local gate and rubric judge are
agent-enforced — their value depends on keeping them honest. Switching CLI or model requires no
change here; the same `/flow` applies.
