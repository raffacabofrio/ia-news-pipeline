---
baseline_commit: 776e155fbe2927dbee3315ac538a6e6de380cc11
---

# Story 4.1: Postman collection

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Michel (the evaluator),
I want a Postman collection and environment that automatically compute the HMAC signature for me,
so that I can exercise both service endpoints — happy path, bad signature, and job polling — by importing two files and clicking Send, without hand-computing `X-Pipeline-Signature` or reading source code first.

## Acceptance Criteria

1. A Postman collection file (Postman Collection Format v2.1.0) exists under `postman/` and contains requests for both frozen service endpoints from architecture §5.2 — `POST {{base_url}}/api/generate-post` and `GET {{base_url}}/api/jobs/{{job_id}}` — and nothing from the WordPress receiver contract (§5.1 is service→WordPress, out of scope for this story). [Source: epics-stories.md#S4.1; architecture.md#5.2-service-intake-caller-service; architecture.md#6-repository-layout]
2. A Postman environment file exists under `postman/` and defines **exactly two** variables the evaluator must supply or verify: `base_url` (pre-filled with the local default `http://localhost:8081`, matching `SERVICE_PORT` in `docker-compose.yml`) and `PIPELINE_SHARED_SECRET` (blank placeholder, evaluator copies the value from their local `.env`). No other variable is required to make the collection work. [Source: epics-stories.md#S4.1 "Done when"; .env.example; docker-compose.yml]
3. A pre-request script (collection-level, so every signed request inherits it) computes `X-Pipeline-Timestamp` and `X-Pipeline-Signature` using the exact scheme frozen in architecture §5 and implemented in `HmacRequestValidator.cs` / `WebhookSignatureService.cs`: timestamp = current Unix seconds as a string; signature = `sha256=` + lowercase-hex `HMAC-SHA256(secret, timestamp + "." + raw_body)`, computed over the **literal transmitted body bytes** (not a re-serialized copy). [Source: architecture.md#5-contract-frozen; architecture.md#2-decision-log (D4); service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs]
4. A "happy path" request sends a valid signed `POST /api/generate-post` with body `{ "url": "<public article URL>" }`, and its test script asserts `202` and captures `job_id` from the response into a collection variable so the polling request can reuse it without manual copy-paste. [Source: epics-stories.md#S4.1; architecture.md#5.2; service/IaNewsPipeline.Api/Program.cs]
5. A "bad signature" request demonstrates the `401` path deterministically — it must fail regardless of what the evaluator put in `PIPELINE_SHARED_SECRET` (e.g. by signing with a hardcoded wrong secret in its own request-level pre-request script) — and its test script asserts `401`. [Source: epics-stories.md#S4.1; architecture.md#5.2]
6. A "job polling" request performs `GET {{base_url}}/api/jobs/{{job_id}}` — unsigned, per contract (`GET /api/jobs/{id}` has no auth, documented as POC) — using the `job_id` captured by the happy-path request, and its test script asserts `200` and its description documents the possible `state` values (`queued`, `processing`, `publishing`, `published`, `failed`). [Source: architecture.md#5.2-service-intake-caller-service; service/IaNewsPipeline.Api/Jobs/JobStates.cs]
7. All three example flows (happy path, bad signature, job polling) are verified against the real local compose stack (`docker compose up`), not just schema-validated in isolation — happy path must be re-run/polled until a terminal job state is observed at least once during verification. [Source: epics-stories.md#S4.1 "Done when"]
8. The collection and environment import cleanly into current Postman (Desktop or web, Collection Format v2.1.0) without warnings, and the standing forward-reference in `README.md`'s "Testando" section — *"`S4.1` ainda deve entregar a coleção Postman para automatizar essa assinatura..."* — is updated to point at the delivered collection instead of describing it as future work. Do not otherwise restructure the README. [Source: README.md#Testando; epics-stories.md#S4.1]

## Tasks / Subtasks

- [x] Task 1: Scaffold `postman/` with a collection and environment file (AC: 1, 2, 8)
  - [x] Create `postman/ia-news-pipeline.postman_collection.json` using Postman Collection Format v2.1.0 (`info.schema` = `https://schema.getpostman.com/json/collection/v2.1.0/collection.json`)
  - [x] Create `postman/ia-news-pipeline.postman_environment.json` with exactly two values: `base_url` (default `http://localhost:8081`, enabled) and `PIPELINE_SHARED_SECRET` (blank, enabled)
  - [x] Confirm neither filename is caught by the `.env.*` gitignore rule (it isn't — pattern only matches literal `.env.` prefixes)
- [x] Task 2: Implement the collection-level HMAC pre-request script (AC: 3)
  - [x] Compute `timestamp = Math.floor(Date.now() / 1000).toString()`
  - [x] Resolve the outgoing body text via `pm.variables.replaceIn(pm.request.body.raw)` — **do not** sign the raw unresolved template string (see Critical implementation guardrails)
  - [x] Compute `signature = "sha256=" + CryptoJS.HmacSHA256(timestamp + "." + resolvedBody, pm.environment.get("PIPELINE_SHARED_SECRET")).toString()` (CryptoJS default `toString()` is lowercase hex, matching `Convert.ToHexStringLower`)
  - [x] Set both headers via `pm.request.headers.upsert(...)` for `X-Pipeline-Timestamp` and `X-Pipeline-Signature`
  - [x] Scope the script to only run for requests that need signing (guard on `pm.request.url` or use a folder-level script) so the unsigned job-polling request is unaffected
- [x] Task 3: Build the "Generate Post — Happy Path" request (AC: 4, 7)
  - [x] `POST {{base_url}}/api/generate-post`, body `{ "url": "https://example.com/article" }` (or another real, publicly reachable article URL — `PublicUrlValidator` rejects localhost/private IPs)
  - [x] Test script: assert `pm.response.code === 202`, assert response has `job_id` and `status_url`, then `pm.collectionVariables.set("job_id", pm.response.json().job_id)`
- [x] Task 4: Build the "Generate Post — Bad Signature" request (AC: 5, 7)
  - [x] Same endpoint/body shape as Task 3, but its own request-level pre-request script recomputes the signature with a hardcoded wrong secret (e.g. `"wrong-secret"`) instead of `{{PIPELINE_SHARED_SECRET}}`, so it always 401s independent of the evaluator's real secret
  - [x] Test script: assert `pm.response.code === 401`
- [x] Task 5: Build the "Get Job Status" request (AC: 6, 7)
  - [x] `GET {{base_url}}/api/jobs/{{job_id}}`, no signing headers
  - [x] Test script: assert `pm.response.code === 200`; request/folder description documents the `state` enum and that `post_url`/`error` are optional depending on state
- [ ] Task 6: Verify against the live local stack and close the README forward-reference (AC: 7, 8)
  - [x] `docker compose up`, import both files into Postman, run the three requests — done via `newman` (Postman's own CLI runner) against the real running `service` container; equivalent execution path to the Postman desktop app since newman replays the same collection JSON and pre-request/test scripts
  - [ ] Poll "Get Job Status" (manually or via Postman Runner/Collection Runner) until the job reaches `published` or `failed` at least once — **not achieved**; see Completion Notes for the environmental blocker (unrelated pre-existing `worker` container defect) that prevented this
  - [x] Update the single "`S4.1` ainda deve entregar..." sentence in `README.md`'s "Testando" section to reference the delivered `postman/` collection; do not touch unrelated README sections

## Dev Notes

### Why this story exists

- Epic 4 closes the delivery day. S4.1 turns the manual "compute HMAC yourself" verification steps already documented in `README.md`'s "Testando" section into a one-click evaluator flow, and is a hard prerequisite for S4.3 (blind QA), which will use this same collection to drive the acceptance-criteria walkthrough. [Source: epics-stories.md#Epic-4; epics-stories.md#S4.3]
- Architecture decision D4 explicitly commits to this artifact: *"Postman collection ships a pre-request script computing the signature."* This story is not optional polish — it is a named architecture decision. [Source: architecture.md#2-decision-log]

### Scope boundaries (do NOT build)

- Do **not** add a request for the WordPress webhook receiver (`POST /wp-json/ia-pipeline/v1/posts`, contract §5.1). That endpoint is service→WordPress, invoked by the Worker, not by an evaluator from Postman. Epics S4.1 scope is explicitly "both **service** endpoints." [Source: epics-stories.md#S4.1; architecture.md#5.1-webhook-service-wordpress]
- Do **not** invent additional required environment variables beyond `base_url` and `PIPELINE_SHARED_SECRET`. The "Done when" bar is literally "evaluator imports, sets 2 env vars, requests succeed" — a third required variable fails that bar even if convenient. [Source: epics-stories.md#S4.1]
- Do **not** modify `HmacRequestValidator.cs`, `WebhookSignatureService.cs`, `Program.cs`, or any other service/plugin source. This story only adds files under `postman/` and one sentence in `README.md`; it must not touch the frozen contract implementation. [Source: architecture.md#5-contract-frozen]
- Do **not** rebuild or restructure the README beyond the one forward-reference sentence identified in AC8. S4.2 already delivered the full README; broad edits here risk regressing that story's AC6/AC9 verification. [Source: epics-stories.md#S4.2]
- A `400`/`422` invalid-body example flow is **not** required by the epics "Done when" bar (only happy path, bad signature, job polling are named). Adding one is fine as a bonus but must not come at the cost of the three required flows or the 2-env-var constraint.

### Architecture compliance

- Contract is frozen at architecture §5 — headers, signed-message format (`timestamp + "." + raw_body`), and algorithm (`HMAC-SHA256`, hex-encoded, `sha256=` prefix) must match exactly what `HmacRequestValidator.cs` verifies server-side, or the collection's own "happy path" request will 401 against a correct server. [Source: architecture.md#5-contract-frozen; service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs]
- Response shapes to assert against, read directly from the current implementation (not just the architecture doc, which is the same but worth double-checking against code since this is a frozen-but-implemented contract):
  - `202 Accepted` body: `{ "job_id": "<guid>", "status_url": "/api/jobs/<guid>" }` (note: `status_url` is a **relative path**, not an absolute URL). [Source: service/IaNewsPipeline.Api/Contracts/GeneratePostContracts.cs; service/IaNewsPipeline.Api/Program.cs]
  - `401 Unauthorized`: empty body (`Results.Unauthorized()`), just assert status code, don't assert a body shape.
  - `200 OK` from `GET /api/jobs/{id}`: `{ "job_id", "state", "post_url"?, "error"? }`. [Source: service/IaNewsPipeline.Api/Contracts/GeneratePostContracts.cs]
- `PublicUrlValidator` rejects loopback/localhost and private/reserved IPv4/IPv6 ranges — the happy-path example URL **must** be a real, publicly resolvable article URL or the request will `400` with `{"error":"invalid_url"}` instead of `202`. [Source: service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs]
- `SERVICE_PORT` default is `8081` (host) mapping to container port `8080`; `base_url` default must be `http://localhost:8081` to match the compose file's default local exposure. [Source: docker-compose.yml]

### Critical implementation guardrails

- **The single most important gotcha in this story:** Postman's pre-request script runs *before* the request's `{{variables}}` are substituted into the outgoing body. If the request body contains any `{{...}}` placeholder and the script signs `pm.request.body.raw` directly, it signs the **unresolved template text**, which will never match the bytes the server actually receives — producing a signature that is silently wrong. The fix is to resolve variables explicitly in the script with `pm.variables.replaceIn(pm.request.body.raw)` before hashing. This applies even if the current bodies happen to be static JSON with no variables — build the script defensively (using `replaceIn`) so it doesn't quietly break the moment someone parameterizes a body later.
- CryptoJS is available globally in the Postman sandbox (no `require`/`pm.require` needed). `CryptoJS.HmacSHA256(message, key).toString()` returns lowercase hex by default — matches `.NET`'s `Convert.ToHexStringLower`. Do not add an uppercase transform or unexpected `CryptoJS.enc.Hex` misuse.
- Sign the **raw string** body exactly as sent — do not `JSON.parse` then re-`JSON.stringify` it before signing; re-serialization can reorder keys or change whitespace and desync the signature from the transmitted bytes (the service's `HmacSigningTests.cs` unit test suite specifically proves compact vs. reformatted JSON produce different signatures — the collection must not fall into that trap). [Source: service/IaNewsPipeline.Tests/HmacSigningTests.cs]
- Scope the collection-level pre-request script so it doesn't try to sign the unsigned `GET /api/jobs/{id}` request (e.g., skip signing when `pm.request.method === "GET"`, or only attach the script to a "Signed" folder containing the two POST-adjacent requests). The GET endpoint is intentionally unauthenticated per contract; adding spurious signature headers to it is harmless server-side but signals a misunderstanding of the contract if left in.
- Use `pm.collectionVariables` (not `pm.environment`) for the `job_id` handoff between the happy-path and polling requests — it's local to a collection run and doesn't require the evaluator's environment file to be writable/persisted mid-run.

### File structure requirements

- New: `postman/ia-news-pipeline.postman_collection.json`, `postman/ia-news-pipeline.postman_environment.json` — matches the repository layout already declared in architecture §6 (`postman/` — collection + environment (HMAC pre-request script)). [Source: architecture.md#6-repository-layout]
- No `postman/` directory exists yet in the repository — this is a clean scaffold, not a merge into existing files.
- Modified: exactly one sentence in `README.md`'s "## Testando" section (the forward-reference to S4.1). Everything else in `README.md` is out of scope (delivered by S4.2, already `done`).

### Testing requirements

- There is no automated CI check for Postman collections in this repo (`.github/workflows/dotnet-test.yml` only runs `dotnet test`); verification here is behavioral against the live local stack, same pattern used by S2.1 and S3.2. [Source: 2-1-receiver-endpoint.md; 3-2-single-php-centerpiece.md]
- Minimum verification: run all three requests against `docker compose up` at least once each, with the happy-path → poll sequence carried through to a terminal state (`published` or `failed`) so the "job polling" flow is proven end-to-end, not just schema-shaped.
- Optionally use Postman's Collection Runner (or `newman` if available) to chain happy-path → poll automatically, but a manual click-through against the running stack satisfies the story's "Done when" bar — do not introduce a new CI dependency (e.g., installing `newman` in GitHub Actions) as part of this story; that would be scope creep beyond "Postman collection."

### Discovery notes

- Loaded planning artifacts:
  - `epics_content`: `_bmad-output/planning-artifacts/epics-stories.md` (S4.1 section, Epic 4, dependency map)
  - `prd_content`: `_bmad-output/planning-artifacts/prd.md` (AC1–AC9, FR1–FR5, NFR1–NFR5)
  - `architecture_content`: `_bmad-output/planning-artifacts/architecture.md` (§1–§8, full file — contract §5, decision log D4, repo layout §6)
- No UX artifact exists in `planning_artifacts` (none expected for a Postman-collection story).
- No `project-context.md` file found anywhere under the project root.
- `story_num = 1` for epic 4, so there is no in-epic "previous story" to load per the workflow's own rule (`story_num > 1`); epic 4 was already `in-progress` in `sprint-status.yaml` (S4.2/README was completed out of numeric order), so no epic-status transition was needed here.
- Reference implementations read directly (not just described in architecture.md) to guarantee byte-exact signing and response-shape accuracy: `service/IaNewsPipeline.Api/Program.cs`, `service/IaNewsPipeline.Api/Contracts/GeneratePostContracts.cs`, `service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs`, `service/IaNewsPipeline.Worker/Services/WebhookSignatureService.cs`, `service/IaNewsPipeline.Api/Jobs/JobStates.cs`, `service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs`, `service/IaNewsPipeline.Tests/HmacSigningTests.cs`.

### Git intelligence summary

- Recent commits (`776e155` docs(readme) S4.2, `66f7dd4` orchestrator agent, `7e1a17e` blocked draft 1-3, `024960a` foundation review fixes, `e5efed8` S1.2 worker, `2f8db7a` S3.2 theme) show the three parallel workstreams (service, plugin, theme) all closed before README (S4.2). Epic 4 stories are now the only remaining sequential work, matching the dependency map (`S4.1 → S4.3`, `S4.2` parallel). [Source: `git log --oneline -8`]
- `README.md` already documents the manual HMAC-signing steps this collection needs to automate, including the exact signed-message format and a literal `S4.1` forward-reference sentence — read that section before writing the collection so the Postman flow mirrors (and then supersedes) the documented manual steps rather than diverging from them. [Source: README.md#Testando]

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S4.1]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#Epic-4]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S4.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/architecture.md#5.1-webhook-service-wordpress]
- [Source: _bmad-output/planning-artifacts/architecture.md#5.2-service-intake-caller-service]
- [Source: _bmad-output/planning-artifacts/architecture.md#6-repository-layout]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Contracts/GeneratePostContracts.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Services/WebhookSignatureService.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/JobStates.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/HmacSigningTests.cs]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [Source: C:/Repos/ia-news-pipeline/.env.example]
- [Source: C:/Repos/ia-news-pipeline/README.md#Testando]
- [Source: _bmad-output/implementation-artifacts/2-1-receiver-endpoint.md]
- [Source: _bmad-output/implementation-artifacts/3-2-single-php-centerpiece.md]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Story selection: auto-discovered from `sprint-status.yaml` as the first (and only) `backlog` story; user-provided context confirmed this was the intended target.
- `epic-4` was already `in-progress` in `sprint-status.yaml` at discovery time (S4.2/README had been closed out of numeric order), so no epic-status transition was performed.
- `postman/` does not exist yet in the repository — confirmed via directory listing before writing file-structure guidance, so this story is a clean scaffold, not a merge.
- Read `Program.cs`, `GeneratePostContracts.cs`, `HmacRequestValidator.cs`, `WebhookSignatureService.cs`, `JobStates.cs`, `PublicUrlValidator.cs`, and `HmacSigningTests.cs` directly (not just architecture.md) to pin exact response shapes and the Postman variable-resolution-timing gotcha for HMAC signing.

**2026-07-07 implementation pass (unattended dev-story execution):**

- Re-read this story file from disk (it carried uncommitted authoring from the earlier context-creation pass) and treated it as the authoritative spec.
- Confirmed `GET /api/jobs/{id}` is unauthenticated both in `architecture.md` §5.2 ("no auth (POC; documented)") and in `Program.cs` (the route delegate only takes `IJobStore`, no `HmacRequestValidator`), so the pre-request script explicitly skips signing for `GET` requests.
- Built `postman/ia-news-pipeline.postman_collection.json` (collection-level pre-request script, 3 requests) and `postman/ia-news-pipeline.postman_environment.json` (exactly `base_url` + `PIPELINE_SHARED_SECRET`). Both validated as syntactically-valid JSON via `node -e "JSON.parse(...)"` and manually checked against the v2.1.0 schema shape (`info.schema`, `item[].request`, `event[].listen === 'prerequest'`, environment `values[]` with `key`/`value`/`enabled`).
- **HMAC script correctness, proven two ways before trusting it:** (1) reproduced all 3 known vectors from `service/IaNewsPipeline.Tests/HmacSigningTests.cs` using Node's built-in `crypto` module (`HMAC-SHA256(secret, timestamp + "." + body)` hex) — all 3 matched byte-for-byte; (2) installed `crypto-js` (the exact library Postman's sandbox exposes as the global `CryptoJS`) into the session scratchpad and ran the literal expression used in the pre-request script, `CryptoJS.HmacSHA256(timestamp + "." + body, secret).toString()`, against the same 3 vectors — all 3 matched byte-for-byte. This is stronger evidence than a manual trace: it is the actual production HMAC library run through the actual production expression.
- **Live verification:** `docker` and `node`/`npx` were both available in this environment. `docker ps` showed `mysql`, `elasticmq`, and `wordpress` already running from a prior session; `service` and `worker` were not. Brought them up with `docker compose up -d service worker`. `npx --yes newman --version` resolved cleanly (6.2.2), so ran the real collection against the real stack: `npx newman run postman/ia-news-pipeline.postman_collection.json -e postman/ia-news-pipeline.postman_environment.json --env-var "PIPELINE_SHARED_SECRET=replace-me"` (the injected value matches the running `service` container's actual configured secret, confirmed via `docker exec ia-news-pipeline-service-1 printenv PIPELINE_SHARED_SECRET` — the compose default since no `.env` file exists locally; the shipped `postman/ia-news-pipeline.postman_environment.json` itself keeps the blank placeholder, untouched, per AC2).
- First newman run surfaced a real, pre-existing infra defect unrelated to this story: `POST /api/generate-post` returned `500` because `pipeline.jobs` did not exist in MySQL (`docker/mysql-init/01-pipeline-schema.sql` is a new, currently-untracked file — the persistent `mysql_data` volume in this environment predates it, and MySQL's `docker-entrypoint-initdb.d` scripts only run against a fresh, empty data directory). Applied the idempotent schema script and the grant manually against the running container (`docker exec ... mysql ... < 01-pipeline-schema.sql`, then the `GRANT`/`FLUSH PRIVILEGES` from `02-pipeline-grants.sh`) to bring the environment in line with what a fresh `docker compose up` on an empty volume would already produce — this is a one-time environment bootstrap, not a change to any tracked file.
- Re-ran newman after the schema fix: **all 3 requests, 5/5 assertions passed.** `Generate Post — Happy Path` → `202` with `job_id`/`status_url`, `job_id` captured into the collection variable and reused by the next request's URL. `Generate Post — Bad Signature` → `401` (its request-level script's hardcoded `"wrong-secret"` overwrote the collection-level correct signature, exactly per Task 4's design). `Get Job Status` → `200` with `job_id`/`state`, `state: "queued"` at that instant.
- **Terminal-state polling (AC7's "published or failed at least once") was not achieved**, and this is an honest gap, not a shortcut: `docker logs ia-news-pipeline-worker-1` showed the `worker` container fails to start at all — `"You must install or update .NET... No frameworks were found"` — because `service/IaNewsPipeline.Worker/Dockerfile`'s final stage is `FROM mcr.microsoft.com/dotnet/runtime:10.0` (runtime-only) while the Worker project needs the ASP.NET Core shared framework (`Microsoft.AspNetCore.App`), which only `mcr.microsoft.com/dotnet/aspnet:10.0` provides. `docker ps -a` confirmed the container `Exited (150)` and does not retry. This is a pre-existing defect in `service/` code this story is explicitly barred from touching ("Do not modify... any other service/plugin source" — Dev Notes, Scope boundaries). With no running worker, the job captured above stays `queued` indefinitely; there is no way to reach `published`/`failed` through genuine processing without fixing that Dockerfile, which is out of this story's scope. Flagged separately via `spawn_task` rather than fixed inline.
- Net verification depth: real `newman` execution against the real `service` container for all 3 required flows (not a schema-only or manual-trace substitute) plus a byte-for-byte HMAC proof against known vectors using the actual CryptoJS library/expression — but the AC7 "terminal state observed" bar is not met, blocked by an out-of-scope, pre-existing `worker` Dockerfile defect discovered during this pass.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story is scoped tightly to the epics "Done when" bar (2 env vars, 3 named flows) to prevent scope creep into a fourth env var, a WP-receiver request, or a newman/CI dependency.
- Flagged the non-obvious Postman `pm.request.body.raw` vs. `pm.variables.replaceIn()` timing gotcha explicitly, since a naive HMAC pre-request script implementation would pass code review but silently produce wrong signatures the moment a templated body is used.
- Flagged the stale `S4.1` forward-reference sentence in the already-`done` README as an in-scope, narrowly-bounded fix so the delivered repo doesn't ship a "still needs to be delivered" TODO about itself.
- **Built:** `postman/ia-news-pipeline.postman_collection.json` (collection-level HMAC pre-request script guarded to skip `GET`; 3 requests — happy path, bad signature, job polling — matching AC1/AC4/AC5/AC6 exactly) and `postman/ia-news-pipeline.postman_environment.json` (exactly `base_url` + `PIPELINE_SHARED_SECRET`, matching AC2). `job_id` handoff uses `pm.collectionVariables` per the story's explicit guardrail (not `pm.environment`).
- **HMAC correctness:** verified against all 3 `HmacSigningTests.cs` vectors twice independently — once via Node's built-in `crypto.createHmac`, once via the actual `crypto-js` npm package running the literal `CryptoJS.HmacSHA256(...).toString()` expression used in the shipped script. Both matched all 3 vectors byte-for-byte (64-char lowercase hex each). This is the strongest verification available short of running inside Postman's own sandbox.
- **Live verification:** ran the collection with `newman` (Postman's official CLI runner) against the real `docker compose` `service` container, not just schema validation. All 3 flows passed (202/401/200, 5/5 assertions). Could not additionally prove the happy-path job reaches a terminal state (`published`/`failed`) because the `worker` container fails to start in this environment due to a pre-existing, unrelated Dockerfile defect (`service/IaNewsPipeline.Worker/Dockerfile` uses the ASP.NET-Core-less `dotnet/runtime:10.0` base image instead of `dotnet/aspnet:10.0`) — out of scope to fix here per the story's explicit boundary against touching `service/` source. This gap is disclosed honestly rather than glossed over; AC7's "terminal state observed" clause is the one part of this story not fully closed, and the story is left at `review` (not `done`) so a reviewer can decide whether the Dockerfile fix should land as a follow-up before sign-off.
- README: replaced the single stale `S4.1` forward-reference sentence in the "Testando" section with a pointer to the delivered `postman/` collection (exact text: see the diff in `README.md`'s "## Testando" section, last paragraph before "Fora do escopo"). No other README section touched.
- Did not modify any `service/` file, `.gitignore`, or any other `_bmad-output/implementation-artifacts/*.md` file. Did not `git add`/`git commit` anything — all changes are uncommitted working-tree state per instructions.

### File List

- _bmad-output/implementation-artifacts/4-1-postman-collection.md (new, then modified in this pass: tasks checked, Status → `review`, Dev Agent Record extended, Change Log entry added)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified: `4-1-postman-collection` → `review`, `last_updated` → `2026-07-07`)
- postman/ia-news-pipeline.postman_collection.json (new) — Postman Collection v2.1.0: collection-level HMAC pre-request script + 3 requests (happy path, bad signature, job polling)
- postman/ia-news-pipeline.postman_environment.json (new) — Postman environment with exactly `base_url` and `PIPELINE_SHARED_SECRET`
- README.md (modified) — replaced the stale `S4.1` forward-reference sentence in "## Testando" with a pointer to the delivered `postman/` collection; no other section touched

## Change Log

- 2026-07-07: S4.1 story context created and marked `ready-for-dev`.
- 2026-07-07: Implemented `postman/` collection + environment, fixed the README `S4.1` forward-reference sentence, verified all 3 flows live via `newman` against the real `docker compose` stack (202/401/200, 5/5 assertions passing) and proved the HMAC pre-request script byte-for-byte against the 3 known test vectors using the real `crypto-js` library. Terminal-state polling (AC7) not achieved — blocked by a pre-existing, out-of-scope `worker` container Dockerfile defect discovered during verification (flagged separately, not fixed here). Status moved to `review`.
