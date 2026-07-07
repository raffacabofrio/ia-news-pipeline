# Architecture — AI Content Pipeline (Portal Tela Challenge)

**Author:** Winston (Architect) with Raffa · **Date:** 2026-07-07 · **Status:** Frozen (v1)
**Upstream:** `prd.md` · **Downstream:** epics & stories, three parallel implementation workstreams

The JSON contract in §5 is the single interface between components **and** between implementation agents. Changes to it after freeze require a `correct-course` decision, not an ad-hoc edit.

---

## 1. Topology

```
                       ┌────────────────────────────────────────┐
                       │ service/  (.NET, single container)     │
 POST /api/generate-post ─► Minimal API ──► SQS queue ──► Worker (BackgroundService)
 GET  /api/jobs/{id}   │        │       (ElasticMQ)  │          │
                       │        ▼                    ▼          │
                       │   jobs table (MySQL)   OpenAI API      │
                       └────────────────────────┬───────────────┘
                                                │ webhook (HMAC)
                                                ▼
                    ┌──────────────────────────────────────────┐
                    │ WordPress (plugin: REST receiver)         │
                    │ theme: Bootstrap/SASS via Vite            │
                    └──────────────────────────────────────────┘

docker compose: wordpress · mysql · elasticmq · service · wp-init (one-shot WP-CLI)
```

One MySQL instance, two schemas: `wordpress` (WP) and `pipeline` (jobs table).
One .NET process hosts both API and worker (POC decision; split documented as production path).

## 2. Decision Log

| # | Decision | Rationale (one line each) |
|---|---|---|
| D1 | **Service in .NET** (latest LTS, Minimal API) | Deadline was the hardest NFR: "I chose the tool I'm fastest and safest with." Fit is real (I/O orchestrator, mature background/resilience primitives). Result: deliberately polyglot architecture (.NET + PHP + JS/SASS) — technology chosen per compromise, no dogma. |
| D2 | **Queue = SQS API via ElasticMQ locally** | At-least-once delivery, DLQ via redrive policy (`maxReceiveCount: 5`), retry via visibility timeout. Local container, zero AWS account needed; production = change endpoint URL, zero code change. |
| D3 | **Idempotency owned by the receiver (WP plugin)** | Exactly-once = at-least-once delivery + idempotent receiver. Plugin checks post meta `_pipeline_job_id` before insert; hit → HTTP 200 with existing post, `duplicate: true`. |
| D4 | **HMAC-SHA256 signatures on both endpoints** | Secret never travels; tampering detected; replay window enforced via timestamp. Stripe/GitHub webhook pattern. Postman collection ships a pre-request script computing the signature. |
| D5 | **`wp-init` one-shot container (WP-CLI)** | `docker compose up` → WP installed, plugin + theme active, permalinks set. Spike (Story 0, timeboxed); graceful degradation = 3-step manual wizard in README. |
| D6 | Extraction `@mozilla/readability`-equivalent in .NET: **SmartReader** (Readability port) · OpenAI small model, JSON-structured output · Theme assets via **Vite** + Bootstrap SASS | Boring, proven, and Vite is Portal Tela's stack. |

## 3. Service internals (.NET)

- **Endpoints:**
  - `POST /api/generate-post` — body `{ "url": "https://..." }`, HMAC-signed. Validates URL shape, creates job row (`queued`), enqueues `job_id`, returns `202` + `{ job_id, status_url }`.
  - `GET /api/jobs/{id}` — returns `{ job_id, state, post_url?, error? }`. States: `queued → processing → publishing → published | failed`.
