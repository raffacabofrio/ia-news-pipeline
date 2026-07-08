---
baseline_commit: ac5c0e8f7b1373fa184437da65de2b327841adbb
---

# Story 0.1: Spike: one-command environment

Status: done

**Timebox: 90 minutes.** This is a spike with an explicit degradation path (see "Degradation" below). Track your time; if the timebox blows, degrade — do not keep polishing.

## Story

As Michel (the evaluator),
I want the entire local stack to come up from a clean clone with a single `docker compose up`,
so that I can run and re-run the pipeline autonomously with no undocumented manual steps (PRD NFR2, AC1 precondition, AC6).

## Acceptance Criteria

1. From a clean clone of the repo, `docker compose up` (with `.env` copied from a documented example if needed) brings up the full stack with **no manual steps**: `wordpress`, `mysql`, `elasticmq`, and one-shot `wp-init`.
2. MySQL runs as **one instance with two schemas**: `wordpress` (used by WP) and `pipeline` (empty for now; jobs table arrives in S1.1). Both schemas exist after first boot. [Source: architecture.md#1-topology]
3. ElasticMQ exposes an SQS-compatible endpoint and answers `ListQueues`, showing a main queue and a DLQ wired by redrive policy with `maxReceiveCount: 5`. [Source: architecture.md#2-decision-log D2]
4. `wp-init` (one-shot WP-CLI container) completes: WP core installed, plugin `ia-pipeline-receiver` activated, theme `ia-news-theme` activated, permalink structure set to `/%postname%/`. Placeholder plugin/theme are OK — minimal valid headers only, no behavior.
5. After `docker compose up` from a clean clone: WordPress is reachable in the browser, the placeholder plugin shows as active, the placeholder theme is the active theme.
6. Re-running `docker compose up` on an already-initialized stack is safe (wp-init is idempotent or exits cleanly when WP is already installed).

## Tasks / Subtasks

- [x] Task 1: Repo layout for docker assets (AC: 1)
  - [x] Create `docker-compose.yml` at repo root; supporting files under `docker/` (`elasticmq.conf`, `wp-init.sh`, MySQL init SQL) per architecture §6 layout
  - [x] Create `.env` handling: compose reads config from env with sensible defaults so `docker compose up` works without editing (document any required var)
- [x] Task 2: MySQL service (AC: 2)
  - [x] Official `mysql` image (pin a specific 8.x tag), volume for data
  - [x] Init script in `/docker-entrypoint-initdb.d/` creating the `pipeline` schema (the `wordpress` schema comes from `MYSQL_DATABASE`); grant the WP/service user access to both
- [x] Task 3: ElasticMQ service (AC: 3)
  - [x] `softwaremill/elasticmq-native` image (pin tag), port 9324 exposed
  - [x] `docker/elasticmq.conf` (HOCON) defining main queue + DLQ with `deadLettersQueue { name = ..., maxReceiveCount = 5 }`; mount at `/opt/elasticmq.conf`
  - [x] Verify: `aws sqs list-queues --endpoint-url http://localhost:9324` (or plain `curl "http://localhost:9324/?Action=ListQueues"`) returns both queues
- [x] Task 4: WordPress service (AC: 5)
  - [x] Official `wordpress` image (pin tag), depends on mysql, port 8080→80 (or similar documented port)
  - [x] Bind-mount `wp-plugin/ia-pipeline-receiver` → `/var/www/html/wp-content/plugins/ia-pipeline-receiver` and `wp-theme/ia-news-theme` → `/var/www/html/wp-content/themes/ia-news-theme`
- [x] Task 5: Placeholder plugin + theme (AC: 4, 5)
  - [x] `wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php` with a valid plugin header comment only
  - [x] `wp-theme/ia-news-theme/` with minimal valid theme: `style.css` (theme header) + `index.php` (WP requires both)
- [x] Task 6: wp-init one-shot container (AC: 4, 6)
  - [x] Use the `wordpress:cli` image variant (must share the wordpress container's volume/mounts and DB env)
  - [x] `docker/wp-init.sh`: wait for DB/WP readiness → `wp core is-installed || wp core install ...` → `wp plugin activate ia-pipeline-receiver` → `wp theme activate ia-news-theme` → `wp rewrite structure '/%postname%/' --hard`
  - [x] Container exits 0 when done; safe on re-run (guard with `wp core is-installed`)
- [x] Task 7: Clean-clone verification (AC: 1, 5, 3)
  - [x] `docker compose down -v` then `docker compose up` from scratch; confirm WP reachable, plugin+theme active (`wp plugin list` / browser), ElasticMQ ListQueues answers
- [x] Task 8 (only if timebox blows): Degradation path — NOT TRIGGERED (see Completion Notes)
  - [x] Drop wp-init; document a 3-step manual wizard (browser install → activate plugin/theme → set permalinks) in a `docker/README-bootstrap.md` note for later merge into root README (S4.2)

### Review Findings

- [x] [Review][Patch] `MYSQL_USER` configurável no compose, mas grant do schema `pipeline` segue hardcoded para `'wordpress'` [docker-compose.yml:27]
- [x] [Review][Patch] `WP_PORT` pode ser sobrescrito sem atualizar o `WP_URL` usado no `wp core install`, deixando a instância instalada com URL/permalinks inconsistentes [docker-compose.yml:79]

## Dev Notes

### Why this story exists
- PRD NFR2: evaluator goes from `git clone` to working pipeline with one compose command; no undocumented manual steps. PRD Risk table: "WordPress/docker bootstrap friction eats the morning → Bootstrap solved first, before feature code."
- Architecture D5: `wp-init` one-shot container (WP-CLI) is a spike, timeboxed, with graceful degradation = 3-step manual wizard in README.
- Architecture D2: queue must be SQS-API via ElasticMQ **by default** (evaluator re-runs autonomously, no AWS credentials). `SQS_ENDPOINT` env var later selects ElasticMQ vs real SQS with zero code change — nothing in this story may hardcode ElasticMQ assumptions into anything other than compose/env defaults.

### Scope boundaries (do NOT build)
- **No `service/` container yet** — the .NET service joins compose later (S0.2 scaffolds the solution; Epic 1 implements). Structure `docker-compose.yml` so a `service` entry can be added without rework (network, env conventions).
- **No plugin behavior, no theme styling** — placeholders only. Real plugin is S2.1; real theme is S3.1/S3.2. Do not create REST routes, HMAC code, or SASS/Vite setup here.
- **No jobs table** — only the empty `pipeline` schema. Table DDL belongs to S1.1.
- **No CI, no `.env.example` for service vars** — that is S0.2.

### Constraints the dev agent MUST follow
- Repository layout is frozen by architecture §6: `docker/` (elasticmq.conf, wp-init.sh, mysql init), `docker-compose.yml` at root, `wp-plugin/ia-pipeline-receiver/`, `wp-theme/ia-news-theme/`. Do not invent different paths — three parallel workstreams depend on these exact directories.
- Plugin slug `ia-pipeline-receiver` and theme slug `ia-news-theme` are contract-adjacent names used by S2.1/S3.1 and wp-init activation — exact spelling matters.
- One MySQL instance, two schemas (`wordpress`, `pipeline`) — not two MySQL containers.
- Redrive `maxReceiveCount: 5` is a frozen decision (D2); visibility-timeout tuning is free.
- Permalink structure `/%postname%/` is required (pretty URLs; the webhook response `post_url` in contract §5.1 will rely on working permalinks).
- Secrets/config only via env vars (NFR4). No secrets committed; compose defaults for local-only values (e.g., local MySQL password) are acceptable and documented.

### Suggested queue naming (assumption — architecture does not name the queues)
- Main: `pipeline-jobs` · DLQ: `pipeline-jobs-dlq`. Whatever you choose, it becomes the `QUEUE_NAME` default consumed by S0.2's `.env.example` and Epic 1 — record the chosen name clearly in compose comments.

### ElasticMQ config sketch (HOCON, `docker/elasticmq.conf`)
```hocon
include classpath("application.conf")
queues {
  pipeline-jobs {
    deadLettersQueue { name = pipeline-jobs-dlq, maxReceiveCount = 5 }
  }
  pipeline-jobs-dlq {}
}
```

### wp-init pattern
- Run the `cli` variant of the same pinned WordPress image version to avoid core-version mismatch.
- It must mount the same `wp-content` bind mounts as the wordpress service and receive the same `WORDPRESS_DB_*` env, plus a site URL env (e.g., `http://localhost:8080`).
- Readiness: loop on `wp db check` (or mysqladmin ping) before `wp core install`; WordPress's own container must have finished copying core files — looping on `wp core version` also works.
- `restart: "no"` + `depends_on` mysql/wordpress. Compose will show it exiting 0 — that is expected; note it in a comment.

### Testing standards for this story
- No unit tests here (spike; test strategy §8 targets service logic from S1.3 on). Verification is behavioral: the "Done when" checks in the ACs, executed from a clean clone (`docker compose down -v` first).

### Project Structure Notes

- New files this story creates (all NEW, nothing to preserve — repo currently contains only docs/planning artifacts):
  - `docker-compose.yml`
  - `docker/elasticmq.conf`, `docker/wp-init.sh`, `docker/mysql-init/01-pipeline-schema.sql` (name free within `docker/`)
  - `wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php`
  - `wp-theme/ia-news-theme/style.css`, `wp-theme/ia-news-theme/index.php`
- Git history is planning-only (5 docs commits); no code conventions exist yet — this story sets the compose/env conventions everyone else inherits.

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#Epic-0 — S0.1 definition, timebox, degradation]
- [Source: _bmad-output/planning-artifacts/architecture.md#1-topology — compose services, one MySQL/two schemas]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log — D2 (ElasticMQ default, redrive 5), D5 (wp-init spike)]
- [Source: _bmad-output/planning-artifacts/architecture.md#6-repository-layout — frozen paths]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR2, NFR4, AC1, AC6, Risks]
- Contract §5 (architecture.md) is frozen but not exercised by this story; only the plugin/theme slugs and permalink requirement touch it indirectly.

## Dev Agent Record

### Agent Model Used

claude-fable-5 (Claude Fable 5, Claude Code dev-story workflow)

### Debug Log References

- First `docker compose up` run: wp-init timed out on the DB wait. Root cause: `wp db check` shells out to the mariadb client (11.4+ in `wordpress:cli-2.12-php8.3`), which now verifies TLS certificates by default and rejects MySQL 8.4's self-signed cert (`mariadb-check: Got error: 2026: TLS/SSL error: self-signed certificate in certificate chain`). Fix: DB readiness probe rewritten as a pure-PHP `mysqli_connect` loop (same driver WordPress itself uses), no mariadb binary involved.
- Second run surfaced `Warning: Unable to create directory wp-content/uploads/2026/07` — uid mismatch (alpine cli image www-data = 82 vs debian apache image www-data = 33). Fix: `user: "33:33"` on the wp-init service. Warning gone on the final clean run.

### Completion Notes List

- Full stack (mysql 8.4, softwaremill/elasticmq-native 1.6.5, wordpress 6.8-apache, wordpress:cli-2.12-php8.3 as wp-init) comes up from `docker compose up` with zero manual steps and zero required `.env` — every variable has a `${VAR:-default}` local-only fallback documented in the compose header (AC1).
- AC2 verified: `SHOW DATABASES` lists `wordpress` + `pipeline`; user `wordpress@%` has ALL PRIVILEGES on both schemas (grant added by `docker/mysql-init/01-pipeline-schema.sql`).
- AC3 verified: `GET http://localhost:9324/?Action=ListQueues` returns `pipeline-jobs` and `pipeline-jobs-dlq`; `GetQueueAttributes` on `pipeline-jobs` shows `RedrivePolicy {"deadLetterTargetArn":"...pipeline-jobs-dlq","maxReceiveCount":5}` (frozen D2 value).
- AC4/AC5 verified: wp-init exits 0; `wp plugin list` shows `ia-pipeline-receiver active`, `wp theme list` shows `ia-news-theme active`, `wp option get permalink_structure` = `/%postname%/`; `http://localhost:8080/` returns HTTP 200 rendering the placeholder theme. `.htaccess` present with WordPress rewrite rules.
- AC6 verified: re-running `docker compose up` on the initialized stack re-runs wp-init which exits 0 ("already installed — skipping core install"; activate steps are no-ops with non-fatal warnings).
- Task 8 (degradation path) NOT TRIGGERED: total elapsed well inside the 90-minute timebox and wp-init proved reliable across clean boot + re-run; checkbox marked only to close the conditional — no `docker/README-bootstrap.md` was created because the manual wizard is unnecessary. (Deviation note: a short "NOT TRIGGERED" annotation was added to the Task 8 line to avoid a misleading plain [x].)
- Queue names chosen (story assumption confirmed): main `pipeline-jobs`, DLQ `pipeline-jobs-dlq` — recorded in compose header + elasticmq.conf comments as the `QUEUE_NAME` default for S0.2/Epic 1.
- Added `.gitattributes` forcing LF on `*.sh`/`*.sql`/`*.conf`/compose so a clean clone on Windows (evaluator scenario) doesn't break the in-container scripts — treated as part of Task 1 (repo layout / clean-clone guarantee).
- No unit tests by design: story's Testing Standards define behavioral verification only (spike); all "Done when" checks executed for real against the running stack.
- Scope respected: no `service/` container, no plugin behavior, no jobs table, no CI/.env.example. Compose network/env conventions leave room for the S0.2+ `service` entry (`SQS_ENDPOINT=http://elasticmq:9324`, `QUEUE_NAME=pipeline-jobs`).
- Follow-ups for S0.2: reuse queue name defaults above in `.env.example`; note that `MYSQL_USER` rename requires touching `docker/mysql-init/01-pipeline-schema.sql` (documented in compose comment).

### File List

- docker-compose.yml (new)
- docker/elasticmq.conf (new)
- docker/mysql-init/01-pipeline-schema.sql (new)
- docker/wp-init.sh (new)
- wp-plugin/ia-pipeline-receiver/ia-pipeline-receiver.php (new)
- wp-theme/ia-news-theme/style.css (new)
- wp-theme/ia-news-theme/index.php (new)
- .gitattributes (new)
- _bmad-output/implementation-artifacts/0-1-spike-one-command-environment.md (modified — status, checkboxes, Dev Agent Record)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — story status transitions)

## Change Log

- 2026-07-07: S0.1 implemented — one-command compose stack (mysql dual-schema, ElasticMQ with DLQ redrive 5, WordPress with placeholder plugin/theme bind mounts, idempotent one-shot wp-init). All 6 ACs verified against a clean `docker compose down -v && docker compose up` boot plus an idempotent re-run. Status → review.
- 2026-07-07: Addressed code review findings - 2 items resolved (Date: 2026-07-07)
