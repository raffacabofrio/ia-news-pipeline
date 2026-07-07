---
baseline_commit: ff92d1a2d129aa6bc8628b55b21f33c002983080
---

# Story 1.1: Intake API + job store

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a content editor,
I want to submit a public article URL to the generation service and receive a trackable job identifier,
so that the rewrite pipeline can process the request asynchronously and I can later confirm whether it was published or failed.

## Acceptance Criteria

1. `POST /api/generate-post` exists in the .NET service and enforces the frozen contract from architecture §5.2: same HMAC header scheme, JSON body `{ "url": "https://..." }`, and `202` response body `{ "job_id": "...", "status_url": "/api/jobs/..." }`. [Source: epics-stories.md#S1.1; architecture.md#3-service-internals; architecture.md#5.2-service-intake-caller-service]
2. Requests with invalid or missing HMAC signature are rejected with `401`; invalid request body or invalid URL shape are rejected with `400`. The response behavior must match the S1.1 "Done when" definition exactly. [Source: epics-stories.md#S1.1; architecture.md#2-decision-log; architecture.md#5.2-service-intake-caller-service]
3. A jobs table is created in the `pipeline` schema and supports at least the state progression needed by the service contract: `queued`, `processing`, `publishing`, `published`, `failed`, plus storage for `source_url`, failure reason, and published post URL. [Source: architecture.md#1-topology; architecture.md#3-service-internals]
4. On accepted intake, the service inserts a job row in state `queued`, enqueues the `job_id` onto the configured SQS-compatible queue, and returns `202` only after both operations succeed. No accepted request may be silently lost between persistence and queue dispatch. [Source: epics-stories.md#S1.1; prd.md#6-non-functional-requirements; architecture.md#3-service-internals]
5. `GET /api/jobs/{id}` returns `200` with `{ "job_id": "...", "state": "...", "post_url"?: "...", "error"?: "..." }` for known jobs and `404` for unknown jobs, following architecture §5.2. [Source: architecture.md#3-service-internals; architecture.md#5.2-service-intake-caller-service]
6. Story scope stays intentionally narrow: no article extraction, no OpenAI call, no webhook post to WordPress, and no queue-consuming worker behavior beyond preserving the existing scaffold. Those behaviors remain for S1.2 and later. [Source: epics-stories.md#S1.2; epics-stories.md#S1.3]

## Tasks / Subtasks

- [x] Task 1: Add the intake API contract to the existing Minimal API host (AC: 1, 2, 5)
  - [x] Replace the scaffold-only API surface with route handlers for `POST /api/generate-post` and `GET /api/jobs/{id}` while preserving a basic health endpoint for container/runtime diagnostics.
  - [x] Parse and validate the HMAC headers `X-Pipeline-Timestamp` and `X-Pipeline-Signature` using the shared secret from env/config and reject invalid signatures with `401`.
  - [x] Validate JSON payload shape and absolute public URL format; return `400` for invalid body or malformed URL input without touching the database or queue.
- [x] Task 2: Introduce job persistence in the `pipeline` schema (AC: 3, 4, 5)
  - [x] Add SQL initialization for a `jobs` table under `docker/mysql-init/` or an equivalent startup-safe mechanism that works from a clean `docker compose up`.
  - [x] Create a thin data-access layer in `service/` for inserting a queued job and reading a job by id.
  - [x] Ensure the job record shape can already support later S1.2 transitions (`processing`, `publishing`, `published`, `failed`) without forcing a breaking schema rewrite.
- [x] Task 3: Enqueue accepted jobs to the configured queue (AC: 4)
  - [x] Add queue client wiring against the architecture's SQS-compatible decision (`SQS_ENDPOINT`, `QUEUE_NAME`) so the same code works with ElasticMQ now and real SQS later.
  - [x] Send the `job_id` after the row is persisted and fail the request if enqueueing does not succeed.
  - [x] Keep the message body intentionally small and stable; do not send the full generated payload in this story.
- [x] Task 4: Make the service runnable inside the local stack (AC: 1, 4, 5)
  - [x] Add the `service` container path the architecture already expects in `docker-compose.yml`, using the existing MySQL/ElasticMQ network and env conventions.
  - [x] Add any missing Dockerfile/project wiring required to expose the API from compose without changing WordPress, ElasticMQ, or MySQL behavior.
  - [x] Preserve the current queue defaults (`pipeline-jobs`, `pipeline-jobs-dlq`) and MySQL dual-schema bootstrap from Epic 0.
- [x] Task 5: Add focused tests for intake behavior (AC: 1, 2, 4, 5)
  - [x] Add service tests that prove valid intake returns `202`, bad signature returns `401`, invalid body returns `400`, and unknown job returns `404`.
  - [x] Prefer test seams around HMAC verification, request validation, and persistence/queue orchestration; do not wait for S1.3 to add the first real behavior tests if they are needed to keep this story safe.

### Review Findings

- [x] [Review][Patch] Queue adapter hardcodes fake AWS credentials, so the "same code works with ElasticMQ now and real SQS later" contract is not actually met [service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs:13]
- [x] [Review][Patch] Public URL validation accepts non-public IP ranges such as `0.0.0.0` and IPv6 ULA (`fc00::/7`), so invalid intake URLs can still be accepted instead of returning `400` [service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs:22]
- [x] [Review][Patch] The test suite does not cover the rejected-address cases above, so the URL-validation contract can regress without detection [service/IaNewsPipeline.Tests/GeneratePostApiTests.cs:73]

## Dev Notes

### Why this story exists

- Epic 1 is the Generation Service workstream. S1.1 is the contract-establishing story that turns the scaffolded .NET app into a real intake surface while keeping the worker logic deferred to S1.2. [Source: epics-stories.md#Epic-1-generation-service-net-parallel-workstream-a]
- PRD FR2 requires immediate acknowledgment plus later status lookup, and NFR1 requires that accepted requests are not silently lost. That makes the "job row first, then queue dispatch, then 202" flow the central implementation concern of this story. [Source: prd.md#5-functional-requirements; prd.md#6-non-functional-requirements]

### Scope boundaries (do NOT build)

- Do not implement queue consumption, URL fetching, SmartReader extraction, OpenAI rewriting, webhook POST, retry classification, or DLQ handling in this story. Those all belong to S1.2. [Source: epics-stories.md#S1.2]
- Do not build the substantial unit suite for extraction/payload classification yet; S1.3 owns that. This story may add tactical tests needed to protect the intake API, but not the whole Epic 1 test matrix. [Source: epics-stories.md#S1.3; architecture.md#8-test-strategy]
- Do not alter the frozen JSON contract in architecture §5. If you find a contract issue, that is a `correct-course` discussion, not a code-side reinterpretation. [Source: architecture.md#frozen-v1; architecture.md#5-contract-frozen]

### Previous story intelligence

- S0.2 intentionally kept the API and Worker "boring and thin" so Epic 1 could layer behavior without deleting large amounts of bootstrap code. Extend the scaffold; do not throw it away and re-template the service. [Source: 0-2-repo-scaffolding-ci.md#Tasks--Subtasks; 0-2-repo-scaffolding-ci.md#Dev Notes]
- The service skeleton already targets `net10.0`, already has `AddProblemDetails()`, and already establishes project boundaries for API, Worker, and Tests. Reuse those entry points instead of collapsing everything into one new project. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj]
- S0.1 fixed the local queue and DB conventions. Preserve `SQS_ENDPOINT=http://elasticmq:9324`, `QUEUE_NAME=pipeline-jobs`, and the single-MySQL dual-schema bootstrap. Do not rename queues or introduce a second database service. [Source: 0-1-spike-one-command-environment.md#Completion Notes List; docker-compose.yml; docker/mysql-init/01-pipeline-schema.sql]

### Architecture compliance

- One .NET process hosts both API and Worker as a POC decision, but the current repo still has separate API and Worker projects. For this story, implement the intake contract in the API project and preserve compatibility with the later convergence decision instead of fighting the current scaffold. That means any new shared logic should live in reusable classes, not only inside route lambdas. [Source: architecture.md#1-topology; architecture.md#2-decision-log D1; architecture.md#3-service-internals]
- `GET /api/jobs/{id}` is intentionally unauthenticated in the POC. Do not add auth/rate limiting as custom scope here, but keep the design clean enough for later hardening. [Source: architecture.md#5.2-service-intake-caller-service]
- Every service configuration value must still come from env vars only: `OPENAI_API_KEY`, `SQS_ENDPOINT`, `QUEUE_NAME`, `WP_WEBHOOK_URL`, `PIPELINE_SHARED_SECRET`, `MYSQL_CONNECTION`. Even though this story does not use all of them yet, it must not invent parallel configuration sources. [Source: architecture.md#3-service-internals; .env.example]
- Logging should already move toward the architecture rule that every line carries `job_id`; at minimum, the orchestration path created in this story should attach `job_id` to logs emitted after the row exists. [Source: architecture.md#3-service-internals; prd.md#6-non-functional-requirements]

### Files likely UPDATED in this story

- `service/IaNewsPipeline.Api/Program.cs`
- `service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj`
- `service/IaNewsPipeline.Api/appsettings.json` and/or `appsettings.Development.json` only if needed for local defaults, but env-vars remain canonical
- `service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj`
- `service/IaNewsPipeline.Tests/ScaffoldTests.cs` or replacement test files
- `docker-compose.yml`
- `docker/mysql-init/01-pipeline-schema.sql` or a new adjacent SQL file under `docker/mysql-init/`
- `.env.example` only if the compose-served service needs additional documented local defaults beyond the frozen env list

### Files likely NEW in this story

- Service-layer source files for request models, HMAC verification, persistence, queue publishing, and API handlers
- A service Dockerfile or compose-specific build assets if none exist yet
- Additional test files for API and orchestration behavior

### Current state of UPDATE candidates

- `service/IaNewsPipeline.Api/Program.cs` currently exposes only `/` and `/health`, and already calls `AddProblemDetails()`. This is the correct insertion point for the new endpoints; preserve health diagnostics unless there is a compelling reason not to. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs]
- `service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj` currently has no extra packages. Any persistence or SQS dependency added here should be deliberate and minimal because this story establishes the service stack baseline for the rest of Epic 1. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj]
- `service/IaNewsPipeline.Tests/ScaffoldTests.cs` currently proves only project wiring. Replace or extend it with real behavior coverage for intake contract validation; do not leave the story depending entirely on manual testing. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/ScaffoldTests.cs]
- `docker/mysql-init/01-pipeline-schema.sql` currently creates only the empty `pipeline` schema and explicitly says jobs table DDL arrives in S1.1. This file is an intended update point. [Source: C:/Repos/ia-news-pipeline/docker/mysql-init/01-pipeline-schema.sql]
- `docker-compose.yml` currently has no `service` container and explicitly says the .NET service joins in a later story. That "later story" is this one. Add the container without regressing MySQL, ElasticMQ, WordPress, or `wp-init`. [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- `service/IaNewsPipeline.Worker/Program.cs` and `Worker.cs` are placeholders for later stories. Preserve them as scaffolding; do not pull S1.2 behavior forward into this story. [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Program.cs; C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Worker.cs]

### Implementation guardrails

- The most failure-prone part of this story is coordinating database insert and queue send. Because the architecture requires "no accepted request silently lost," do not return `202` if the DB row was inserted but the enqueue failed. If true atomicity is not practical in this story, the implementation must still fail closed and leave an observable state that S1.2/S4.3 can reason about; this tradeoff should be explicit in code comments and tests.
- HMAC verification must follow the frozen header scheme exactly: `X-Pipeline-Timestamp` and `X-Pipeline-Signature: sha256=<hex HMAC-SHA256(secret, timestamp + "." + raw_body)>`. Avoid "equivalent" interpretations such as signing parsed JSON or a normalized body. Use the raw request body bytes/string. [Source: architecture.md#5.2-service-intake-caller-service]
- URL validation should reject obviously invalid or non-absolute input at intake time, but deep reachability classification belongs to S1.2. Keep the boundary clean: syntax/shape here, fetch/extract later.
- Keep queue message payload minimal. The architecture only requires enqueueing `job_id` at this stage; storing and later reading job context from MySQL reduces duplication and avoids contract drift.

### Testing requirements

- Local verification should include `dotnet test` and, if feasible inside the story implementation, a compose-backed smoke check that `POST /api/generate-post` returns `202` against the running stack.
- Add automated tests around the exact status code matrix mandated by S1.1: `202`, `400`, `401`, `404`. Tests should assert the contract shape, not just that "some non-success code" happened.
- If you introduce abstractions for queue/database orchestration, test the failure path where enqueueing fails after request validation. This is the highest-value regression to guard in S1.1.

### Latest tech information

- Microsoft Learn currently lists `.NET 10` as a supported LTS release through November 2028, so staying on `net10.0` is aligned with current platform support and there is no reason to downgrade this story to an older target. [Source: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support]
- Microsoft Learn's current Minimal API error-handling guidance says `AddProblemDetails()` should be paired with middleware such as `UseExceptionHandler()` and `UseStatusCodePages()` if you want framework-generated problem details for unhandled errors and empty error responses. The current scaffold only registers the service; the dev agent should decide whether to complete that setup while keeping the contract's explicit `400`/`401`/`404` bodies intact. [Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0]
- AWS's current SDK documentation shows the supported pattern is still the `AWSSDK.SQS` client sending explicit messages to a queue URL. For this story, that reinforces using the standard SQS client abstraction against `SQS_ENDPOINT` rather than inventing an ElasticMQ-only transport. [Source: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/SendMessage.html]

### Git intelligence summary

- Recent project history is still foundation-heavy: S0.1 implemented the stack, S0.2 scaffolded the .NET solution, and the latest commit only adjusted BMad agent guidance. There is no competing service implementation in progress to merge around inside `service/`; this story will set the first real service conventions. [Source: `git log -5 --oneline`]
- The repo is already carrying build outputs under `service/**/bin` and `obj` in the workspace, but `.gitignore` correctly ignores them. The dev agent should continue avoiding generated-file churn and keep changes focused on source, docker, and tests. [Source: C:/Repos/ia-news-pipeline/.gitignore]

### Project Structure Notes

- No UX artifact exists and that is acceptable for this backend story; do not invent UI requirements.
- Favor additive structure under `service/` instead of a root-level reorg. Architecture §6 is already the coordination contract for the parallel workstreams.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.1]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.2]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S1.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#1-topology]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/architecture.md#8-test-strategy]
- [Source: _bmad-output/planning-artifacts/prd.md#5-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
- [Source: _bmad-output/implementation-artifacts/0-1-spike-one-command-environment.md]
- [Source: _bmad-output/implementation-artifacts/0-2-repo-scaffolding-ci.md]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/Program.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Tests/ScaffoldTests.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Program.cs]
- [Source: C:/Repos/ia-news-pipeline/service/IaNewsPipeline.Worker/Worker.cs]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [Source: C:/Repos/ia-news-pipeline/docker/mysql-init/01-pipeline-schema.sql]
- [Source: C:/Repos/ia-news-pipeline/.env.example]
- [Source: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support]
- [Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0]
- [Source: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/SendMessage.html]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Story target was user-specified as `1-1-intake-api-job-store`, so no auto-discovery ambiguity remained.
- No `project-context.md` file was present in the repository during story creation.
- No UX artifact was found under `_bmad-output/planning-artifacts`; treated as expected for a backend service story.
- Full sprint-status was read before mutation, and Epic 1 was promoted from `backlog` to `in-progress` because this is the first story in that epic.
- Red phase started by replacing the scaffold test with API contract tests; the first `dotnet test` failed on missing service contracts and seams as expected.
- A compose-backed smoke run initially exposed a real MySQL materialization bug on `GET /api/jobs/{id}`; `MySqlJobStore` was corrected to read the native `Guid` value before final validation.

### Implementation Plan

- Add explicit request/response contracts and keep endpoint orchestration thin in `Program.cs`.
- Isolate HMAC validation, public-URL validation, queue dispatch, and job persistence behind dedicated classes so tests can replace the store/queue with fakes.
- Fail closed when queue dispatch breaks after persistence by marking the job `failed` with `enqueue_failed` instead of returning `202`.
- Keep the worker scaffold untouched and wire only the API container path needed by this story.

### Completion Notes List

- Implemented `POST /api/generate-post` and `GET /api/jobs/{id}` on the existing Minimal API host with frozen JSON contract shapes and retained `/health`.
- Added reusable HMAC validation, public URL validation, MySQL-backed job storage, and SQS-compatible queue dispatch with a fail-closed `enqueue_failed` path.
- Added a `service` Docker image/container path plus `pipeline.jobs` bootstrap DDL so the API runs against the existing MySQL + ElasticMQ stack.
- Replaced the scaffold-only test with focused API behavior tests covering `202`, `400`, `401`, `404`, `200`, and queue failure orchestration.
- Verified the story with `dotnet test service/IaNewsPipeline.sln`, `docker compose config`, and a temporary compose smoke stack that returned `202` for intake and `queued` on job lookup.

### File List

- _bmad-output/implementation-artifacts/1-1-intake-api-job-store.md (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)
- docker-compose.yml (modified)
- docker/mysql-init/01-pipeline-schema.sql (modified)
- service/IaNewsPipeline.Api/Dockerfile (new)
- service/IaNewsPipeline.Api/Program.cs (modified)
- service/IaNewsPipeline.Api/Contracts/GeneratePostContracts.cs (new)
- service/IaNewsPipeline.Api/Jobs/IJobStore.cs (new)
- service/IaNewsPipeline.Api/Jobs/JobRecord.cs (new)
- service/IaNewsPipeline.Api/Jobs/JobStates.cs (new)
- service/IaNewsPipeline.Api/Jobs/MySqlJobStore.cs (new)
- service/IaNewsPipeline.Api/Queueing/IJobQueue.cs (new)
- service/IaNewsPipeline.Api/Queueing/SqsJobQueue.cs (new)
- service/IaNewsPipeline.Api/Security/HmacRequestValidator.cs (new)
- service/IaNewsPipeline.Api/Services/JobIntakeService.cs (new)
- service/IaNewsPipeline.Api/Validation/PublicUrlValidator.cs (new)
- service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj (modified)
- service/IaNewsPipeline.Tests/GeneratePostApiTests.cs (new)
- service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj (modified)
- service/IaNewsPipeline.Tests/ScaffoldTests.cs (deleted)

## Change Log

- 2026-07-07: Story created and contexted for development. Status -> ready-for-dev.
- 2026-07-07: Implemented intake API, MySQL job store, SQS-compatible enqueue, compose wiring, and focused API tests. Status -> review.
- 2026-07-07: Addressed review findings for SQS credential mode selection and non-public URL rejection coverage. Status -> done.
