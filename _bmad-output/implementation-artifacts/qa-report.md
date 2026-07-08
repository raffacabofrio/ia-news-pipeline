# QA Report — S4.3 Blind QA + fixes

Date: 2026-07-07
Story: `4-3-blind-qa-fixes`
Verdict: pass

## Summary

All PRD acceptance criteria AC1–AC9 were re-verified against the live local stack or the authoritative repository state. Two concrete findings were fixed in this story:

1. `service/IaNewsPipeline.Worker/Services/OpenAiRewriteClient.cs`
   Reduced the OpenAI prompt payload by stripping HTML and truncating oversized extracted content before sending it to `chat/completions`; this unblocked the real happy path after a live `openai_http_400` failure.
2. `README.md`, `postman/ia-news-pipeline.postman_collection.json`, `postman/ia-news-pipeline.postman_environment.json`
   Replaced the misleading hardcoded `https://example.com/article` happy-path example with a real `article_url` variable/default that was proven in live QA, so README/Postman now guide evaluators through a working publish path instead of a guaranteed failure path.

## AC Results

### AC1 — End-to-end happy path

Outcome: pass

Method:
- Submitted a signed `POST http://localhost:8081/api/generate-post` using `https://en.wikipedia.org/wiki/Artificial_intelligence`.
- Polled `GET /api/jobs/{id}` until terminal state.

Evidence:
- Job `6605bd40-99ba-4d98-95f4-cd844e39e44e`
- `0.1s -> processing`
- `8.1s -> processing`
- `16.1s -> published`
- Published URL: `http://localhost:8080/artificial-intelligence-3/`

### AC2 — Observable status progression

Outcome: pass

Method:
- Polled `GET /api/jobs/{id}` across multiple live runs to observe non-terminal and terminal transitions.

Evidence:
- `483a5890-3440-4c59-a04f-e60ca5bbfb44`: `queued -> processing -> published`
- `2f61753b-4276-4d44-9cc6-1914be5757c1`: `processing -> publishing -> published`
- `8870e27d-1d2d-4f01-bdba-16deb6f68275`: terminal `failed` with `source_not_found`
- `9d1730f3-be15-49cc-b71c-d8d1e4f1a8b9`: terminal `failed` with `source_not_article`

### AC3 — WordPress-outage recovery drill, exactly once

Outcome: pass

Method:
- Stopped WordPress before submitting a valid signed request.
- Waited until the job reached `publishing` while WordPress was unavailable.
- Started WordPress again and continued polling to terminal success.
- Verified exact-once publication by querying `_pipeline_job_id` in MySQL.

Evidence:
- Job `2f61753b-4276-4d44-9cc6-1914be5757c1`
- While WordPress was down: `processing`, `processing`, `publishing`
- After WordPress restart: remained `publishing`, then returned to `processing`, then `published`
- Final published URL: `http://localhost:8080/artificial-intelligence-2/`
- Published posts before drill: `2`
- Published posts after drill: `3`
- MySQL exact-once check: `count(distinct post_id)` for `_pipeline_job_id = 2f61753b-4276-4d44-9cc6-1914be5757c1` returned `1`

Risk note:
- The documented redelivery-window risk is real: the drill stayed within the existing `VisibilityTimeout = 120s` / `maxReceiveCount = 5` limits and passed without hitting the DLQ gap.

### AC4 — Webhook auth rejection

Outcome: pass

Method:
- Posted directly to `http://localhost:8080/wp-json/ia-pipeline/v1/posts` without signature headers.
- Compared published-post count before and after.

Evidence:
- HTTP status: `401`
- Published posts before request: `2`
- Published posts after request: `2`

### AC5 — Invalid/unreachable URL fails cleanly

Outcome: pass

Method:
- Submitted malformed URL to intake.
- Submitted unreachable/404 URL.
- Submitted non-article page that resolves successfully.

Evidence:
- Malformed intake body `{"url":"notaurl"}` -> HTTP `400`, body `{"error":"invalid_url"}`
- Job `8870e27d-1d2d-4f01-bdba-16deb6f68275` (`https://www.nasa.gov/news-release/...`) -> terminal `failed`, error `source_not_found`
- Job `9d1730f3-be15-49cc-b71c-d8d1e4f1a8b9` (`https://example.com/`) -> terminal `failed`, error `source_not_article`
- No post was created for those failed runs

### AC6 — README is self-sufficient

Outcome: pass

Method:
- Performed a fresh-stack walkthrough with `docker compose down -v` followed by `docker compose up -d --build`.
- Verified the README-described bootstrap behavior and corrected the misleading happy-path testing guidance.

Evidence:
- `wp-init` exited with code `0`
- `service`, `worker`, `wordpress`, `mysql`, `elasticmq` all reached running/healthy states
- Finding fixed: README happy-path example no longer points to `https://example.com/article`; it now directs evaluators to a real verified article URL and the Postman flow that signs requests automatically

### AC7 — Themed single-post rendering

Outcome: pass

Method:
- Verified the actual pipeline-created post in the in-app browser at desktop and mobile width.

Evidence:
- Desktop page: `http://localhost:8080/artificial-intelligence/`
- Badge present: `AI-generated`
- Lead paragraph present with extracted/re-written summary
- Source attribution link present: `https://en.wikipedia.org/wiki/Artificial_intelligence`
- Constrained content measure observed: `.single-feature__measure` width `704px` in `1280px` viewport
- Mobile check at `375px`: article width `360px`, surface width `336px`, lead visible, source link visible, `horizontalOverflow = false`

### AC8 — CI green, tests cover the right things

Outcome: pass

Method:
- Verified the repo workflow definition.
- Ran the local proxy command because GitHub Actions state was not inspected from this environment.
- Confirmed test-suite substance by reading the relevant test files.

Evidence:
- Workflow present: `.github/workflows/dotnet-test.yml`
- Local proxy run: `dotnet test service/IaNewsPipeline.sln` -> `46/46` passing
- Coverage present in named tests:
  - extraction: `ArticleExtractionTests.cs`
  - webhook payload construction: `WebhookPayloadTests.cs`
  - retry/failure classification/idempotency: `FailureClassificationTests.cs`, `IdempotentReplayTests.cs`, `WorkerPipelineTests.cs`
  - HMAC signing: `HmacSigningTests.cs`

### AC9 — README content completeness

Outcome: pass

Method:
- Literal read-through of `README.md` after the AC6 fix.

Evidence:
- Architecture + decisions section present with links to frozen artifacts
- Executive summary present in semaforo style (`Entregue`, `Riscos conhecidos`, `Nao fazer ainda`, `Proximo passo`)
- Out-of-scope table present with one-line production paths
- Cost-aware production section present using AWS vocabulary (`SQS`, `ECS Fargate`, `Bedrock`, `cost per post`)

## Final verdict

S4.3 is done. All AC1–AC9 pass on the current tree, and the two previously unproven claims called out by the story were closed with live evidence:

1. Real happy-path publication to `published`
2. Real WordPress-outage recovery with exact-once publication
