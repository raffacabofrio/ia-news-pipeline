---
baseline_commit: ff92d1a2d129aa6bc8628b55b21f33c002983080
---

# Story 2.1: Receiver endpoint

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the generation service,
I want to deliver signed rewritten-content payloads to a dedicated WordPress REST endpoint,
so that accepted jobs become published posts exactly once even when deliveries are retried.

## Acceptance Criteria

1. The plugin registers `POST /wp-json/ia-pipeline/v1/posts` via the WordPress REST API, not via custom rewrites or admin-post hooks. [Source: epics-stories.md#Epic-2; architecture.md#4-wordpress-side]
2. The endpoint verifies the HMAC contract exactly as frozen in architecture §5.1:
   - headers `X-Pipeline-Timestamp` and `X-Pipeline-Signature`
   - signature format `sha256=<hex>`
   - signed message is `timestamp + "." + raw_body`
   - algorithm `HMAC-SHA256`
   - stale or invalid signature returns `401`
   - timestamp tolerance is ±300 seconds. [Source: architecture.md#5-contract-frozen]
3. Invalid payloads return `422` with a reason in the response body. At minimum, the implementation validates the presence and basic shape of `job_id`, `source_url`, `title`, `content_html`, `excerpt`, and `meta.model` / `meta.generated_at`. [Source: architecture.md#5-contract-frozen; epics-stories.md#S2.1]
4. Idempotency is owned by the receiver: if a post already exists for `_pipeline_job_id = job_id`, the endpoint returns `200` with `{ "post_id": ..., "post_url": "...", "duplicate": true }` and does not create another post. [Source: architecture.md#2-decision-log; architecture.md#4-wordpress-side; architecture.md#5-contract-frozen]
5. For a new valid request, the plugin sanitizes `content_html` with `wp_kses_post`, creates a published WordPress post, stores the pipeline metadata, and returns `201` with `{ "post_id": ..., "post_url": "...", "duplicate": false }`. [Source: epics-stories.md#S2.1; architecture.md#4-wordpress-side; architecture.md#5-contract-frozen]
6. The created post preserves the payload information needed by later stories and end-to-end QA:
   - title becomes the post title
   - `content_html` becomes post content after sanitization
   - `excerpt` becomes `post_excerpt`
   - source/model/job identifiers are persisted as post meta using the `_pipeline_*` convention. [Source: architecture.md#4-wordpress-side; epics-stories.md#S3.2]
7. The implementation works against the real local WordPress stack from S0.1, with all four behavior paths verifiable on a running instance: `201 created`, `200 duplicate:true`, `401 unauthorized`, and `422 invalid payload`. [Source: epics-stories.md#S2.1; prd.md#AC4]

## Tasks / Subtasks

- [x] Task 1: Replace the placeholder plugin bootstrap with a real REST receiver entrypoint (AC: 1)
  - [x] Keep the plugin header and `ABSPATH` guard intact in `wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php`
  - [x] Register the route on `rest_api_init` with namespace `ia-pipeline/v1` and route `/posts`
  - [x] Supply an explicit `permission_callback` and perform HMAC auth inside the endpoint flow
- [x] Task 2: Implement contract-accurate request authentication (AC: 2)
  - [x] Read the raw body before any lossy re-encoding and build the signature string as `timestamp . "." . raw_body`
  - [x] Load the shared secret from environment/constant only; fail closed if it is missing
  - [x] Use `hash_hmac( 'sha256', ... )` and `hash_equals()`; reject missing/malformed headers and stale timestamps with `401`
- [x] Task 3: Validate and normalize the webhook payload (AC: 3)
  - [x] Decode JSON safely and return `422` for malformed JSON or missing required fields
  - [x] Validate `source_url` as a URL, `title` / `excerpt` as non-empty strings, `content_html` as non-empty HTML/text, and `meta` as an object/array containing `model` and `generated_at`
  - [x] Keep error responses concise and machine-readable; do not return HTML error pages
- [x] Task 4: Implement receiver-owned idempotency (AC: 4)
  - [x] Query existing posts by `_pipeline_job_id` before insert
  - [x] On hit, return the existing post reference with HTTP `200` and `duplicate: true`
  - [x] Ensure retries do not mutate the existing post on duplicate replay
- [x] Task 5: Create the published post and persist the needed metadata (AC: 5, 6)
  - [x] Sanitize `content_html` with `wp_kses_post` on unslashed content
  - [x] Create a published `post` with title/content/excerpt from the payload
  - [x] Store `_pipeline_job_id`, `_pipeline_source_url`, and `_pipeline_model` post meta at minimum
- [x] Task 6: Wire the local secret source needed for real verification (AC: 2, 7)
  - [x] If the current WordPress container cannot read `PIPELINE_SHARED_SECRET`, update only the directly relevant infrastructure path to expose it to the plugin runtime
  - [x] Keep the secret path aligned with architecture guidance: env var or constant, no settings UI and no committed real secret
- [x] Task 7: Verify the four required response paths against a real local WordPress instance (AC: 7)
  - [x] Happy path creates a post and returns `201`
  - [x] Replay of the same `job_id` returns `200` with `duplicate: true`
  - [x] Bad or stale signature returns `401`
  - [x] Invalid payload returns `422` with reason

## Dev Notes

### Why this story exists

- Epic 2 is the WordPress workstream that makes the service-to-WordPress contract real. It is intentionally parallel to Epics 1 and 3, but all interop depends on architecture §5.1 staying frozen. [Source: epics-stories.md#Epic-2; architecture.md#5-contract-frozen]
- PRD FR3 and NFR4 make webhook authentication and sanitization non-optional. AC4 specifically requires rejection without the correct secret. [Source: prd.md#5-functional-requirements; prd.md#6-non-functional-requirements; prd.md#7-acceptance-criteria-behavioral]

### Scope boundaries (do NOT build)

- Do not implement the service caller, queue logic, or retry policy here. Those belong to Epic 1. This story only receives already-generated payloads. [Source: epics-stories.md#S1.1; epics-stories.md#S1.2]
- Do not create a WordPress settings page, admin UI, nonce-based auth, or manual post-management workflow. Secret sourcing is env/constant only. [Source: architecture.md#4-wordpress-side]
- Do not redesign the JSON contract, rename headers, or invent a different success envelope. The contract is frozen. [Source: architecture.md#5-contract-frozen]
- Do not pull theme work into the plugin. The theme consumes the resulting posts later; this story must only make sure the stored post/excerpt/meta support that downstream rendering. [Source: epics-stories.md#Epic-3; epics-stories.md#S3.2]

### Architecture compliance

- Use the WordPress REST API with `register_rest_route()` on `rest_api_init`; WordPress documents that registering routes before that hook is incorrect and public routes still require an explicit `permission_callback`. [External: https://developer.wordpress.org/reference/functions/register_rest_route/]
- `wp_kses_post()` is the correct WordPress sanitization primitive for post content and expects unslashed data. Do not down-convert `content_html` to plain text. [External: https://developer.wordpress.org/reference/functions/wp_kses_post/]
- Constant-time comparison matters for the signature check: `hash_equals()` is the PHP primitive intended for this use. [External: https://www.php.net/manual/en/function.hash-equals.php]
- HMAC generation must use the raw request bytes, not a decoded-and-re-encoded JSON string, or signatures will drift on whitespace/escaping differences. This is essential for interop with the .NET sender. [Source: architecture.md#5-contract-frozen]

### Files likely UPDATED in this story

- `wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php`
  - Current state: placeholder plugin header plus `ABSPATH` guard only.
  - What this story changes: adds REST route registration and either the endpoint implementation directly or bootstrap code for helper files.
  - What must be preserved: plugin slug/header metadata, direct-execution guard, activation compatibility with S0.1.
- `docker-compose.yml` only if required to expose `PIPELINE_SHARED_SECRET` to the running WordPress container for real local verification.
  - Current state: the `wordpress` service has DB env vars but no pipeline secret env var.
  - What this story changes: at most, a narrow env wiring change.
  - What must be preserved: existing service topology, bind mounts, ports, and S0.1 bootstrap behavior.

### Files likely NEW in this story

- Optional helper files inside `wp-plugin/ia-pipeline-receiver/` if the implementation becomes clearer when split (for example `includes/` helpers for auth, validation, or response shaping).
- Avoid introducing new top-level folders or cross-workstream structure changes.

### Critical implementation guardrails

- Treat `job_id` as the stable idempotency key for the lifetime of the system. Duplicate handling must happen before insert and must not create side effects on replay. [Source: architecture.md#2-decision-log]
- Persist `excerpt` into the WordPress post record, not only the raw payload. This is not spelled out in Epic 2, but S3.2 expects single-post pages to render a lead excerpt for pipeline-created posts during end-to-end QA. Failing to store it here creates a downstream regression. [Source: epics-stories.md#S3.2]
- Use a `_pipeline_*` post-meta prefix consistently. Architecture names `_pipeline_job_id` explicitly; keep companion keys consistent for readability and future lookup (`_pipeline_source_url`, `_pipeline_model`). [Source: architecture.md#2-decision-log; architecture.md#4-wordpress-side]
- Keep success responses lean and exact. Do not wrap `{ post_id, post_url, duplicate }` inside extra envelopes or add unrelated payload fields to the contract response. [Source: architecture.md#5-contract-frozen]
- Error responses must stay JSON/REST-native. Returning rendered HTML or triggering `wp_die()` pages would break the service caller and Postman verification.

### Current integration gap to account for

- The local WordPress runtime currently has no obvious secret injection path in `docker-compose.yml`. Since this story must be verifiable against a real WP instance, the implementation may need a small, direct infrastructure adjustment so `getenv( 'PIPELINE_SHARED_SECRET' )` or a defined constant is available in the plugin runtime. This is in-scope because it is directly required by AC7. [Source: C:/Repos/ia-news-pipeline/docker-compose.yml; architecture.md#4-wordpress-side]

### Testing requirements

- This story has no PHP unit-test harness in the repo today, so the minimum required verification is behavioral against a real running WordPress stack.
- Capture the exact four paths from the story definition:
  - valid signed payload → `201`
  - replayed `job_id` → `200 duplicate:true`
  - invalid/missing/stale signature → `401`
  - invalid payload → `422`
- If you add lightweight local verification helpers, keep them scoped to this plugin and do not build a parallel custom test framework.

### Latest tech information

- Official WordPress developer docs currently require an explicit `permission_callback` when registering REST routes and recommend `__return_true` for public routes that enforce their own auth inside the callback. [External: https://developer.wordpress.org/reference/functions/register_rest_route/]
- Official WordPress docs for `wp_kses_post()` state that it sanitizes for allowed post-content HTML and expects unslashed input. [External: https://developer.wordpress.org/reference/functions/wp_kses_post/]
- The PHP manual documents `hash_equals()` as timing-safe string comparison and `hash_hmac()` as the keyed-hash primitive to use for HMAC generation. [External: https://www.php.net/manual/en/function.hash-equals.php] [External: https://www.php.net/manual/en/function.hash-hmac.php]
- As of 2026-07-07, WordPress project compatibility notes document WordPress 6.8 as fully compatible with PHP 8.4. The local stack is pinned to `wordpress:6.8-apache`, so this story should target current WordPress 6.8 behavior and avoid speculative upgrades. [External: https://make.wordpress.org/core/handbook/references/php-compatibility-and-wordpress-versions/]

### Git intelligence summary

- Recent implementation work is additive and foundation-oriented: S0.1 established the compose/WP bootstrap; S0.2 added service scaffolding and `.env.example`. There is still no existing PHP implementation pattern in the repo, so this story sets the plugin conventions for later QA. [Source: `git log --oneline -5`]
- The plugin file is still a placeholder, which is good news for this story: most work is additive, but `docker-compose.yml` is already user-modified in the working tree and must be edited carefully if the secret wiring change is needed. [Source: `git status --short`; `wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php`]

### Discovery notes

- Loaded planning artifacts:
  - `epics_content`: 1 file (`_bmad-output/planning-artifacts/epics-stories.md`)
  - `prd_content`: 1 file (`_bmad-output/planning-artifacts/prd.md`)
  - `architecture_content`: 1 file (`_bmad-output/planning-artifacts/architecture.md`)
- No UX artifact was found.
- No `project-context.md` file was found anywhere under the project root.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#Epic-2]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S2.1]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- [Source: _bmad-output/planning-artifacts/prd.md#5-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#6-non-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: C:/Repos/ia-news-pipeline/wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [External: https://developer.wordpress.org/reference/functions/register_rest_route/]
- [External: https://developer.wordpress.org/reference/functions/wp_kses_post/]
- [External: https://www.php.net/manual/en/function.hash-equals.php]
- [External: https://www.php.net/manual/en/function.hash-hmac.php]
- [External: https://make.wordpress.org/core/handbook/references/php-compatibility-and-wordpress-versions/]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- User explicitly targeted `2-1-receiver-endpoint`, so story selection used the provided key instead of backlog auto-discovery.
- Workflow activation resolved successfully through `_bmad/scripts/resolve_customization.py`; no prepend/append activation steps were configured.
- `project-context.md` was not present anywhere in the repo; no UX file was present in planning artifacts.
- The full `sprint-status.yaml` was loaded before updates; `epic-2` and `2-1-receiver-endpoint` were both `backlog` at story-creation time.
- Added a lightweight PowerShell verifier under `wp-plugin/ia-pipeline-receiver/tests/` to exercise the four required runtime paths against the live local WordPress stack.
- Recreated only the `wordpress` container after wiring `PIPELINE_SHARED_SECRET` into `docker-compose.yml`, preserving the rest of the local topology.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created
- Story focuses the developer on the frozen webhook contract, the WordPress-specific REST/auth primitives, and the only meaningful integration gap currently visible: exposing the shared secret to the WordPress runtime.
- Added explicit downstream guardrail to persist `excerpt` into the created post so theme story S3.2 and blind QA S4.3 can succeed without hidden follow-up work.
- Kept scope restricted to S2.1 and the directly required sprint-tracking artifact.
- Implemented `POST /wp-json/ia-pipeline/v1/posts` with explicit `permission_callback`, HMAC validation over `timestamp.raw_body`, contract-shaped `401`/`422` error responses, and receiver-owned idempotency by `_pipeline_job_id`.
- Published posts now persist sanitized content, excerpt, and `_pipeline_*` metadata needed downstream, including `_pipeline_generated_at` for QA traceability.
- Verified on the real local WordPress instance: `201 created`, `200 duplicate:true`, `401 stale signature`, and `422 invalid payload`, plus direct inspection of the created post and stored metadata.

### File List

- _bmad-output/implementation-artifacts/2-1-receiver-endpoint.md (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)
- docker-compose.yml (modified)
- wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php (modified)
- wp-plugin/ia-pipeline-receiver/tests/verify-receiver.ps1 (new)

## Change Log

- 2026-07-07: S2.1 story context created and marked `ready-for-dev`. Epic 2 advanced from `backlog` to `in-progress`.
- 2026-07-07: Implemented the WordPress REST receiver, secret env wiring, and live verification helper; story advanced to `review`.