- **Worker (BackgroundService):** long-polls SQS → fetch URL → extract article (SmartReader) → OpenAI rewrite (JSON output: title, content_html, excerpt) → sign + POST to WP webhook → update job row → delete message.
- **Retry semantics:** any transient failure → message not deleted → SQS redelivers after visibility timeout; after 5 receives → DLQ + job row `failed` with reason. Non-transient failures (invalid URL, 404, non-article) → job `failed` immediately, message deleted (no pointless retries).
- **Logging:** structured (Serilog or built-in), every line carries `job_id`.
- **Config via env vars only:** `OPENAI_API_KEY`, `SQS_ENDPOINT`, `QUEUE_NAME`, `WP_WEBHOOK_URL`, `PIPELINE_SHARED_SECRET`, `MYSQL_CONNECTION`. `.env.example` committed.

## 4. WordPress side

- **Plugin `ia-pipeline-receiver`:** registers `POST /wp-json/ia-pipeline/v1/posts`. Verifies HMAC (timestamp tolerance ±300s, `hash_equals`), validates payload, checks `_pipeline_job_id` meta for idempotency, sanitizes content (`wp_kses_post`), creates published post, stores `job_id` + `source_url` + model meta as post meta. No settings UI — secret via constant/env (documented).
- **Theme `ia-news-theme`:** minimal WP theme; `single.php` is the centerpiece (typography, featured layout, source attribution block, "AI-generated" badge). Bootstrap as devDependency, customization through SASS variables, built with Vite; compiled assets committed so the evaluator does not need Node to see the theme.

## 5. Contract (frozen)

### 5.1 Webhook: service → WordPress

`POST /wp-json/ia-pipeline/v1/posts`

Headers:
```
Content-Type: application/json
X-Pipeline-Timestamp: <unix seconds>
X-Pipeline-Signature: sha256=<hex HMAC-SHA256(secret, timestamp + "." + raw_body)>
```

Body:
```json
{
  "job_id": "6f1c9c9e-2f7a-4b0e-9a4b-1a2b3c4d5e6f",
  "source_url": "https://example.com/original-article",
  "title": "Rewritten headline",
  "content_html": "<p>Rewritten body…</p>",
  "excerpt": "One-paragraph summary.",
  "meta": { "model": "gpt-4o-mini", "generated_at": "2026-07-07T15:00:00Z" }
}
```

Responses:
- `201` `{ "post_id": 123, "post_url": "http://…/?p=123", "duplicate": false }`
- `200` idempotent replay `{ …, "duplicate": true }`
- `401` invalid/missing signature or stale timestamp · `422` invalid payload (reason in body)

### 5.2 Service intake: caller → service

`POST /api/generate-post` — same HMAC header scheme, body `{ "url": "https://…" }`
- `202` `{ "job_id": "…", "status_url": "/api/jobs/…" }` · `400` invalid body · `401` bad signature

`GET /api/jobs/{id}` — no auth (POC; documented)
- `200` `{ "job_id": "…", "state": "published", "post_url": "http://…" }` · `404` unknown job

## 6. Repository layout

```
service/            # .NET solution (API + worker + tests)
wp-plugin/ia-pipeline-receiver/
wp-theme/ia-news-theme/
docker/             # elasticmq.conf, wp-init.sh, mysql init (pipeline schema)
docker-compose.yml
postman/            # collection + environment (HMAC pre-request script)
docs/               # challenge PDF, project knowledge
_bmad-output/       # method artifacts (part of the delivery story)
```

## 7. Production path (README section, not code)

Local piece → production equivalent, cost-aware: ElasticMQ → **SQS** (endpoint swap) · container → **ECS Fargate** (not k8s — deliberately) · OpenAI → **Bedrock** option + cost-per-token analysis · secrets → **Secrets Manager/SSM** · `GET /jobs` auth, rate limiting, multi-provider abstraction → documented one line each. Observability: CloudWatch structured logs + alarm on DLQ depth.

## 8. Test strategy

- **Unit (service, in CI):** extraction normalization, payload building, HMAC signing, retry/idempotency decision logic (transient vs permanent failure classification).
- **CI:** GitHub Actions — `dotnet test` on push.
- **Blind QA (method):** isolated reviewer runs the compose stack and verifies PRD ACs 1–9 behaviorally, including the kill-WordPress/recovery drill (AC3).
