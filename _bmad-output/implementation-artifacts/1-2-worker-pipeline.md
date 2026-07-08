---
baseline_commit: 8630f1c39b9cdc2d2e7e35b3c70f913d0b919a00
---

# Story 1.2: Worker pipeline

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a content editor,
I want accepted jobs to be processed asynchronously from queue to published post,
so that article rewriting continues reliably even when extraction, OpenAI, or WordPress are temporarily unstable.

## Acceptance Criteria

1. A .NET `BackgroundService` consumes queued `job_id` messages from the existing SQS-compatible queue, loads the matching job from MySQL, and advances the persisted state through `processing` and `publishing` to either `published` or `failed` exactly as the architecture describes. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/architecture.md#3-service-internals]
2. For a valid queued job, the worker fetches the stored `source_url`, extracts article content with a Readability-style library, requests OpenAI structured output with the JSON fields `title`, `content_html`, and `excerpt`, then HMAC-signs and POSTs the frozen webhook contract from architecture Â§5.1. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/architecture.md#5.1-webhook-service-wordpress]
3. Verification for this story remains independent of Epic 2: the worker must be proven against a contract-Â§5.1 stub webhook that asserts the exact request shape and exercises `201`, `200 duplicate:true`, `401`, and `422` responses without depending on a live WordPress receiver. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#summary-and-recommendations]
4. Failure handling follows architecture Â§3 precisely: transient failures leave the message undeleted so SQS redelivers it later; permanent failures (`invalid URL`, `404`, `non-article/empty extraction`, and contract-invalid webhook payload responses) mark the job `failed` with a clear reason and delete the message; successful publish paths delete the message. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/architecture.md#3-service-internals; _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
5. Structured logs emitted by the worker carry `job_id` on every operational line across receive, fetch, extract, rewrite, publish, retry, and terminal-failure paths. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/architecture.md#3-service-internals; _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
6. The worker correctly handles the stub-unavailable scenario by leaving the message for redelivery, then succeeding once the stub is healthy again; `200 duplicate:true` is treated as idempotent success, not a failure. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]

## Tasks / Subtasks

- [x] Task 1: Turn the worker scaffold into a queue-processing host (AC: 1, 5)
  - [x] Replace the placeholder infinite-delay loop with long-poll receive/process/delete flow around the existing queue.
  - [x] Add worker DI wiring for queue receive/delete, job-store updates, HTTP fetch, extraction, OpenAI call, webhook publish, and structured logging.
  - [x] Ensure every processing scope attaches `job_id` before emitting operational logs.
- [x] Task 2: Add durable job-state transitions around processing and publish (AC: 1, 4, 6)
  - [x] Extend the existing job persistence seam so the worker can transition rows to `processing`, `publishing`, `published`, and `failed`, including `published_post_url` and failure reason.
  - [x] Preserve the `S1.1` intake behavior and the existing `GET /api/jobs/{id}` contract while evolving the store.
  - [x] Make terminal outcomes observable even when downstream delivery fails permanently.
- [x] Task 3: Implement article fetch and extraction boundaries (AC: 2, 4)
  - [x] Fetch `source_url` over HTTP with explicit cancellation/timeouts and classify obvious transport failures as transient vs permanent.
  - [x] Use SmartReader (or the chosen Readability-port implementation) to determine whether the page is article-like; empty/non-article extraction is a permanent failure with a clear reason.
  - [x] Keep URL-shape validation in `S1.1`; `S1.2` owns reachability, HTTP status, and article-readability classification.
- [x] Task 4: Generate structured rewrite output from OpenAI (AC: 2, 4)
  - [x] Request structured JSON output containing exactly `title`, `content_html`, and `excerpt`.
  - [x] Persist enough metadata to later populate the webhook `meta` block, including model identity and generation timestamp.
  - [x] Treat OpenAI/network/platform outages as transient failures; malformed model output that cannot satisfy the schema must be surfaced explicitly and handled deliberately.
- [x] Task 5: Deliver the frozen webhook payload with exact HMAC semantics (AC: 2, 3, 4, 6)
  - [x] Build the exact contract-Â§5.1 JSON body: `job_id`, `source_url`, `title`, `content_html`, `excerpt`, and `meta`.
  - [x] Sign `timestamp.raw_body` exactly as frozen; do not sign parsed JSON or any normalized representation.
  - [x] Interpret stub responses correctly: `201` and `200 duplicate:true` => publish success; `401` and `422` => permanent failure; connection/timeout/5xx => transient failure with redelivery.
