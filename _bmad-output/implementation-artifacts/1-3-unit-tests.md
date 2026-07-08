---
baseline_commit: 8630f1c39b9cdc2d2e7e35b3c70f913d0b919a00
---

# Story 1.3: Unit tests

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> **Unblocked 2026-07-07.** `S1.2` reached `review` status with a real `SmartReaderArticleExtractor`, `WordPressWebhookPublisher`, `WebhookSignatureService`, and `JobMessageProcessor` shipped under `service/IaNewsPipeline.Worker/Services/`. Task 0 reconciliation against S1.2's actual File List is complete (see Dev Agent Record below); no invented parallel seams were needed except one production bug fix documented under Seam/production changes.

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

- [x] Task 0: Reconcile this draft against the real S1.2 implementation (AC: all)
  - [x] Re-read `S1.2`'s File List and Completion Notes (status was `review`, not yet `done`, but the code and its 19/19 passing worker-integration tests were fully shipped — proceeded on that basis).
  - [x] Confirmed every class/seam name below against the real `service/IaNewsPipeline.Worker/Services/*.cs` files before writing any test: `SmartReaderArticleExtractor`, `WordPressWebhookPublisher`, `WebhookSignatureService`, `HttpSourceFetcher`, `JobMessageProcessor`, and their associated result records (`ExtractionResult`, `PublishResult`, `FetchResult`) all matched the story's assumptions. No invented parallel seams were needed.
- [x] Task 1: Unit tests for extraction normalization (AC: 1)
  - [x] `ArticleExtractionTests.cs` tests `SmartReaderArticleExtractor.ExtractAsync` directly against a real article-like HTML literal and a real empty/non-article HTML literal (no mocking of SmartReader itself — the real library runs).
  - [x] Empty/non-article path asserted to return `ExtractionResult.PermanentFailure("source_not_article")`, not a swallowed exception.
- [x] Task 2: Unit tests for webhook payload building (AC: 2)
  - [x] `WebhookPayloadTests.cs` tests `WordPressWebhookPublisher.PublishAsync` with an in-memory fake `HttpMessageHandler` (no real HTTP/network) and asserts the exact top-level JSON field set (`job_id`, `source_url`, `title`, `content_html`, `excerpt`, `meta`) and exact `meta` field set (`model`, `generated_at`) match architecture §5.1, with no extra/missing fields.
  - [x] Asserted `meta.model` and `meta.generated_at` (ISO-8601 `O` format) match what the request carries.
- [x] Task 3: Unit tests for HMAC signing against known vectors (AC: 3)
  - [x] `HmacSigningTests.cs` adds all three fixed vectors as `[Theory]/[InlineData]` against `WebhookSignatureService.Compute`. All three were independently re-verified via PowerShell `HMACSHA256` before trusting them (byte-for-byte match).
  - [x] Added an explicit raw-body-byte-semantics test proving two semantically-equal-but-textually-different JSON bodies produce different signatures (i.e. the exact transmitted bytes are signed, not a normalized re-serialization).
- [x] Task 4: Unit tests for transient-vs-permanent classification (AC: 4)
  - [x] `FailureClassificationTests.cs` covers, as isolated `[Theory]`/`[Fact]` decision tests against `HttpSourceFetcher` and `WordPressWebhookPublisher` (fake handler, no live network): source 404/403/410 (permanent), source 5xx/timeout/connection-reset (transient), source empty body (permanent), webhook 401/422 (permanent), webhook 5xx/timeout/connection-reset (transient). Non-article/empty extraction is covered in `ArticleExtractionTests.cs` (AC1) rather than duplicated here. Invalid-URL-reaching-the-worker is a one-line `Uri.TryCreate` guard inside `JobMessageProcessor` with no independent decision logic to isolate; it remains covered by `WorkerPipelineTests.cs` (S1.2) per the test boundary matrix.
- [x] Task 5: Unit test for idempotent replay handling (AC: 5)
  - [x] `IdempotentReplayTests.cs` isolates `WordPressWebhookPublisher.PublishAsync`'s response-interpretation branch: `200 duplicate:true` → success (with a `201 duplicate:false` companion case proving both land on the identical success branch), and a `200` missing `post_url` → permanent failure (`webhook_missing_post_url`), proving "duplicate" isn't a fragile special case.
- [x] Task 6: Confirm no duplication with S1.2 Task 6 (AC: 6)
  - [x] Reviewed `WorkerPipelineTests.cs` scenario-by-scenario against the new S1.3 files: every new S1.3 test targets a single class in isolation (extractor, fetcher, publisher, signature service) via literal inputs or a fake in-memory `HttpMessageHandler`, never the full `JobMessageProcessor` loop, queue, or job store — matching the story's own boundary matrix. `dotnet test service/IaNewsPipeline.sln` is green: 45/45 (19 baseline + 26 new), no placeholder assertions anywhere in the new files.

