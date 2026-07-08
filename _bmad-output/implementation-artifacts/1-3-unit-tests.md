---
baseline_commit: 8630f1c39b9cdc2d2e7e35b3c70f913d0b919a00
---

# Story 1.3: Unit tests

Status: backlog (blocked-by-S1.2)

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> **Blocked.** This story exercises code that does not exist yet: extraction normalization, outbound webhook payload building, HMAC signing, and transient/permanent classification are all owned by `S1.2`, which is `ready-for-dev` and currently being implemented. Do not run `DS` against this story until `S1.2` reaches `done`. Once it does, re-validate every file path and seam name below against `S1.2`'s actual File List before starting Task work — this draft was written ahead of that implementation and will drift.

## Story

As a developer maintaining the generation service,
I want isolated unit tests for extraction normalization, webhook payload building, HMAC signing, and failure classification,
so that the worker's core decision logic is protected by fast, deterministic tests independent of the stub-based worker integration tests in S1.2.

## Acceptance Criteria

1. Extraction normalization logic (SmartReader output → normalized article fields, empty/non-article detection) has unit tests covering at least one article-like input and at least one empty/non-article input, asserting the exact classification produced. [Source: epics-stories.md#S1.3; architecture.md#8-test-strategy]
2. Outbound webhook payload building has unit tests asserting the exact JSON shape from architecture §5.1 (`job_id`, `source_url`, `title`, `content_html`, `excerpt`, `meta`), independent of any HTTP call. [Source: epics-stories.md#S1.3; architecture.md#5.1-webhook-service-wordpress]
3. HMAC signing has unit tests against fixed, documented test vectors (see Dev Notes → Known HMAC test vectors below), proving `sha256=<hex HMAC-SHA256(secret, timestamp + "." + raw_body)>` byte-for-byte, not an approximation. [Source: epics-stories.md#S1.3; architecture.md#5.1-webhook-service-wordpress]
4. Transient-vs-permanent failure classification has unit tests for each documented case: transient (timeout, connection reset, 5xx, stub unavailable) vs permanent (invalid URL reaching this stage, source 404, non-article/empty extraction, webhook 401, webhook 422), asserting the classification decision in isolation from any real network call. [Source: epics-stories.md#S1.3; architecture.md#3-service-internals; 1-2-worker-pipeline.md#Implementation-guardrails]
5. Idempotent replay handling has a unit test proving that a webhook response of `200` with `duplicate: true` is classified as terminal success (not retried, not failed), isolated from the stub HTTP transport. [Source: epics-stories.md#S1.3; architecture.md#2-decision-log D3]
6. `dotnet test service/IaNewsPipeline.sln` is green in CI with meaningful assertions — no placeholder/`Assert.True(true)`-style tests. [Source: epics-stories.md#S1.3]

## Tasks / Subtasks

- [ ] Task 0: Reconcile this draft against the real S1.2 implementation (AC: all)
  - [ ] Re-read `S1.2`'s File List and Completion Notes once status is `done`.
  - [ ] Update every class/seam name referenced below to match what S1.2 actually shipped; do not invent parallel seams if S1.2 already exposes a testable one.
- [ ] Task 1: Unit tests for extraction normalization (AC: 1)
  - [ ] Test the pure function/seam that maps SmartReader's `Article` result to the normalized fields the worker uses downstream.
  - [ ] Test the empty/non-article path returns a permanent-classification signal, not an exception swallowed silently.
- [ ] Task 2: Unit tests for webhook payload building (AC: 2)
  - [ ] Test payload construction in isolation from HTTP — assert the serialized JSON field set and types match architecture §5.1 exactly.
  - [ ] Test that `meta` carries model identity and generation timestamp as S1.2 persists them.
- [ ] Task 3: Unit tests for HMAC signing against known vectors (AC: 3)
  - [ ] Add the three fixed vectors from Dev Notes as `[Theory]/[InlineData]` (or equivalent) against the outbound-signing function.
  - [ ] Assert raw-body byte semantics: signing must use the exact transmitted body bytes, not a re-serialized/normalized copy.
- [ ] Task 4: Unit tests for transient-vs-permanent classification (AC: 4)
  - [ ] Cover every case enumerated in `1-2-worker-pipeline.md` Implementation guardrails as an isolated table/decision test, not an end-to-end stub call.
- [ ] Task 5: Unit test for idempotent replay handling (AC: 5)
  - [ ] Isolate the response-interpretation function (`201` / `200 duplicate:true` / `401` / `422` / transport failure → outcome) and test each branch directly.
- [ ] Task 6: Confirm no duplication with S1.2 Task 6 (AC: 6)
  - [ ] Diff this story's test file list against S1.2's worker-integration tests; if a scenario is covered end-to-end there and in isolation here, keep both only if they protect genuinely different failure modes (see Dev Notes → Test boundary matrix).

## Dev Notes

### Why this story exists

- Architecture §8 explicitly separates unit-level coverage (extraction normalization, payload building, HMAC signing, retry/idempotency decision logic) from the Blind QA behavioral pass. S1.3 is the story that fulfills that unit-level line item. [Source: architecture.md#8-test-strategy]
- `S1.2`'s Dev Notes deliberately defer "the entire S1.3 test matrix" to this story while still shipping worker-integration tests of its own (Task 6). Without an explicit boundary, the two stories' test suites overlap or leave gaps. [Source: 1-2-worker-pipeline.md#Scope-boundaries]

### Scope boundaries (do NOT build)

- Do not re-test the full worker orchestration loop (queue receive → delete, retry-via-redelivery, end-to-end stub flow) — that is S1.2 Task 6's job. This story tests the decision/transformation functions in isolation, not the `BackgroundService` loop.
- Do not depend on a running stub webhook, ElasticMQ, or MySQL for these tests. If a test needs any of those running, it belongs to S1.2's integration suite, not here.
- Do not touch the frozen contract in architecture §5. If a payload-building test reveals a contract ambiguity, that is a `correct-course` discussion.

### Test boundary matrix — S1.2 Task 6 vs S1.3

| Scenario | S1.2 Task 6 (worker integration, via stub) | S1.3 (this story, isolated unit) |
|---|---|---|
| Happy path publish | ✅ full loop: receive → fetch → extract → rewrite → sign → POST → state update → delete | ✅ payload shape + signature only, no HTTP |
| Duplicate replay (`200 duplicate:true`) | ✅ worker ends in `published` state, message deleted | ✅ response-interpretation function returns "success" for this input |
| Stub unavailable → retry → success | ✅ message survives, redelivery observed | ❌ out of scope (requires real redelivery timing) |
| Invalid URL / source 404 | ✅ job ends `failed`, message deleted | ✅ classification function returns "permanent" for these inputs |
| Non-article/empty extraction | ✅ job ends `failed` | ✅ normalization function flags non-article directly |
| Webhook `401` / `422` | ✅ job ends `failed`, message deleted | ✅ classification function returns "permanent" for these status codes |
| HMAC signature correctness | Implicitly exercised (stub would reject bad signatures) | ✅ explicit known-vector proof, byte-for-byte |

Rule of thumb: if a test needs the worker's queue loop or a live HTTP transport to fail, it's S1.2's. If it needs only a function's input/output, it's S1.3's.

### Known HMAC test vectors

Computed independently (PowerShell `HMACSHA256`, not copied from any existing code path) against the frozen rule `sha256=<hex HMAC-SHA256(secret, timestamp + "." + raw_body)>`. [Source: architecture.md#5.1-webhook-service-wordpress]

**Vector 1** — realistic §5.1 payload
- `secret`: `test-secret`
- `timestamp`: `1735689600`
- `raw_body`: `{"job_id":"11111111-1111-1111-1111-111111111111","source_url":"https://example.com/article","title":"Test Title","content_html":"<p>Test content.</p>","excerpt":"Test excerpt.","meta":{"model":"gpt-test","generated_at":"2026-07-07T12:00:00Z"}}`
- `signature` (hex, 64 chars): `dd05ab6bb07f6e6476127d59417f122c26f53b6c5c056af4e633161150e0af8d`

**Vector 2** — empty JSON body
- `secret`: `another-secret-value`
- `timestamp`: `1735689601`
- `raw_body`: `{}`
- `signature` (hex, 64 chars): `556ccd0e81a8438238935d73683faf78d9e2737a19c332bc4633bdfdfe3f14a6`

**Vector 3** — edge case: empty secret, single-char body
- `secret`: `` (empty string)
- `timestamp`: `1735689602`
- `raw_body`: `x`
- `signature` (hex, 64 chars): `ded049e65f8ff6087e43cef7459606765bbe69ff089308c0c05475a36cf0657a`

Regenerate independently before trusting blindly: `HMAC-SHA256(UTF8(secret), UTF8("{timestamp}.{raw_body}"))`, hex-encoded, lowercase, no `sha256=` prefix in the stored constant (add the prefix only when constructing the header).

### Previous story intelligence

- `S1.1` established the HMAC pattern for the **inbound** intake endpoint only (`service/IaNewsPipeline.Tests/GeneratePostApiTests.cs:170-173`, `HmacRequestValidator`). That code path verifies a signature; S1.3 needs the **outbound** signing path (worker → WP), which does not exist yet and is `S1.2`'s to build. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/GeneratePostApiTests.cs; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs]
- `S1.1` review findings established a precedent: URL validation gaps were only caught because tests asserted exact rejected cases, not just "some failure happened." Apply the same standard to classification tests here — assert the specific transient/permanent verdict, not just pass/fail. [Source: 1-1-intake-api-job-store.md#Review-Findings]

### Files likely UPDATED or NEW in this story

Cannot be finalized until `S1.2` ships — its Dev Notes list worker-side services for extraction, OpenAI rewrite, webhook delivery, and failure classification as new files, but exact class/method names are implementation-time decisions. [Source: 1-2-worker-pipeline.md#Files-likely-NEW-in-this-story]

Expected shape once S1.2 lands:
- New test file(s) under `service/IaNewsPipeline.Tests/` targeting the isolated functions (e.g. an extraction-normalization test file, a payload/signing test file, a classification test file) — exact filenames depend on S1.2's actual class boundaries.
- Likely no production code changes, unless S1.2 leaves classification/payload-building logic embedded inline in the worker loop instead of as separately testable seams — if so, Task 0 should flag that as a prerequisite refactor, not silently work around it.

### Testing requirements

- `dotnet test service/IaNewsPipeline.sln` remains mandatory and must stay green alongside S1.1 and S1.2 tests.
- Every assertion must be specific (exact classification, exact hex signature, exact JSON field set) — no placeholder tests.
- These tests must not require Docker, MySQL, ElasticMQ, or a running stub webhook. If a test needs any of those, it has drifted into S1.2's territory.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.3]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/architecture.md#5.1-webhook-service-wordpress]
- [Source: _bmad-output/planning-artifacts/architecture.md#8-test-strategy]
- [Source: _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md]
- [Source: _bmad-output/implementation-artifacts/1-2-worker-pipeline.md]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/GeneratePostApiTests.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs]

## Dev Agent Record

### Agent Model Used

Claude (Amelia persona) — analysis and prep only, no production/test code written.

### Debug Log References

- Drafted ahead of `S1.2` completion at the user's explicit request, while a separate agent implements `S1.2` concurrently.
- Confirmed via `service/IaNewsPipeline.Worker/Worker.cs` that no extraction/rewrite/webhook code exists yet — this story's Tasks 1-5 cannot start until that lands.
- HMAC vectors computed independently via PowerShell `HMACSHA256`, verified for exact 64-hex-char length before recording.

### Completion Notes List

- Story file created in `backlog` (not `ready-for-dev`) because the dependency (`S1.2`) is unimplemented; `sprint-status.yaml` intentionally left unchanged.
- Test boundary matrix written to prevent S1.2 Task 6 and S1.3 from duplicating or leaving gaps in worker test coverage.
- Known HMAC test vectors are ready to drop into `[Theory]/[InlineData]` once the outbound signing function exists.

### File List

- _bmad-output/implementation-artifacts/1-3-unit-tests.md (new)

## Change Log

- 2026-07-07: Draft story created ahead of S1.2 completion, at user's request, to prepare test vectors and scope boundary while S1.2 is implemented concurrently. Status left at `backlog` (blocked).
