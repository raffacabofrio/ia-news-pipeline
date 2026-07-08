---
baseline_commit: 8f9c15c3a3ade788b05288f436eb33d3fb7d0c8a
---

# Story 0.2: Repo scaffolding + CI

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Michel (the evaluator),
I want the repository to include a green .NET service skeleton, committed environment scaffolding, and CI on push,
so that subsequent implementation stories start from a stable, reproducible structure with immediate quality feedback.

## Acceptance Criteria

1. `service/` contains a .NET solution skeleton with three projects: API, Worker, and Tests. The solution restores, builds, and `dotnet test` passes locally with at least one non-empty placeholder test. [Source: epics-stories.md#S0.2; architecture.md#3-service-internals; architecture.md#8-test-strategy]
2. The repository layout stays aligned with architecture §6: `service/` is introduced without relocating existing `docker/`, `wp-plugin/`, `wp-theme/`, `docker-compose.yml`, or `_bmad-output/` paths. [Source: architecture.md#6-repository-layout]
3. `.env.example` is committed and contains the service environment variables defined in architecture §3 with safe placeholder/local values: `OPENAI_API_KEY`, `SQS_ENDPOINT`, `QUEUE_NAME`, `WP_WEBHOOK_URL`, `PIPELINE_SHARED_SECRET`, `MYSQL_CONNECTION`. [Source: architecture.md#3-service-internals; prd.md#6-non-functional-requirements]
4. A GitHub Actions workflow runs `dotnet test` on push and succeeds against the scaffolded solution. [Source: epics-stories.md#S0.2; architecture.md#8-test-strategy; prd.md#7-acceptance-criteria-behavioral]
5. S0.1 bootstrap remains intact: new scaffolding must not break the existing compose topology assumptions, queue defaults (`pipeline-jobs`, `pipeline-jobs-dlq`), or local endpoint conventions (`SQS_ENDPOINT=http://elasticmq:9324`). [Source: 0-1-spike-one-command-environment.md#Completion Notes List]
6. This story does **not** implement business behavior yet: no intake endpoint contract, no queue consumer logic, no jobs table DDL, no webhook calls, and no WordPress plugin/theme feature work. The output is scaffolding only. [Source: epics-stories.md#Epic-0; architecture.md#5-contract-frozen]

## Tasks / Subtasks

- [x] Task 1: Create the .NET solution skeleton under `service/` (AC: 1, 2)
  - [x] Create a solution file and three projects with consistent naming: API, Worker, and Tests.
  - [x] Wire project references so the test project builds against the production code layout that later stories will extend.
  - [x] Add a minimal placeholder test with a real assertion so `dotnet test` proves the test runner path works end-to-end.
- [x] Task 2: Scaffold placeholder API and Worker entrypoints (AC: 1, 6)
  - [x] API project uses Minimal API hosting only as a shell; no real `/api/generate-post` implementation yet.
  - [x] Worker project includes a placeholder hosted/background service shape only; no queue polling or OpenAI calls yet.
  - [x] Keep scaffolding boring and thin so Epic 1 can layer behavior without deleting large amounts of bootstrap code.
- [x] Task 3: Commit deterministic SDK/config scaffolding (AC: 1, 3, 4)
  - [x] Add `.env.example` with the exact architecture §3 service variables and local-safe defaults/comments.
  - [x] Add SDK/version pinning needed for stable local + CI builds (for example `global.json`) so the repo does not silently drift to whatever SDK the runner already has installed.
  - [x] Update `.gitignore` for .NET build outputs and tooling artifacts if missing.
- [x] Task 4: Add CI workflow (AC: 4)
  - [x] Create `.github/workflows/` workflow that triggers on push and runs restore/build/test for the scaffolded solution.
  - [x] Keep the workflow focused on `dotnet test`; do not add unrelated lint/release/package jobs in this story.
- [x] Task 5: Preserve and document integration guardrails from S0.1 (AC: 2, 5, 6)
  - [x] Read any existing files this story updates completely before editing them (`docker-compose.yml`, `.gitignore`, optionally `README.md` only if absolutely needed for consistency).
  - [x] If a placeholder `service` container is introduced into `docker-compose.yml`, it must preserve the current MySQL/ElasticMQ/WordPress wiring and use the same local env conventions chosen in S0.1.
  - [x] Do not modify the frozen JSON contracts in architecture §5.

## Dev Notes

### Why this story exists

- Epic 0 remains sequential: S0.1 solved local infrastructure friction first; S0.2 now creates the stable repo and CI base every later workstream will inherit. [Source: epics-stories.md#Epic-0]
- PRD NFR4 explicitly requires secrets only via env vars and a committed `.env.example`; PRD AC8 requires CI green on push. This story is where those delivery requirements first become concrete. [Source: prd.md#6-non-functional-requirements; prd.md#7-acceptance-criteria-behavioral]

### Scope boundaries (do NOT build)

- No real implementation of `POST /api/generate-post` or `GET /api/jobs/{id}` yet. Those belong to S1.1. [Source: epics-stories.md#S1.1]
- No queue polling, extraction, OpenAI integration, webhook delivery, or retry classification. Those belong to S1.2. [Source: epics-stories.md#S1.2]
- No meaningful service-core unit suite yet beyond proving the skeleton and test runner work; substantive tests belong to S1.3. [Source: epics-stories.md#S1.3]
- No plugin receiver or theme build work. Those belong to Epics 2 and 3. [Source: epics-stories.md#Epic-2; epics-stories.md#Epic-3]

### Previous story intelligence (S0.1)

- `docker-compose.yml` was intentionally shaped so a `service` entry can be added later without reworking network/env conventions. Reuse the existing local defaults: `SQS_ENDPOINT=http://elasticmq:9324` and `QUEUE_NAME=pipeline-jobs`. [Source: 0-1-spike-one-command-environment.md#Completion Notes List]
- Queue names are already chosen and documented: main `pipeline-jobs`, DLQ `pipeline-jobs-dlq`. Do not rename them in `.env.example` or compose comments. [Source: 0-1-spike-one-command-environment.md#Completion Notes List]
- Existing bootstrap paths are now a contract for parallel work: keep `docker/`, `wp-plugin/ia-pipeline-receiver/`, `wp-theme/ia-news-theme/`, and root `docker-compose.yml` untouched structurally unless this story has a direct reason to update them. [Source: 0-1-spike-one-command-environment.md#Constraints the dev agent MUST follow]

### Architecture compliance

- Service language/runtime is .NET Minimal API + BackgroundService hosted in one process/container as a deliberate POC decision. Scaffolding should reflect that future shape even before business behavior exists. [Source: architecture.md#1-topology; architecture.md#2-decision-log; architecture.md#3-service-internals]
- Config is env-vars only. `.env.example` must be the canonical manifest for service configuration. [Source: architecture.md#3-service-internals]
- Repository layout is frozen by architecture §6. New service scaffolding must fit that structure, not redefine it. [Source: architecture.md#6-repository-layout]

### Files likely NEW in this story

- `service/` solution and project files
- `service/` source folders for API, Worker, and Tests
- `.env.example`
- `.github/workflows/<ci-workflow>.yml`
- `global.json` (recommended for deterministic SDK selection)

### Files likely UPDATED in this story

- `.gitignore`
- `docker-compose.yml` only if the story adds a placeholder `service` container to converge toward the architecture topology
- `README.md` only if a tiny consistency update is necessary; do not try to complete S4.2 here

### Current state of UPDATE candidates

- `docker-compose.yml` currently contains `mysql`, `elasticmq`, `wordpress`, and `wp-init`, with explicit comments reserving the future service conventions (`SQS_ENDPOINT=http://elasticmq:9324`, `QUEUE_NAME=pipeline-jobs`). Preserve those assumptions if you touch it. [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- `.gitignore` currently ignores `.env`, `.env.*` except `.env.example`, plus Node/build/editor basics, but does not yet ignore common .NET outputs such as `bin/` and `obj/`. [Source: C:/Repos/ia-news-pipeline/.gitignore]
- There is no `.github/` directory and no existing `service/` directory yet, so this story primarily creates new assets rather than refactoring existing code.

### Testing requirements

- Local success path for this story is `dotnet test` green on the scaffolded solution. The placeholder test should still be a real test with an assertion, not an empty generated stub left failing/skipped.
- CI must execute the same essential path on push: restore/build/test for the solution. Keep the workflow minimal and deterministic.

### Latest tech information

- As of 2026-07-07, Microsoft lists `.NET 10` as the current supported LTS release (start date November 11, 2025; end date November 14, 2028). Use .NET 10 for the scaffold unless a local tool limitation blocks it. [Inference from official lifecycle table: https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core]
- Official `actions/setup-dotnet` docs currently document `actions/setup-dotnet@v5`, and warn that without a concrete SDK selection the runner may fall back to another preinstalled SDK. Prefer pinning the SDK deterministically (for example with `global.json`). [Source: https://github.com/actions/setup-dotnet]
- `actions/checkout` currently has `v7.0.0` as the latest release. For GitHub-hosted runners this is the safest current major to use unless the implementation discovers a compatibility reason to stay on an older major. [Inference from official releases page: https://github.com/actions/checkout/releases]

### Git intelligence summary

- Recent commit pattern is still early-foundation work: one implementation commit for S0.1 after planning commits. No established .NET naming convention exists yet, so S0.2 will set that convention for Epic 1. [Source: `git log --oneline --decorate -5`]
- Existing code changes are infra-first and additive; there is no service code to preserve, but there is now bootstrap behavior to avoid regressing. [Source: `git show --stat --oneline --name-only HEAD`]

### Project Structure Notes

- This repo is still at the bootstrap stage. Resist the urge to reorganize root folders; architecture §6 intentionally keeps top-level domains (`service/`, `wp-plugin/`, `wp-theme/`, `docker/`) explicit for the three parallel implementation workstreams.
- If you need a service container now, add it in the existing root compose file rather than inventing a second compose file.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S0.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#1-topology]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#3-service-internals]
- [Source: _bmad-output/planning-artifacts/architecture.md#6-repository-layout]
- [Source: _bmad-output/planning-artifacts/architecture.md#8-test-strategy]
- [Source: _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/implementation-artifacts/0-1-spike-one-command-environment.md]
- [Source: https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core]
- [Source: https://github.com/actions/setup-dotnet]
- [Source: https://github.com/actions/checkout/releases]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Sprint auto-discovery selected `0-2-repo-scaffolding-ci` as the first `backlog` story after S0.1 was marked `done`.
- No `project-context.md` files were present in the repo at story-creation time.
- No UX artifact exists; acceptable because this story is backend/repo scaffolding only.
- `dotnet test service/IaNewsPipeline.sln --configuration Release` passed on local SDK `10.0.301`.
- An initial parallelized `dotnet test --no-build` validation attempt failed because the build artifact was not yet present; the subsequent sequential validation passed cleanly.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created
- Scaffolded `service/` with a classic `.sln`, API/Worker/Tests projects, and test-project references to both runtime projects.
- Added minimal placeholder API and Worker shapes without crossing into Epic 1 business behavior.
- Added `.env.example`, `global.json`, `.gitignore` updates, and a GitHub Actions workflow running restore/build/test on push.
- Kept S0.1 bootstrap conventions intact: no compose topology rewrite and no frozen-contract changes.
- Validation passed: `dotnet test service/IaNewsPipeline.sln --configuration Release`.

### File List

- .env.example (new)
- .github/workflows/dotnet-test.yml (new)
- .gitignore (modified)
- _bmad-output/implementation-artifacts/0-2-repo-scaffolding-ci.md (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)
- global.json (new)
- service/IaNewsPipeline.sln (new)
- service/IaNewsPipeline.Api/ApiAssemblyMarker.cs (new)
- service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj (new)
- service/IaNewsPipeline.Api/Program.cs (new)
- service/IaNewsPipeline.Api/Properties/launchSettings.json (new)
- service/IaNewsPipeline.Api/appsettings.Development.json (new)
- service/IaNewsPipeline.Api/appsettings.json (new)
- service/IaNewsPipeline.Tests/IaNewsPipeline.Tests.csproj (new)
- service/IaNewsPipeline.Tests/ScaffoldTests.cs (new)
- service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj (new)
- service/IaNewsPipeline.Worker/Program.cs (new)
- service/IaNewsPipeline.Worker/Properties/launchSettings.json (new)
- service/IaNewsPipeline.Worker/Worker.cs (new)
- service/IaNewsPipeline.Worker/WorkerAssemblyMarker.cs (new)
- service/IaNewsPipeline.Worker/appsettings.Development.json (new)
- service/IaNewsPipeline.Worker/appsettings.json (new)

## Change Log

- 2026-07-07: S0.2 implemented — .NET solution scaffolded with API, Worker, and Tests projects; env manifest, SDK pinning, and push CI added. Status → review.
