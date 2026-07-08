---
baseline_commit: 99c5445
---

# Story 4.3: Blind QA + fixes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Michel (the evaluator, primary persona),
I want an isolated reviewer — fresh context, acceptance criteria only, no access to any implementer's reasoning — to run the real `docker compose up` stack and behaviorally verify every PRD acceptance criterion (AC1–AC9), including a live kill-WordPress/recovery drill for AC3, fix whatever it finds broken, and leave a written report,
so that the pipeline's claimed engineering maturity is **proven by execution**, not asserted by a README — closing the delivery day the way `prd.md` §9 ("Blind QA: acceptance criteria verified against the running stack by an isolated reviewer") and `README.md`'s own "QA cega" line promise it will.

## Acceptance Criteria

This story's job is to verify all 9 PRD acceptance criteria behaviorally against the live stack, fix any genuine defects found, and document the outcome. Each PRD AC below is both the **target being verified** and — once verified — a completion gate for this story: this story is not `done` until every one of them passes for real or carries a documented, accepted exception in the QA report.

1. **AC1 — End-to-end happy path.** Given the stack is up via `docker compose up`, when a valid public article URL is submitted to `POST /api/generate-post` (HMAC-signed per architecture §5.2), then within 2 minutes a new post with an AI-generated title and body is published and visible on the WordPress site at `http://localhost:8080`. [Source: prd.md#7-acceptance-criteria-behavioral AC1; architecture.md#5.2-service-intake-caller-service]
2. **AC2 — Observable status to terminal state.** Given a submitted request, `GET /api/jobs/{id}` lets the caller observe job state progression (`queued → processing → publishing → published|failed`) until a terminal state is reached. [Source: prd.md AC2; architecture.md#3-service-internals-net; service/IaNewsPipeline.Api/Jobs/JobStates.cs]
3. **AC3 — WordPress-outage recovery drill, exactly once.** Given WordPress is temporarily unavailable when generation completes (`docker compose stop wordpress` mid-flight, per the drill already scripted in `README.md#Testando` step 5), when it becomes available again (`docker compose start wordpress`), then the post is eventually published without manual intervention and **exactly once** — no duplicate post from the SQS redelivery that occurred during the outage. [Source: prd.md AC3; architecture.md D2 (SQS retry/DLQ), D3 (idempotency owned by the receiver)]
4. **AC4 — Webhook auth rejection.** Given a webhook call to `POST /wp-json/ia-pipeline/v1/posts` without the correct shared-secret HMAC signature, then no post is created and the response indicates rejection (`401`). [Source: prd.md AC4; architecture.md#5.1-webhook-service-wordpress; wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php]
5. **AC5 — Invalid/unreachable URL fails cleanly.** Given an invalid or unreachable URL submitted to `POST /api/generate-post`, then the request reaches a `failed` state with a clear reason (`invalid_url`, `source_not_found`, or `source_not_article`), and no post is created. [Source: prd.md AC5; architecture.md#3-service-internals-net (failure classification)]
6. **AC6 — README is self-sufficient.** Given a fresh clone, `README.md` alone (no other source of instructions) is sufficient to configure, run, and test the whole pipeline — verified by following it **literally**, step by step, as an evaluator with no prior context would. [Source: prd.md AC6]
7. **AC7 — Themed single-post rendering.** The published post page renders with the custom Bootstrap theme; the single-post layout (`single.php`) is the visual centerpiece — readable measure, excerpt lead, source-attribution block linking `source_url`, "AI-generated" badge, responsive at mobile width. Verify this against a post the pipeline itself created in AC1 (not a manually seeded one — S3.2 already covered the manual-post case; this story closes the real pipeline-to-theme path S3.2 explicitly deferred). [Source: prd.md AC7; epics-stories.md#S3.2 "pipeline-created post verification belongs to S4.3"; wp-theme/ia-news-theme/single.php]
8. **AC8 — CI green, tests cover the right things.** CI (`.github/workflows/dotnet-test.yml`) runs on push and is green; `dotnet test service/IaNewsPipeline.sln` passes and its suite covers extraction, payload construction, and retry/idempotency logic (not placeholder assertions). [Source: prd.md AC8; .github/workflows/dotnet-test.yml]
9. **AC9 — README content completeness.** `README.md` contains: architecture + decisions (with links to the frozen artifacts), an executive summary (delivered / risks / next steps, semáforo style), out-of-scope items each with a one-line production path, and a cost-aware production section using AWS vocabulary (SQS, ECS, Bedrock, cost-per-token). [Source: prd.md AC9]
10. **Findings are fixed, not just logged.** Any genuine defect found while verifying AC1–AC9 is fixed in this story (in the same working tree), re-verified, and the fix is recorded — this story's scope explicitly includes remediation, unlike prior stories' code reviews which could only flag issues out of scope. [Source: epics-stories.md#S4.3 "Findings fixed"]
11. **QA report delivered.** A QA report is saved to `_bmad-output/implementation-artifacts/qa-report.md`, listing each AC1–AC9, its verification method, its pass/fail outcome, and — for any AC not fully passing — either the fix that closed it or an explicit, justified, accepted exception. [Source: epics-stories.md#S4.3 "QA report saved to..."]
12. **Done bar.** All 9 PRD ACs pass, or each non-passing one carries a documented, accepted exception in `qa-report.md` — there is no silent gap. [Source: epics-stories.md#S4.3 "Done when"]

## Tasks / Subtasks

- [ ] Task 1: Establish the isolated-reviewer posture before touching any implementation reasoning (AC: methodology, all)
  - [ ] Read only `prd.md` §7 (Acceptance Criteria) as the verification target — do not re-derive expectations from `architecture.md`'s prose beyond what's needed to exercise the system (endpoints, ports, contract shapes already summarized in this story's Dev Notes below); the point of "isolated reviewer" is verifying against stated behavior, not re-litigating implementation choices already frozen and reviewed in prior stories
  - [ ] Note the starting git state (`git log --oneline -3`, `git status`) so this story's own diff is traceable
- [ ] Task 2: Bring up the real stack from a clean-ish state and confirm baseline health (AC: 1, 2, 6)
  - [ ] Follow `README.md`'s "Como rodar" section literally: `cp .env.example .env` (fill `OPENAI_API_KEY` and `PIPELINE_SHARED_SECRET`), `docker compose up` (or `up -d --build` to pick up any local Dockerfile changes)
  - [ ] Confirm `wp-init` exits 0 (success, not failure — README calls this out explicitly), all 5 services reach a running state (`mysql`, `elasticmq`, `service`, `worker`, `wordpress`), and note any step in the README that does not work as literally written — that is itself an AC6 finding
- [ ] Task 3: Verify AC1 + AC2 — happy path end to end, with status polling to a terminal state (AC: 1, 2)
  - [ ] Use the `postman/` collection (S4.1, already delivered and verified) to drive `POST /api/generate-post` with a real, publicly reachable article URL — `PublicUrlValidator` rejects localhost/private IPs, so the URL must resolve publicly; `newman` (`npx newman run postman/ia-news-pipeline.postman_collection.json -e postman/ia-news-pipeline.postman_environment.json --env-var "PIPELINE_SHARED_SECRET=<value from .env>"`) is the fastest repeatable path, proven to work in the 4-1 story's own verification
  - [ ] Poll `GET /api/jobs/{id}` (via the collection's "Get Job Status" request or directly) until a terminal state is reached; confirm it reaches `published` within 2 minutes of submission, not just any terminal state — `failed` does not satisfy AC1
  - [ ] Confirm the resulting post is visible on `http://localhost:8080` with the AI-rewritten title/body
- [ ] Task 4: Verify AC3 — the WordPress-outage recovery drill (AC: 3)
  - [ ] Follow `README.md#Testando` step 5 literally: submit a signed URL, then `docker compose stop wordpress` before the webhook POST lands (timing this requires either a fast trigger right after submission or temporarily stopping WordPress before submitting at all — either demonstrates the same retry mechanism); observe via worker logs (`docker compose logs -f worker`) that delivery attempts fail and the job does **not** reach a terminal state while WordPress is down
  - [ ] `docker compose start wordpress`; observe the worker's next SQS redelivery (bounded by the visibility timeout — check `docker/elasticmq.conf` for the configured value) successfully deliver the webhook and the job reach `published`
  - [ ] Confirm **exactly one** post was created for that `job_id` — check WordPress admin or query the post by `_pipeline_job_id` meta — not two, even though the webhook was attempted more than once. This exercises architecture D3 (idempotency via `_pipeline_job_id` meta check in `wp-pipeline-receiver`) under real retry conditions, which no prior story tested end-to-end.
  - [ ] **Known risk to watch:** `_bmad-output/implementation-artifacts/deferred-work.md` documents that SQS redelivery is capped at `maxReceiveCount: 5` before a message moves to the DLQ, and nothing currently marks a job `failed` when that happens — if the WordPress outage in this drill lasts long enough to exhaust 5 redeliveries, the job will silently stay `processing`/`publishing` forever with no failure surfaced. Keep the outage window short enough to stay within 5 redelivery attempts, but note in the QA report if the outage window used in the drill risked or hit this gap — this is exactly the kind of edge a blind reviewer should stress, not avoid.
- [ ] Task 5: Verify AC4 and AC5 — the two failure-path ACs (AC: 4, 5)
  - [ ] AC4: use the collection's "Generate Post — Bad Signature" request (or hand-craft a webhook call to `POST /wp-json/ia-pipeline/v1/posts` with a wrong/missing signature) — confirm `401` and confirm no post was created
  - [ ] AC5: submit an invalid URL (malformed) and confirm `400` at intake; submit a URL that 404s or is unreachable and confirm the job reaches `failed` with reason `invalid_url` / `source_not_found`; if feasible, submit a URL that resolves but is not article content (e.g. a search results page or homepage) and confirm `source_not_article` — this closes the "non-article content" branch the implementation-readiness report flagged as only implicitly covered (m1)
- [ ] Task 6: Verify AC6 — literal README walkthrough (AC: 6)
  - [ ] Treat `README.md` as the *only* instruction source; if any step is missing, wrong, or requires undocumented tribal knowledge (e.g. the MySQL-volume schema-init gap already discovered once during 4-1's own verification — a **stale local Docker volume** predating `docker/mysql-init/01-pipeline-schema.sql` needing manual schema application — note whether a genuinely fresh `docker compose down -v && docker compose up` still hits this, since a truly clean volume should not), record it as an AC6 finding and fix the README (or the underlying script) so the next fresh clone does not hit it
- [ ] Task 7: Verify AC7 — pipeline-created post renders correctly on the theme (AC: 7)
  - [ ] Using the actual post published in Task 3 (real pipeline output, not a manually seeded one — S3.2 explicitly deferred this real-post check here), confirm on `http://localhost:8080/?p=<id>` (or its permalink): constrained readable content width (not full-bleed), excerpt rendered as a styled lead paragraph, source-attribution block present and linking the original `source_url`, "AI-generated" badge visible, layout holds at 375px mobile width (resize/dev-tools check)
- [ ] Task 8: Verify AC8 — CI status and test substance (AC: 8)
  - [ ] Confirm the latest push's GitHub Actions run for `.github/workflows/dotnet-test.yml` is green (or run `dotnet test service/IaNewsPipeline.sln` locally against the current tree as a proxy if Actions isn't inspectable from this environment)
  - [ ] Skim `service/IaNewsPipeline.Tests/` for real assertions on extraction, payload building, and retry/idempotency (not empty/placeholder tests) — S1.3 already delivered this; confirm it's still true, don't re-implement
- [ ] Task 9: Verify AC9 — README content completeness (AC: 9)
  - [ ] Literal read-through of `README.md` checking for: architecture + decisions section with links, executive summary (semáforo: delivered/risks/next-steps), out-of-scope table with one-line production paths, cost-aware production section using AWS vocabulary (SQS/ECS/Bedrock/cost-per-token) — all already present per S4.2's delivery; confirm nothing has drifted since S4.2/S4.1 landed
- [ ] Task 10: Fix every genuine finding from Tasks 2–9 (AC: 10)
  - [ ] For each defect found (not stylistic nitpicks — genuine AC-blocking or AC-risking issues), apply the smallest correct fix in the relevant component (`service/`, `wp-plugin/`, `wp-theme/`, `docker/`, `README.md`, `docker-compose.yml`, etc.)
  - [ ] Re-run the specific verification step for that AC after the fix to confirm it now passes — do not just assume the fix works
  - [ ] Re-run `dotnet test service/IaNewsPipeline.sln` after any `service/` code change to confirm no regression (this repo is at 45/45 green as of the last recorded baseline; that number should not go down)
- [ ] Task 11: Write `_bmad-output/implementation-artifacts/qa-report.md` (AC: 11, 12)
  - [ ] One row/section per AC1–AC9: verification method used, outcome (pass/fail→fixed/documented exception), and evidence (command run, observed response, screenshot description, or log excerpt)
  - [ ] Explicitly call out AC3's redelivery-window risk (Task 4) and AC7's real-pipeline-post verification as the two checks no prior story could close
  - [ ] List every fix applied in Task 10 with the file(s) touched, so the report doubles as a change log for this story
  - [ ] State the final verdict: all 9 ACs pass, or name the specific AC(s) with a documented, accepted exception and why it's acceptable for a 1-day POC
- [ ] Task 12: Close out story bookkeeping (AC: all)
  - [ ] Update this story's Status, Dev Agent Record, File List, Change Log
  - [ ] Update `sprint-status.yaml`: `4-3-blind-qa-fixes` → `review` (or `done` if run via an autonomous dev-story pass that also self-closes), and consider whether `epic-4` should move to `done` once this lands (it's the last story in the last epic)

## Dev Notes

### Why this story exists — and why it's genuinely last

- This is the final story in the entire project. Every other story (`0-1`, `0-2`, `1-1`, `1-2`, `1-3`, `2-1`, `3-1`, `3-2`, `4-1`, `4-2`) is `done` in `sprint-status.yaml`. Three prior stories (`S1.2`, `S3.2`) *deliberately deferred* their real end-to-end verification to this story — re-read their "Done when" language: S1.2 says "Real end-to-end verification belongs to S4.3"; S3.2 says "pipeline-created post verification belongs to S4.3." **This story is where those two IOUs come due**, not optional polish. [Source: epics-stories.md#S1.2; epics-stories.md#S3.2]
- The PRD's own delivery plan names this exact structure: "Blind QA: acceptance criteria above verified against the running stack by an isolated reviewer." [Source: prd.md#9-delivery-plan]. `README.md` already tells this story to the evaluator before it's even been executed — "**QA cega** permanece como fechamento de `S4.3`; o relatório final planejado ficará em `qa-report.md`" — so the deliverable path (`qa-report.md`) is a promise already made in a `done` artifact; this story keeps it.

### The pipeline has already run end-to-end once — use that, don't assume it, don't re-litigate it

- A real infrastructure bug (wrong Docker base image on the worker) was found and fixed in commit `11a208d` (`fix(worker): use aspnet base image so the worker container actually starts`) during the *previous* story's (`4-1`) code review, one commit before this story was authored. Root cause: `service/IaNewsPipeline.Worker/Dockerfile`'s final stage used `mcr.microsoft.com/dotnet/runtime:10.0` (no ASP.NET Core shared framework), but the Worker project transitively references ASP.NET Core hosting types (via the Api project) — the container crash-looped on every prior local run. Fixed by switching to `mcr.microsoft.com/dotnet/aspnet:10.0`, matching the pattern already used by `IaNewsPipeline.Api/Dockerfile`.
- That commit's own verification is the **first recorded proof this pipeline completes a full API → queue → worker → MySQL round trip**: a signed job was accepted (`202`), picked up by the worker, fetched its source URL for real, and reached a terminal `failed` state with a legitimate reason (`source_not_found` — the test URL used wasn't a real article). `dotnet test` stayed at 45/45.
- **What this means for this story:** the worker container itself is no longer presumed broken — do not re-diagnose "does the worker start" as if from scratch. But note precisely what was proven and what wasn't: a `failed`-terminal-state round trip was observed once, with a URL that 404s. **A genuine `published` terminal state (AC1's actual bar) has not yet been recorded by any prior story.** This story is where AC1 gets its first real, positive proof — go in expecting the stack to *work*, but verify the full happy path yourself rather than assuming it because a `failed` path once worked. Do not pre-judge the outcome either way.

### The `postman/` collection is a ready-made verification tool, not just a deliverable to check off

- S4.1 delivered and verified (including in its own code review) a Postman collection (`postman/ia-news-pipeline.postman_collection.json`) and environment (`postman/ia-news-pipeline.postman_environment.json`) with three working flows: happy path (asserts `202`, captures `job_id`), bad signature (asserts `401`), and job polling (asserts `200`). It computes the HMAC signature automatically via a collection-level pre-request script — use it instead of hand-computing signatures for AC1/AC2/AC4 verification.
- `newman` (Postman's CLI runner) was proven to work in this exact environment during 4-1's verification: `npx newman run postman/ia-news-pipeline.postman_collection.json -e postman/ia-news-pipeline.postman_environment.json --env-var "PIPELINE_SHARED_SECRET=<secret>"`. Reuse this pattern for fast, scriptable re-verification after any fix in Task 10, rather than manually re-computing HMAC signatures by hand each time.
- The collection does **not** cover AC3 (WordPress-outage drill), AC5's non-article-content branch, AC6 (README literal walkthrough), AC7 (theme rendering), AC8 (CI), or AC9 (README content) — those need direct verification per Tasks 4–9.

### Architecture compliance — exact shapes to verify against

- **Contract (frozen, architecture §5):** service intake `POST /api/generate-post` (HMAC-signed, `202`/`400`/`401`) and `GET /api/jobs/{id}` (unauthenticated, `200`/`404`); webhook `POST /wp-json/ia-pipeline/v1/posts` (HMAC-signed, `201`/`200 duplicate:true`/`401`/`422`). Do not accept a response shape that deviates from these without treating it as a finding. [Source: architecture.md#5-contract-frozen]
- **Ports (docker-compose.yml defaults):** WordPress `http://localhost:8080`, service `http://localhost:8081` (maps to container `8080`), ElasticMQ `9324`. [Source: docker-compose.yml]
- **Job states:** `queued → processing → publishing → published | failed`. [Source: architecture.md#3-service-internals-net; service/IaNewsPipeline.Api/Jobs/JobStates.cs]
- **Idempotency mechanism (D3):** the WordPress plugin (`wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php`) checks post meta `_pipeline_job_id` before insert; a repeat delivery for the same `job_id` returns `200` with `duplicate: true` instead of creating a second post. This is the mechanism AC3's "exactly once" claim depends on — verify it under genuine retry conditions (Task 4), not just by re-POSTing the same payload manually (which S2.1 already did in its own story).
- **Post meta to check for AC7:** `_pipeline_job_id`, `source_url`, model — set by the receiver on publish, read by `single.php` for the source-attribution block. [Source: architecture.md#4-wordpress-side]
- **Redelivery timing risk (real, documented, not hypothetical):** `_bmad-output/implementation-artifacts/deferred-work.md` records that SQS/ElasticMQ redrive is capped at `maxReceiveCount: 5` (architecture D2) and nothing currently marks a job `failed` once a message exhausts redelivery and lands in the DLQ — a job stuck in transient failure forever stays silently `processing`/`publishing` via `GET /api/jobs/{id}`. This is explicitly *not* a defect to fix as part of this story unless it actually blocks AC3 in your drill (it's flagged as an Epic 1 hardening candidate, out of this story's frozen scope) — but if your outage window in Task 4 is long enough to hit it, that observation belongs in the QA report as a risk note, and the drill should be re-run with a shorter outage window to actually demonstrate AC3's "eventually published" claim rather than stall there.
- **`PublicUrlValidator` rejects localhost/private IPs.** Any URL used in Tasks 3–5 for the happy path must be a real, publicly resolvable article; only the "invalid/unreachable" AC5 checks should intentionally use bad URLs. [Source: service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs]

### Scope boundaries — what this story should and should not touch

- Fix only genuine AC-blocking or AC-risking defects found during verification (Task 10). Do not use this story as a vehicle for unrelated refactors, style cleanup, or the deferred-work.md DLQ-sweep item (explicitly out of scope, flagged for a later hardening story) unless it directly blocks an AC.
- Do not re-implement or duplicate what S4.1 (Postman collection) and S1.3 (unit tests) already verified — reuse them as tools, per Dev Notes above, rather than re-authoring equivalent checks.
- `qa-report.md` is new; do not fold its content into `README.md` — `README.md`'s "QA cega" line already points at `qa-report.md` as a separate file, and rewriting README structure here risks regressing S4.2's already-verified AC6/AC9.

### Testing requirements

- This story's own "test suite" *is* the AC1–AC9 verification against the live stack — there is no separate automated test to add for the QA process itself. Any code fix applied in Task 10 must be covered by the existing `dotnet test` suite if it touches `service/`; do not skip re-running `dotnet test service/IaNewsPipeline.sln` after any service-side fix.
- If a fix changes `service/` behavior in a way not covered by an existing test, add a minimal, meaningful test (not a placeholder) — consistent with NFR5 and AC8's "no placeholder" bar already established by S1.3.

### File structure requirements

- New: `_bmad-output/implementation-artifacts/qa-report.md` — the story's primary deliverable beyond fixes.
- Modified (only as needed by genuine findings): any file under `service/`, `wp-plugin/`, `wp-theme/`, `docker/`, `docker-compose.yml`, `.env.example`, or `README.md`. Do not touch `postman/` unless a genuine defect is found in it (S4.1 already reviewed it clean).
- Modified: `_bmad-output/implementation-artifacts/sprint-status.yaml` (status transition) and this story file (Dev Agent Record, Change Log).

### Previous story intelligence

- **No `4-2-*.md` story file exists** — S4.2 (README) was completed directly (commit `776e155`, `docs(readme): close story 4.2 draft`) without going through this create-story/dev-story flow, so there is no in-epic immediate-predecessor file to load per the workflow's usual rule. The nearest prior Epic 4 story file with a full Dev Agent Record is `4-1-postman-collection.md` (`done`), used throughout this story's Dev Notes above.
- From `4-1-postman-collection.md`'s Dev Agent Record: a **stale local MySQL Docker volume** (predating `docker/mysql-init/01-pipeline-schema.sql`, which is a newer file than the volume) caused a `500` on `POST /api/generate-post` because `pipeline.jobs` didn't exist — fixed manually against the running container in that story, not by any tracked-file change. **This is exactly the kind of gap Task 6 (AC6 literal README walkthrough) should catch**: if a genuinely fresh `docker compose down -v && docker compose up` (destroying the volume) still doesn't provision the schema correctly, that is a real AC6/AC2 defect (all requests would `500`), not an environment quirk to work around silently.
- From `4-1`'s review findings: HMAC signing, response shapes, and the `postman/` collection are all independently verified correct against the frozen contract — no need to re-verify HMAC correctness from scratch in this story; trust it and focus verification effort on the ACs no prior story could close (AC1's `published` terminal state, AC3, AC7 with a real pipeline post).

### Git intelligence summary

- Recent commits: `99c5445` (docs: close code review for 4-1), `11a208d` (fix: worker Dockerfile — see Dev Notes above), `8425d78` (feat: S4.1 postman), `5315bf6` (test: S1.3 unit tests + extraction fix), `49c939e` (fix: S1.2 review follow-ups), `776e155` (docs: S4.2 readme close), `66f7dd4` (feat: orchestrator agent), `024960a` (chore: foundation review fixes), `e5efed8` (S1.2 worker), `2f8db7a` (S3.2 theme). All three parallel workstreams and both Epic 4 predecessor stories are closed; this is the only remaining item before the project is complete. [Source: `git log --oneline -15`]
- `baseline_commit` for this story is `99c5445` (current `HEAD` at story-creation time).

### References

- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/planning-artifacts/prd.md#9-delivery-plan]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S4.3]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals-net]
- [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md (m1: non-article branch)]
- [Source: _bmad-output/implementation-artifacts/4-1-postman-collection.md]
- [Source: _bmad-output/implementation-artifacts/deferred-work.md]
- [Source: C:/Repos/ia-news-pipeline/README.md#Como-rodar]
- [Source: C:/Repos/ia-news-pipeline/README.md#Testando]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [Source: C:/Repos/ia-news-pipeline/.env.example]
- [Source: C:/Repos/ia-news-pipeline/.github/workflows/dotnet-test.yml]
- [Source: C:/Repos/ia-news-pipeline/postman/ia-news-pipeline.postman_collection.json]
- [Source: C:/Repos/ia-news-pipeline/postman/ia-news-pipeline.postman_environment.json]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/JobStates.cs]
- [Source: C:/Repos/ia-news-pipeline/wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php]
- [Source: commit 11a208d "fix(worker): use aspnet base image so the worker container actually starts"]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (story creation pass)

### Debug Log References

- Story auto-targeted by explicit key `4-3-blind-qa-fixes` (last remaining story; confirmed all other 10 stories `done` in `sprint-status.yaml` before proceeding).
- `epic-4` was already `in-progress` (S4.1/S4.2 both closed before this story), so no epic-status transition was needed.
- No `4-2-*.md` file exists in `implementation-artifacts/` — S4.2 (README) was closed directly via commit, not through this workflow; fell back to `4-1-postman-collection.md` as the nearest prior Epic 4 story for "previous story intelligence."
- No `project-context.md` found anywhere under the project root.
- Confirmed `_bmad-output/implementation-artifacts/qa-report.md` does not yet exist — this story creates it fresh.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- This story intentionally does not prescribe exact fix implementations for Task 10 findings — genuine QA can't be scripted in advance; the story instead over-specifies *how to verify* each AC precisely (exact commands, exact states, exact meta fields) so the executing agent recognizes a real defect versus a red herring.
- Flagged the `deferred-work.md` DLQ/redelivery-cap risk explicitly against AC3, since a naive kill-WordPress drill with too long an outage window would appear to "hang" for a reason unrelated to AC3 itself — this prevents a false failure report.
- Flagged that AC1's `published` terminal state and AC7's real-pipeline-post rendering have literally never been observed by any prior story (only a `failed` terminal state has been recorded, in 4-1's review) — this story is where the strongest, previously-unproven claims get their first real test.
- Fresh-stack blind QA was executed live with `docker compose down -v` + `docker compose up -d --build`, and the stack came up cleanly with `wp-init` exiting `0`.
- The first live happy-path attempt exposed a real blocker: OpenAI returned `openai_http_400` for oversized extracted material. Fixed in `OpenAiRewriteClient` by stripping HTML and truncating source material before calling `chat/completions`; added a unit test, and `dotnet test service/IaNewsPipeline.sln` moved from `45/45` to `46/46` green.
- AC3 was proven under a real outage drill: with WordPress down, the job reached `publishing`; after restart, it eventually published exactly once, confirmed by `_pipeline_job_id` in MySQL.
- README/Postman guidance was corrected so the documented happy path uses a real verified `article_url` instead of the guaranteed-failure `https://example.com/article` placeholder.

### File List

- _bmad-output/implementation-artifacts/4-3-blind-qa-fixes.md (new)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified: `4-3-blind-qa-fixes` → `ready-for-dev`, `last_updated`)
- _bmad-output/implementation-artifacts/qa-report.md (new)
- service/IaNewsPipeline.Worker/Services/OpenAiRewriteClient.cs (modified)
- service/IaNewsPipeline.Tests/OpenAiRewriteClientTests.cs (new)
- README.md (modified)
- postman/ia-news-pipeline.postman_collection.json (modified)
- postman/ia-news-pipeline.postman_environment.json (modified)

## Change Log

- 2026-07-07: S4.3 story context created and marked `ready-for-dev`. Captured that S1.2 and S3.2 both deliberately deferred their real end-to-end verification to this story; that the worker container's Docker-base-image bug was fixed in `11a208d` and produced the first-ever full round-trip proof (to a `failed` state, not yet `published`); that `postman/`'s newman-verified collection is available as a verification tool; and the exact AC1–AC9 verification method for each criterion, including the AC3 kill-WordPress drill and its documented DLQ-redelivery-cap risk.
- 2026-07-07: Executed live blind QA against the real compose stack, fixed the OpenAI oversized-prompt happy-path failure, corrected README/Postman happy-path guidance to use a verified article URL, delivered `qa-report.md`, and closed AC1–AC9 with live evidence. Status moved to `done`.