### Review Findings

- [x] [Review][Note] Independently re-verified the story's central claims rather than trusting the Dev Agent Record at face value: (1) recomputed all three HMAC-SHA256 vectors from this file's "Known HMAC test vectors" table via PowerShell `System.Security.Cryptography.HMACSHA256`, byte-for-byte match on all three; (2) wrote and ran a throwaway `[Fact]` comparing `SmartReader.Reader.ParseArticle(url, html)` against `new Reader(url, html).GetArticle()` against the same article-like HTML literal used in `ArticleExtractionTests.cs` — confirmed the static helper returns `IsReadable=False, ContentLen=0` while the instance API returns `IsReadable=True, ContentLen=758`, substantiating the production bug-fix rationale. The throwaway test was deleted after verification. [service/IaNewsPipeline.Worker/Services/SmartReaderArticleExtractor.cs:11]
- [x] [Review][Note] Traced `IdempotentReplayTests.cs`'s `200 duplicate:true` case against the actual control flow in `JobMessageProcessor.ProcessAsync` (not just the publisher in isolation): a `PublishResult` with `IsSuccess=true` drives `MarkPublishedAsync` + `queue.DeleteAsync` (JobMessageProcessor.cs:100-107), while `IsTransient=true` leaves the message on the queue for redelivery (JobMessageProcessor.cs:110-124) and `IsSuccess=false, IsTransient=false` deletes the message as a terminal failure. The test's assertions (`IsSuccess=true`, `IsTransient=false`) land precisely on the branch that both stops retries and does not fail the job, which is what AC5 requires — not a weaker "didn't throw" check. [service/IaNewsPipeline.Tests/IdempotentReplayTests.cs:44-46]
- [x] [Review][Note] Ran a full Blind Hunter / Edge Case Hunter / Acceptance Auditor pass against all 5 new test files, `TestSupport/StubHttpMessageHandler.cs`, and the production diff in `SmartReaderArticleExtractor.cs`: no tautological assertions, no `Assert.True(true)`-style placeholders, no resource leaks (the fake handler opens no real sockets), no timing/ordering-dependent flakiness, and every AC1–AC6 has a test asserting the specific classification/value rather than pass/fail. `FailureClassificationTests.cs`'s documented exclusions (invalid-URL and non-article/empty extraction, covered elsewhere) were checked against `WorkerPipelineTests.cs` and `ArticleExtractionTests.cs` respectively and confirmed present, not silently dropped. No functional defects found; no patch was necessary. `dotnet test service/IaNewsPipeline.sln`: 45/45 green both before and after this review (no production or test code was changed by the review).

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

Claude Sonnet 5 (Amelia persona), unattended test-first implementation pass.

### Debug Log References

- Baseline `dotnet test service/IaNewsPipeline.sln` before any change: 19/19 passing (S1.1 + S1.2 suites), matching S1.2's own recorded result.
- Read every relevant S1.2 file under `service/IaNewsPipeline.Worker/Services/` before writing tests (Task 0), per the story's own instruction not to guess signatures.
- **Real bug found and fixed via red-green-refactor**: writing AC1's article-like-input test first (red) surfaced that `SmartReaderArticleExtractor.cs` called the static `SmartReader.Reader.ParseArticle(url, html)` helper, which throws an internal `FormatException` for every input in SmartReader 0.11.0 (the exception is swallowed into `Article.Errors` rather than propagated, so it fails silently and `IsReadable` always comes back `false`). This means article extraction has never actually worked in this codebase — S1.2's own tests never caught it because `WorkerPipelineTests.cs` exercises a `StubExtractor`, never the real `SmartReaderArticleExtractor`. Verified deterministically with two isolated throwaway `[Fact]`s (since deleted) directly comparing `Reader.ParseArticle(url, html)` against `new Reader(url, html).GetArticle()` on identical input: the static helper always returned `IsReadable=false, ContentLen=0`, the instance API always returned the correct, populated article. Fixed by switching `SmartReaderArticleExtractor.ExtractAsync` to the working instance API (`service/IaNewsPipeline.Worker/Services/SmartReaderArticleExtractor.cs:11`), a one-line swap with no other behavior change. After the fix, both `ArticleExtractionTests.cs` cases pass with real (non-trivial article/HTML) inputs.
- HMAC vectors independently re-verified via PowerShell's own `System.Security.Cryptography.HMACSHA256` before being trusted in `HmacSigningTests.cs`; all three matched the story's recorded hex byte-for-byte.
- Environment note: the worktree originally assigned for this task (`agent-a9add5ddb1c5982e9`) was pinned to a stale commit (`ac5c0e8`, pre-dating all Epic 1 implementation — no `service/` directory existed). Fast-forwarded that worktree's branch to the then-current local `main` tip (`66f7dd4`, a clean ancestor fast-forward, `git merge --ff-only`) before starting any file work, since the Write/Edit tools are sandboxed to that specific worktree path. All work below happened after that fast-forward.
- Final `dotnet test service/IaNewsPipeline.sln`: 45/45 passing (19 baseline + 26 new: 2 extraction + 5 HMAC + 2 payload + 14 classification + 3 idempotent-replay), including all pre-existing `GeneratePostApiTests.cs` and `WorkerPipelineTests.cs` tests unmodified.

