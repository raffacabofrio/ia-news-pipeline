# Epics & Stories — AI Content Pipeline

**Author:** John (PM) · **Date:** 2026-07-07 · **Status:** Ready for implementation
**Upstream:** `prd.md`, `architecture.md` (contract §5 frozen)

Story sizing rule (BMAD): one story = one fresh agent session. Each story prompt receives ONLY: the story text, the frozen contract (§5 of architecture.md), and the referenced architecture sections. No agent reads another agent's reasoning.

Execution order: **Epic 0 sequential first** → Epics 1, 2, 3 in parallel → Epic 4 closes.

---

## Epic 0 — Foundation (sequential, blocks everything)

### S0.1 — Spike: one-command environment ⏱ timebox 90 min
Compose stack: `wordpress`, `mysql` (schemas `wordpress` + `pipeline`), `elasticmq` (queue + DLQ, redrive maxReceiveCount=5), `wp-init` one-shot (WP-CLI: core install, activate plugin `ia-pipeline-receiver` + theme `ia-news-theme` — placeholder dirs ok, permalink structure `/%postname%/`).
**Done when:** `docker compose up` from clean clone → WP reachable with placeholder plugin/theme active; ElasticMQ answers SQS ListQueues.
**Degradation if timebox blows:** manual 3-step wizard documented; wp-init dropped.

### S0.2 — Repo scaffolding + CI
`service/` .NET solution skeleton (API + Worker + Tests projects, builds green), `.env.example` (all vars from architecture §3), GitHub Actions workflow (`dotnet test` on push), folder layout per architecture §6.
**Done when:** CI green on push with placeholder test.

## Epic 1 — Generation Service (.NET) — parallel workstream A

### S1.1 — Intake API + job store
`POST /api/generate-post` (HMAC verify, URL validation, insert job row `queued`, enqueue job_id, return 202 per contract §5.2) + `GET /api/jobs/{id}`. Jobs table in `pipeline` schema.
**Done when:** contract §5.2 responses exact; bad signature → 401; invalid body → 400.

### S1.2 — Worker pipeline
BackgroundService long-polling SQS: fetch URL → SmartReader extraction → OpenAI (JSON-structured output: title, content_html, excerpt) → HMAC-sign → POST webhook (contract §5.1) → job row updates through states → delete message. Failure classification per architecture §3: transient → message NOT deleted (SQS redelivers); permanent (invalid URL, 404, non-article/empty extraction) → `failed` + delete. Structured logs with `job_id` on every line.
**Done when:** verified against a contract-§5.1 stub webhook (this story does NOT depend on Epic 2): happy path sends the exact §5.1 request shape and handles 201, 200 `duplicate:true`, 401 and 422 responses correctly; stub-unavailable scenario → message redelivered, then succeeds once stub returns; invalid URL and non-article page fail fast with reason. Real end-to-end verification belongs to S4.3.

### S1.3 — Unit tests
Extraction normalization, payload building, HMAC signing (known test vectors), transient-vs-permanent classification (including non-article/empty extraction → permanent), idempotent replay handling of webhook 200/duplicate.
**Done when:** `dotnet test` green in CI, meaningful assertions (no placeholder).

## Epic 2 — WordPress Plugin — parallel workstream B

### S2.1 — Receiver endpoint
Plugin `ia-pipeline-receiver`: register `POST /wp-json/ia-pipeline/v1/posts`; HMAC verify (`hash_hmac` + `hash_equals`, timestamp ±300s) → 401; payload validation → 422; idempotency via `_pipeline_job_id` meta query → 200 `duplicate:true`; else `wp_kses_post` sanitize → create published post + meta (`job_id`, `source_url`, model) → 201 per contract §5.1.
**Done when:** all four response paths behave exactly per contract §5.1 against a real WP.

## Epic 3 — Theme — parallel workstream C

### S3.1 — Theme scaffold + build
`ia-news-theme`: minimal valid WP theme; Vite build, Bootstrap as devDependency, customization via SASS variables (no CDN); compiled assets committed.
**Done when:** theme activates, styles load from built assets, `npm run build` reproduces them.

### S3.2 — single.php centerpiece
Polished single-post layout: typography, readable measure, excerpt lead, source attribution block (links `source_url` meta), "AI-generated" badge, responsive.
**Done when:** verified with a manually created post carrying the pipeline meta fields (`_pipeline_job_id`, `source_url`, model) — this story does NOT depend on Epics 1–2; pipeline-created post verification belongs to S4.3. Objective checks: content column has constrained readable measure (not full-width); excerpt renders as styled lead paragraph; source attribution block present and links `source_url`; "AI-generated" badge visible; layout holds at 375px mobile width.

## Epic 4 — Delivery (closes the day)

### S4.1 — Postman collection
Collection + environment for both service endpoints; pre-request script computes HMAC from environment secret; example flows: happy path, bad signature, job polling.
**Done when:** evaluator imports, sets 2 env vars, requests succeed.

### S4.2 — README
Architecture + decision log (from artifacts, linked), quickstart (compose + .env), executive summary (semáforo: delivered / risks / next steps / product vision), out-of-scope items with one-line production path each, production section (SQS/ECS/Bedrock, cost-per-token), the method story (BMAD/SDD, timestamps).
**Done when:** AC6 and AC9 satisfied by literal read-through.

### S4.3 — Blind QA + fixes
Isolated reviewer (fresh context, ACs only — no implementation reasoning) runs the stack and verifies PRD AC1–AC9, including kill-WP recovery drill (AC3). Findings fixed; QA report saved to `_bmad-output/implementation-artifacts/qa-report.md`.
**Done when:** all ACs pass or have a documented, accepted exception.

---

## Dependency map

```
S0.1 → S0.2 → ┬─ S1.1 → S1.2 → S1.3 ─┐
              ├─ S2.1 ────────────────┤→ S4.1 → S4.3
              └─ S3.1 → S3.2 ─────────┘   S4.2 (parallel with S4.1)
```