- [x] Task 6: Prove the worker independently with focused automated tests (AC: 3, 4, 6)
  - [x] Add worker-centric tests for happy path, duplicate replay success, stub unavailable then success on retry, invalid URL/404, non-article extraction, and webhook `401`/`422`.
  - [x] Keep verification independent from Epic 2 by using a stub webhook and test doubles around queue receipt/deletion and OpenAI/extraction seams.
  - [x] Keep existing `S1.1` tests green; this story must not regress the intake API contract.

## Dev Notes

### Why this story exists

- `S1.1` established the intake contract and persistence boundary; `S1.2` is the story that turns queued jobs into published posts and therefore closes the main FR1/FR2/FR5 service path. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.1; _bmad-output/planning-artifacts/epics-stories.md#S1.2; _bmad-output/planning-artifacts/prd.md#5-functional-requirements]
- PRD NFR1 is the center of gravity here. The worker is where retries, idempotent publish semantics, and permanent-vs-transient classification become real behavior instead of design intent. [Source: _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]

### Scope boundaries (do NOT build)

- Do not change the frozen contracts in architecture Â§5. If the webhook shape or signature rule feels wrong, that is a `correct-course` problem, not a worker reinterpretation. [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- Do not depend on the real WordPress plugin for story verification. `S2.1` already exists now, but this story remains intentionally verifiable against a stub so the Epic 1 workstream stays independently testable. Real end-to-end belongs to `S4.3`. [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#findings-by-severity; _bmad-output/planning-artifacts/epics-stories.md#S4.3]
- Do not spend this story on README, Postman, or UI/theme work; those belong to Epic 4 and Epic 3. [Source: _bmad-output/planning-artifacts/epics-stories.md#Epic-3-theme--parallel-workstream-c; _bmad-output/planning-artifacts/epics-stories.md#Epic-4-delivery-closes-the-day]
- Do not try to solve the entire `S1.3` test matrix here. Add the tests needed to keep the worker safe and independently verifiable, then leave broader unit hardening to `S1.3`. [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.3; _bmad-output/planning-artifacts/architecture.md#8-test-strategy]

### Previous story intelligence

- `1.1` already created the queue-first intake seam, MySQL-backed job storage, HMAC helper, and the `jobs` state vocabulary. Reuse those seams; do not rebuild intake contracts or invent a second job lifecycle representation. [Source: _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md#completion-notes-list; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/JobStates.cs]
- `1.1` review findings matter directly here: queue code must keep dual-mode SQS compatibility, and request-signing logic must stay exact. Do not duplicate or "simplify" those behaviors in a divergent worker-only implementation. [Source: _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md#review-findings]
- `1.1` intentionally left `fail closed` behavior when enqueueing fails and preserved worker scaffolding for this story. Build forward from that observable model instead of bypassing the queue or adding synchronous shortcuts. [Source: _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md#implementation-guardrails; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Services/JobIntakeService.cs]

### Architecture compliance

- The architecture target is one service topology with Minimal API plus `BackgroundService`, but the repo still carries separate API and Worker projects from scaffolding. For this story, prefer the cheapest additive evolution that preserves the current structure and keeps the future merge path open; do not re-template the solution or perform a broad project reorganization. [Source: _bmad-output/planning-artifacts/architecture.md#1-topology; _bmad-output/implementation-artifacts/0-2-repo-scaffolding-ci.md]
- All runtime configuration remains env-only: `OPENAI_API_KEY`, `SQS_ENDPOINT`, `QUEUE_NAME`, `WP_WEBHOOK_URL`, `PIPELINE_SHARED_SECRET`, and `MYSQL_CONNECTION`. Do not add side config files or hidden secrets stores for the worker path. [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals; C:/Repos/ia-news-pipeline/.env.example]
- Receiver idempotency is owned by WordPress, but the service must still interpret `200 duplicate:true` as successful terminal publish and stop retrying. Exactly-once at the system boundary depends on that behavior. [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log; _bmad-output/planning-artifacts/architecture.md#5.1-webhook-service-wordpress]

### Files likely UPDATED in this story

- `service/IaNewsPipeline.Worker/Program.cs`
- `service/IaNewsPipeline.Worker/Worker.cs`
- `service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj`
- `service/IaNewsPipeline.Api/Jobs/IJobStore.cs`
- `service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs`
- `service/IaNewsPipeline.Api/Jobs/JobRecord.cs`
- `service/IaNewsPipeline.Api/Jobs/JobStates.cs` only if a truly missing state/field is discovered; avoid churn otherwise
- `service/IaNewsPipeline.Api/Queueing/IJobQueue.cs`
- `service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs`
- `service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj`
- `service/IaNewsPipeline.Tests/GeneratePostApiTests.cs` only as needed to preserve `S1.1` compatibility
- `docker-compose.yml` if the local stack needs explicit worker runtime wiring beyond the current API container
- `.env.example` only if this story introduces a genuinely new required env var (prefer not to)

### Files likely NEW in this story

- Worker-side services for queue receive/delete orchestration, article fetch/extraction, OpenAI rewrite, webhook delivery, and failure classification
- Shared DTOs for the webhook payload and/or structured rewrite result if the existing API contracts are not the right home
- Focused worker tests and test stub helpers for webhook/OpenAI/extraction behavior

### Current state of UPDATE candidates

- `service/IaNewsPipeline.Worker/Program.cs` only registers the placeholder `Worker`. This is the natural insertion point for real worker DI, typed `HttpClient` setup, and queue-processing services. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Program.cs]
- `service/IaNewsPipeline.Worker/Worker.cs` currently logs that queue polling arrives in Epic 1 and then waits forever. Replace this loop; do not stack a second loop elsewhere and leave the placeholder running. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Worker.cs]
- `service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj` currently references only `Microsoft.Extensions.Hosting` `10.0.9`. Any OpenAI, SmartReader, AWS SQS, or HTTP dependencies added here should be intentional and minimal because this project becomes the worker baseline for the rest of Epic 1. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj]
- `service/IaNewsPipeline.Api/Jobs/IJobStore.cs` currently supports `CreateQueuedJobAsync`, `GetJobAsync`, and `MarkFailedAsync` only. `S1.2` needs additive state-transition methods rather than a second persistence path. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/IJobStore.cs]
- `service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs` currently persists `queued` jobs and failures and reads by id. Extend it carefully so `GET /api/jobs/{id}` continues to reflect the worker's later state transitions. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs]
- `service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs` currently supports enqueue only. `S1.2` likely needs receive/delete (and possibly visibility-extension) capabilities while preserving the existing enqueue behavior and dual-mode endpoint logic. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs]
- `service/IaNewsPipeline.Api/Program.cs` already exposes the intake endpoints and should not absorb worker business logic casually. Touch it only if a small shared registration/refactor is necessary. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs]
- `service/IaNewsPipeline.Tests/GeneratePostApiTests.cs` already protects the `S1.1` contract. Keep it green; if a refactor for shared seams breaks these tests, the refactor is wrong or incomplete. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/GeneratePostApiTests.cs]
- `docker-compose.yml` currently wires an API `service` container but no explicit worker runtime. If local proof needs a dedicated worker process, add it narrowly and preserve the existing MySQL, ElasticMQ, WordPress, and API behaviors. [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]

### Implementation guardrails

- Do not process a queue message without first loading the corresponding job row. Missing-job scenarios should be explicit and observable, not silent no-ops.
- Distinguish failure classes deliberately:
  - Transient: queue/API/OpenAI/network timeout, connection reset, 5xx, stub unavailable.
  - Permanent: invalid URL reaching this stage unexpectedly, HTTP 404 from source fetch, non-article/empty extraction, webhook `401`, webhook `422`.
- Preserve raw-body HMAC semantics exactly for the outgoing webhook. The worker must sign the exact JSON body bytes it transmits.
- Reuse state strings from `JobStates`; do not hardcode parallel literals in multiple places.
- Treat `duplicate:true` as success and update the job to `published` with the returned `post_url`; retries must not turn idempotency into a false failure.
- Keep OpenAI output constrained to the story's schema. Do not widen the rewrite payload shape beyond `title`, `content_html`, `excerpt`, and the contract's metadata block.
- Keep the worker independently verifiable. If a test requires a live WordPress plugin to pass, the test belongs in `S4.3`, not this story.

### Testing requirements

- Automated coverage for this story should include:
  - happy path through stub `201`
  - idempotent replay through stub `200 duplicate:true`
  - stub unavailable / transient failure followed by retry success
  - invalid URL or unexpected fetch rejection
  - source `404`
  - non-article or empty extraction
  - webhook `401`
  - webhook `422`
- `dotnet test service/IaNewsPipeline.sln` remains mandatory.
- Verification should assert exact contract shape for the outgoing webhook request, including headers and serialized body fields, not only that "a POST happened".
- If you add a compose-backed smoke for the worker, keep it stub-based and deterministic; do not make story completion depend on external internet fetches or a real OpenAI call.

### Latest tech information

- Microsoft Learn's current guidance for hosted background services recommends resolving scoped work inside `BackgroundService` rather than embedding all stateful work in the singleton hosted service itself. That favors a thin loop plus scoped processor pattern for queue jobs. [Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service]
- AWS documentation says SQS long polling is enabled when `ReceiveMessage` waits more than `0`, with a maximum wait of `20` seconds, and it reduces empty responses. Use long polling instead of a tight receive loop. [Source: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html]
- AWS documentation also notes that visibility timeout begins when a message is delivered and must cover processing time; if processing may exceed the queue default, the design should account for visibility management instead of assuming infinite processing windows. [Source: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html]
- SmartReader's current NuGet package is `0.11.0` and documents `Article.IsReadable` plus collected errors for failed fetch/extraction scenarios, which maps well to this story's permanent-vs-transient classification needs. [Source: https://www.nuget.org/packages/SmartReader]
- OpenAI's current Structured Outputs guidance recommends defining a JSON Schema so responses adhere to the requested shape. For this story, that is the safest path to keep `title`, `content_html`, and `excerpt` stable for downstream webhook delivery. [Source: https://developers.openai.com/api/docs/guides/structured-outputs]

### Git intelligence summary

- Recent commits show `S1.1` was implemented first and then hardened via review follow-up (`041a818`, `631cf12`). This story should build on that code, not replace it with a parallel implementation path. [Source: `git log -5 --oneline`]
- The repo currently has unrelated uncommitted work outside Epic 1. Keep `S1.2` scoped to its own story files and service runtime changes; do not "clean up" neighboring files opportunistically. [Source: `git status --short`]

### Project Structure Notes

- No UX artifact exists and that is expected for this backend story.
- Prefer additive structure under `service/` and shared seams over root-level reorganization.
- If you need shared code between API and Worker, choose the smallest extraction that prevents duplication without destabilizing the current solution layout.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.3]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S4.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#1-topology]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/architecture.md#5.1-webhook-service-wordpress]
- [Source: _bmad-output/planning-artifacts/architecture.md#8-test-strategy]
- [Source: _bmad-output/planning-artifacts/prd.md#5-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md]
- [Source: _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md]
- [Source: _bmad-output/implementation-artifacts/0-2-repo-scaffolding-ci.md]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Program.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Worker.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/IJobStore.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Services/JobIntakeService.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/GeneratePostApiTests.cs]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [Source: C:/Repos/ia-news-pipeline/.env.example]
- [Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service]
- [Source: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html]
- [Source: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html]
- [Source: https://www.nuget.org/packages/SmartReader]
- [Source: https://developers.openai.com/api/docs/guides/structured-outputs]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Story target was user-specified as `1-2-worker-pipeline`, so no sprint auto-discovery ambiguity remained.
- No `project-context.md` file was present in the repository during story creation.
- No UX artifact was found under `_bmad-output/planning-artifacts`; treated as expected for a backend worker story.
- `1-1-intake-api-job-store` was read as the direct predecessor and source of implementation learnings.
- The readiness artifact's remediation for `S1.2` stub-based verification was incorporated directly into this story.
- Initial compose validation exposed a real publish conflict on duplicated `appsettings*.json` outputs between the Worker template and the referenced API project; worker-local appsettings were excluded from publish output and the image build was revalidated.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Verification independence from Epic 2 is explicit: use a contract-Â§5.1 stub webhook for all story-level proof.
- Update candidates and current code state were documented to reduce refactor drift during implementation.
- Replaced the placeholder worker with a real `BackgroundService` loop that long-polls the queue, resolves scoped processing services, and preserves `job_id` scope across receive, fetch, extract, rewrite, publish, retry, and terminal failure logs.
- Extended the shared queue/job seams so the pipeline can receive/delete messages and persist `processing`, `publishing`, `published`, and `failed` outcomes while preserving the existing intake and `GET /api/jobs/{id}` contract from `S1.1`.
- Added worker services for source fetch classification, SmartReader extraction, structured OpenAI rewrite, exact raw-body HMAC signing, and WordPress webhook publishing against the frozen architecture §5.1 contract.
- Added stub-driven worker tests that cover happy path, duplicate replay success, transient webhook outage with retry success, invalid URL, source `404`, non-article extraction, and webhook `401`/`422`, while keeping the `S1.1` API tests green.
- Added a dedicated worker Dockerfile and a `worker` service in `docker-compose.yml` so the local stack can host the asynchronous processor explicitly.
- Validation commands and outcomes:
  - `dotnet build service/IaNewsPipeline.sln` passed.
  - `dotnet test service/IaNewsPipeline.sln` passed (`19/19`).
  - `docker compose config` passed after wiring the `worker` service.
  - `docker compose build worker` initially failed on duplicate publish `appsettings*.json` outputs and then passed after excluding worker-local appsettings from publish.
- Residual note: restore/build still emits the pre-existing low-severity `AWSSDK.Core` advisory warning (`NU1901`), but it did not block the story.

### File List

- _bmad-output/implementation-artifacts/1-2-worker-pipeline.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docker-compose.yml
- service/IaNewsPipeline.Api/Jobs/IJobStore.cs
- service/IaNewsPipeline.Api/Jobs/JobRecord.cs
- service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs
- service/IaNewsPipeline.Api/Queueing/IJobQueue.cs
- service/IaNewsPipeline.Api/Queueing/QueuedJobMessage.cs
- service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs
- service/IaNewsPipeline.Tests/GeneratePostApiTests.cs
- service/IaNewsPipeline.Tests/WorkerPipelineTests.cs
- service/IaNewsPipeline.Worker/Dockerfile
- service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj
- service/IaNewsPipeline.Worker/Program.cs
- service/IaNewsPipeline.Worker/Services/ExtractedArticle.cs
- service/IaNewsPipeline.Worker/Services/ExtractionResult.cs
- service/IaNewsPipeline.Worker/Services/FetchResult.cs
- service/IaNewsPipeline.Worker/Services/HttpSourceFetcher.cs
- service/IaNewsPipeline.Worker/Services/IArticleExtractor.cs
- service/IaNewsPipeline.Worker/Services/IOpenAiRewriteClient.cs
- service/IaNewsPipeline.Worker/Services/ISourceFetcher.cs
- service/IaNewsPipeline.Worker/Services/IWebhookPublisher.cs
- service/IaNewsPipeline.Worker/Services/JobMessageProcessor.cs
- service/IaNewsPipeline.Worker/Services/OpenAiOptions.cs
- service/IaNewsPipeline.Worker/Services/OpenAiRewriteClient.cs
- service/IaNewsPipeline.Worker/Services/PublishResult.cs
- service/IaNewsPipeline.Worker/Services/RewriteResult.cs
- service/IaNewsPipeline.Worker/Services/RewrittenPost.cs
- service/IaNewsPipeline.Worker/Services/SmartReaderArticleExtractor.cs
- service/IaNewsPipeline.Worker/Services/WebhookOptions.cs
- service/IaNewsPipeline.Worker/Services/WebhookPublishRequest.cs
- service/IaNewsPipeline.Worker/Services/WebhookSignatureService.cs
- service/IaNewsPipeline.Worker/Services/WordPressWebhookPublisher.cs
- service/IaNewsPipeline.Worker/Worker.cs

## Change Log

- 2026-07-07: Story created and contexted for development. Status -> ready-for-dev.
- 2026-07-07: Implemented the async worker pipeline, added stub-based worker tests, wired the compose worker runtime, and moved status to `review`.