### Completion Notes List

- All 6 ACs have real, assertion-specific unit test coverage; no placeholder/`Assert.True(true)`-style tests were written.
- One production seam change was required and applied: `SmartReaderArticleExtractor.cs` line 11, swapping the broken static `Reader.ParseArticle` call for the working instance `new Reader(...).GetArticle()` call. This is a bug fix, not a contract or behavior redesign — see Debug Log for full justification. Flagged explicitly per the task's transparency requirement since it goes slightly beyond "tests only."
- No other production code changes were needed: `WebhookSignatureService`, `WordPressWebhookPublisher`, and `HttpSourceFetcher` were all directly testable as shipped, using an in-memory fake `HttpMessageHandler` (new shared helper `TestSupport/StubHttpMessageHandler.cs`) to isolate HTTP-client-based classes from any real network call without needing further extraction.
- `WorkerPipelineTests.cs` (S1.2) was read for context and pattern reference but not modified, per the story's scope constraint. A small new shared test helper (`TestSupport/StubHttpMessageHandler.cs`) was added instead of touching that file, exactly as the guardrail suggested.
- The `BuildExcerpt` 280-character-truncation fallback inside `SmartReaderArticleExtractor` (used only when SmartReader's own `Article.Excerpt` is blank) was not separately unit-tested: SmartReader synthesized a non-blank excerpt from the test article's first paragraph in every literal HTML input tried, so that specific fallback branch could not be reliably forced via public API/HTML content alone without a fragile test. This is a minor, documented gap, not a silently dropped requirement — AC1 itself only requires exact classification for one article-like and one non-article input, both of which are covered.
- `GET /api/jobs/{id}`, MySQL, ElasticMQ, Docker, and live OpenAI/WordPress were not touched or required by any new test, per the story's testing requirements.

### File List

- service/IaNewsPipeline.Worker/Services/SmartReaderArticleExtractor.cs (production bug fix — see Debug Log)
- service/IaNewsPipeline.Tests/TestSupport/StubHttpMessageHandler.cs (new — shared in-memory HTTP fake used by AC2/AC4/AC5 tests)
- service/IaNewsPipeline.Tests/ArticleExtractionTests.cs (new — AC1)
- service/IaNewsPipeline.Tests/WebhookPayloadTests.cs (new — AC2)
- service/IaNewsPipeline.Tests/HmacSigningTests.cs (new — AC3)
- service/IaNewsPipeline.Tests/FailureClassificationTests.cs (new — AC4)
- service/IaNewsPipeline.Tests/IdempotentReplayTests.cs (new — AC5)
- _bmad-output/implementation-artifacts/1-3-unit-tests.md (this file — task checkboxes, status, Dev Agent Record, Change Log)
- _bmad-output/implementation-artifacts/sprint-status.yaml (status field updated to `review`)

## Change Log

- 2026-07-07: Draft story created ahead of S1.2 completion, at user's request, to prepare test vectors and scope boundary while S1.2 is implemented concurrently. Status left at `backlog` (blocked).
- 2026-07-07: Implemented all 6 ACs as isolated unit tests (26 new tests across 5 files) against the real S1.2 seams; found and fixed a real extraction bug (broken `SmartReader.Reader.ParseArticle` static call) surfaced by AC1's red-green cycle. `dotnet test` 19/19 → 45/45 green. Status moved to `review`.
- 2026-07-07: Unattended code review completed (Blind Hunter / Edge Case Hunter / Acceptance Auditor). Independently re-verified the production bug-fix claim via a throwaway comparison test and re-derived all three HMAC vectors byte-for-byte; both confirmed accurate. Traced the idempotent-replay test's assertions against the real `JobMessageProcessor` control flow to confirm they prove "not retried, not failed," not just "didn't throw." No correctness defects found in the production diff or the five new test files; no patch, decision, or deferral was required. `dotnet test service/IaNewsPipeline.sln`: 45/45 green, unchanged by the review. Status moved to `done`.
