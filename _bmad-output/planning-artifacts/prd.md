# PRD — AI Content Pipeline (Technical Challenge)

**Author:** John (PM) with Raffa · **Date:** 2026-07-07 · **Status:** Draft for review
**Source:** `docs/desafio-portal-tela.pdf` (challenge v1.0) + evaluator profile analysis

---

## 1. Context & Thesis

This is a proof of concept built for a technical challenge. The product will not go to real production — and that is a deliberate, documented decision, not a limitation.

**The thesis being demonstrated:** AI-assisted engineering with advanced context engineering (Spec-Driven Development / BMAD method) delivers in 1 day what conventionally takes 3+, **without sacrificing engineering rigor** — spec as source of truth, decoupled verification, resilience, tests, CI.

The delivery timestamp trail (receipt confirmation → repo invite) is part of the evidence.

## 2. Users

| Persona | Role | What they need |
|---|---|---|
| **Michel (primary)** | Evaluator, Tech Lead | Clone, run everything with one command, see it work, read decisions with clear rationale, verify engineering maturity |
| Content editor (secondary, fictional) | Scenario persona | Submit an article URL and get a rewritten post published on WordPress |

When the two personas conflict, **Michel wins**.

Evaluator profile (from interview analysis, drives NFRs):
- Explicitly rejects "vibe coding"; values SDD, AI-with-ownership — *the method is the product*
- Anti-overengineering ("start simple, professionalize when needed") — *justify what we did NOT build*
- Quality proven by pipeline (tests, review, observability), not by promise — *tests + CI are in scope*
- AWS-native vocabulary, cost/ROI mindset — *production section speaks Bedrock/ECS/cost-per-token*
- Gap to correct from interview: executive altitude — *README carries an executive summary (semáforo)*

## 3. Goals

1. **G1 — Working end-to-end pipeline:** public article URL in → AI-rewritten post published on WordPress out.
2. **G2 — Method made visible:** planning artifacts (PRD, architecture, stories), commit history and README tell the 1-day SDD story.
3. **G3 — Engineering maturity on display:** resilience implemented (not promised), tests, CI, structured logs, honest production analysis.

## 4. Scope

### In scope
- **Generation service** (independent app): endpoint receiving a public article URL; fetches and extracts content; rewrites via OpenAI API; delivers structured payload to WordPress plugin webhook.
- **Resilience (first-class requirement):** async processing; retry with backoff on transient failures (target site, OpenAI, WordPress); no accepted request silently lost; no duplicate posts from retries; permanent failures observable.
- **WordPress plugin:** custom REST endpoint receiving the payload, authenticated via shared secret; creates the post.
- **WordPress theme:** Bootstrap as dev dependency with asset build (SASS variables customization); design effort concentrated on `single.php`.
- **One-command environment:** `docker compose up` brings up WordPress + MySQL + service, with documented bootstrap.
- **Quality:** unit tests on service core logic; CI (GitHub Actions) running them; structured logs.
- **Deliverables:** README (architecture, decisions, setup, executive summary, production path), Postman collection, GitHub repo shared with `michel-portaltela`.

### Out of scope (documented in README with production path, one line each)
- Multiple AI providers (OpenAI only — pragmatic: key at hand; abstraction documented)
- Admin panel / management UI
- Caching layer
- Rate limiting / abuse protection
- Auto-scaling infrastructure (documented as Bedrock/ECS path)

## 5. Functional Requirements

**FR1 — Generate & publish:** given a public article/news URL, the system produces a rewritten version (new title + body, structured JSON) and publishes it as a WordPress post.
**FR2 — Async semantics:** the caller receives immediate acknowledgment of an accepted request and has a way to determine its final outcome (success with post reference, or failure with reason).
**FR3 — Authenticated intake:** the WordPress webhook rejects payloads that do not carry the expected shared secret.
**FR4 — Content presentation:** published posts render on the theme's single-post page, visually polished, Bootstrap-based.
**FR5 — Input validation:** invalid URLs, unreachable pages, and non-article content produce clear errors, not crashes or garbage posts.

## 6. Non-Functional Requirements

**NFR1 — Resilience:** transient failure of any downstream dependency (target site, OpenAI, WordPress) does not lose an accepted request; retries use backoff; retries never create duplicate posts; permanently failed requests are visible in logs with reason.
**NFR2 — Runnability:** evaluator goes from `git clone` to working pipeline with one compose command plus documented `.env` setup; no undocumented manual steps.
**NFR3 — Observability (POC level):** structured logs allow following one request across the pipeline (request id in every log line).
**NFR4 — Security (POC level):** secrets only via environment variables; `.env.example` provided; webhook authenticated; WordPress sanitizes incoming content.
**NFR5 — Test coverage where it matters:** core service logic (extraction, payload building, retry/idempotency behavior) unit-tested and green in CI.

## 7. Acceptance Criteria (behavioral)

- **AC1:** Given the stack is up via `docker compose up`, when a valid article URL is submitted to the generation endpoint, then within 2 minutes a new post with AI-generated title and body is published and visible on the WordPress site.
- **AC2:** Given a submitted request, the caller can observe its status until a terminal state (published / failed with reason).
- **AC3:** Given WordPress is temporarily unavailable when generation completes, when it becomes available again, then the post is eventually published without manual intervention and **exactly once**.
- **AC4:** Given a webhook call without the correct shared secret, then no post is created and the response indicates rejection.
- **AC5:** Given an invalid or unreachable URL, then the request reaches a failed state with a clear reason, and no post is created.
- **AC6:** Given a fresh clone, `README.md` alone is sufficient to configure, run, and test the whole pipeline (verified by following it literally).
- **AC7:** The published post page renders with the custom Bootstrap theme; single-post layout is the visual centerpiece.
- **AC8:** CI runs on push and is green; tests cover extraction, payload construction, and retry/idempotency logic.
- **AC9:** README contains: architecture + decisions, executive summary (delivered / risks / next steps), out-of-scope items each with production path, cost-aware production section (AWS vocabulary).

## 8. Risks

| Risk | Mitigation |
|---|---|
| 1-day window blown by resilience scope | Architect chooses the cheapest mechanism satisfying NFR1 (no heavyweight infra by default); parallel implementation of the 3 components |
| Content extraction is an open-ended rabbit hole | Extraction "good enough for blog/news pages"; edge cases documented, not chased |
| WordPress/docker bootstrap friction eats the morning | Bootstrap solved first, before feature code (Story ordering) |
| OpenAI cost/latency during demos | Small default model, capped tokens; documented |

## 9. Delivery Plan

Single day, three parallel workstreams after contract freeze:
1. **Contract first:** JSON payload schema + webhook auth agreed (architecture phase) — the interface between all components and all agents.
2. **Parallel build:** `service/`, `wp-plugin/`, `wp-theme/` implemented independently against the contract.
3. **Blind QA:** acceptance criteria above verified against the running stack by an isolated reviewer.

---
*Next artifact: `architecture.md` (Winston) — queue mechanism, idempotency strategy, service stack decision, docker topology, payload contract.*
