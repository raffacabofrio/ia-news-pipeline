---
baseline_commit: ff92d1a2d129aa6bc8628b55b21f33c002983080
---

# Story 3.1: Theme scaffold + build

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Michel (the evaluator),
I want the WordPress theme to have a real local asset pipeline and committed built assets,
so that the site already looks intentionally custom and the evaluator can see the theme without needing Node at runtime.

## Acceptance Criteria

1. `wp-theme/ia-news-theme/` becomes a valid custom theme scaffold with a reproducible local build based on Vite, Bootstrap installed as an npm dev dependency, and theme customization driven through Sass variables only (no CDN assets). [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
2. Theme activation from the existing compose/bootstrap flow remains intact: slug stays `ia-news-theme`, required WordPress theme files still exist, and front-end styles/scripts load from built local assets committed in the repo. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1; _bmad-output/implementation-artifacts/0-1-spike-one-command-environment.md#Constraints the dev agent MUST follow; C:/Repos/ia-news-pipeline/docker/wp-init.sh]
3. `npm run build` executed inside `wp-theme/ia-news-theme/` reproduces the committed production assets without requiring a running Vite dev server, and the repository includes the files needed for deterministic npm install/build (`package.json` and lockfile). [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1; _bmad-output/planning-artifacts/prd.md#4-scope; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
4. Scope stays scaffold-only for Epic 3: this story prepares the asset/tooling foundation and a minimal styled shell, but does not deliver the final `single.php` centerpiece requirements (excerpt lead, source attribution block, badge, objective visual checks), which belong to S3.2. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2; _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#Epic-Quality-Review]

## Tasks / Subtasks

- [x] Task 1: Establish the theme-local build toolchain inside `wp-theme/ia-news-theme/` (AC: 1, 3)
  - [x] Create `package.json` with npm scripts at minimum for `build`; add `dev` only if it does not complicate the POC.
  - [x] Add Vite config scoped to this theme folder and output built assets into a committed theme-local directory such as `assets/dist/`.
  - [x] Add Bootstrap as an npm dev dependency and compile from source Sass, not via CDN.
  - [x] Commit the npm lockfile so `npm install`/`npm run build` are reproducible for later reviewers.
- [x] Task 2: Wire WordPress to production-built assets correctly (AC: 2, 3)
  - [x] Add `functions.php` and enqueue front-end assets using WordPress hooks (`wp_enqueue_scripts`) and production file paths/URIs only.
  - [x] Use the Vite build output in a WordPress-safe way; if hashed filenames are emitted, resolve them via the generated manifest instead of hardcoding unstable names.
  - [x] Do not depend on a local dev server, HMR, or Node running during normal WordPress page requests.
- [x] Task 3: Preserve theme validity while replacing the placeholder shell (AC: 1, 2, 4)
  - [x] Keep `style.css` at the theme root with a valid theme header so WP activation continues to work.
  - [x] Update `index.php` from placeholder status to a minimal but intentional Bootstrap-backed shell that proves assets load, while keeping it simple enough for S3.2 to own the final presentation work.
  - [x] Add only the template files that reduce later churn (`functions.php`, optionally `header.php`/`footer.php`/`single.php` stubs) without pre-implementing the S3.2 centerpiece.
- [x] Task 4: Commit built assets and verify the evaluator path (AC: 2, 3)
  - [x] Run `npm run build` in `wp-theme/ia-news-theme/` and commit the generated production assets.
  - [x] Verify the active theme still renders in the existing compose environment and that the page loads the compiled assets from the theme directory.
  - [x] Record the exact verification commands/outcomes in the story completion notes for the dev-story run.

### Review Findings

- [x] [Review][Patch] Declare the supported Node floor in `wp-theme/ia-news-theme/package.json` so the pinned Vite toolchain fails early on unsupported runtimes.
- [x] [Review][Patch] Harden `wp-theme/ia-news-theme/functions.php` so unreadable, empty, or corrupted Vite manifest files fail closed instead of leaking runtime warnings or partial asset state.
- [x] [Review][Patch] Expand theme coverage with a runtime-oriented PHP verification path that exercises manifest parsing and enqueue behavior closer to the WordPress environment required by AC2.
- [x] [Review][Note] Replaced the theme-local Sass `@import` with `@use`; remaining deprecation warnings come from upstream Bootstrap 5.3.8 internals and are documented as residual scope outside this story.

## Dev Notes

### Why this story exists

- PRD FR4 and AC7 make the WordPress single-post experience the only explicit user-facing UI in the project. S3.1 exists to create the tooling and scaffold that make S3.2's visual centerpiece feasible without improvising an asset pipeline mid-story. [Source: _bmad-output/planning-artifacts/prd.md#5-functional-requirements; _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- Architecture already froze the implementation direction: a classic WordPress theme, Bootstrap as a dev dependency, customization through Sass variables, Vite build, and compiled assets committed so the evaluator does not need Node just to see the theme. This story is the moment those constraints become concrete. [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]

### Scope boundaries (do NOT build)

- Do not implement the final single-post visual system here. The excerpt lead, source attribution block, AI badge, readable measure checks, and 375px mobile verification belong to S3.2. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- Do not touch service or plugin behavior, webhook contracts, queue logic, or compose topology. Epic 3 is independent and should stay independent. [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen; _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#Epic-Quality-Review]
- Do not rename the theme slug, directory, text domain, or activation assumptions. `wp-init` already activates `ia-news-theme` by exact slug. [Source: C:/Repos/ia-news-pipeline/docker/wp-init.sh; _bmad-output/implementation-artifacts/0-1-spike-one-command-environment.md#Constraints the dev agent MUST follow]

### Existing code and update-file intelligence

#### `wp-theme/ia-news-theme/style.css` (UPDATE)

- Current state: root file only contains the WordPress theme header metadata; there is no compiled CSS import, no theme variables, and no runtime styling. This file is currently the activation anchor that allows WordPress to recognize the theme. [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/style.css]
- What this story changes: keep the header valid while introducing the real asset pipeline. The file may remain mostly metadata-only if compiled CSS is enqueued from `functions.php`; that is preferable to cramming build output into the header file.
- What must be preserved: valid theme header at the root, same theme name/slug/text domain, no move/rename of the file.

#### `wp-theme/ia-news-theme/index.php` (UPDATE)

- Current state: placeholder template with basic loop, no Bootstrap classes, and explicit comments that the real theme arrives in S3.1/S3.2. It currently proves the placeholder theme can render posts. [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/index.php]
- What this story changes: replace the placeholder feel with a minimal scaffold that demonstrates the built CSS/JS path is working and gives S3.2 a cleaner base.
- What must be preserved: valid fallback rendering, `wp_head()`/`wp_footer()`, standard loop behavior, and the ability to render when `single.php` is not yet fully specialized.

#### `docker/wp-init.sh` and compose bootstrap assumptions (READ-ONLY guardrails)

- Current state: `wp-init` activates the theme by exact slug `ia-news-theme`; `docker-compose.yml` bind-mounts the theme directory into `/var/www/html/wp-content/themes/ia-news-theme`. [Source: C:/Repos/ia-news-pipeline/docker/wp-init.sh; C:/Repos/ia-news-pipeline/docker-compose.yml]
- What this story changes: nothing directly, unless the dev-story has an unavoidable consistency fix.
- What must be preserved: folder name, root theme files required for activation, and the ability for the existing compose stack to show the theme without any Node process.

### Architecture compliance

- Repository layout is frozen: theme work lives under `wp-theme/ia-news-theme/`. Do not introduce a separate frontend app elsewhere in the repo. [Source: _bmad-output/planning-artifacts/architecture.md#6-repository-layout]
- The theme must stay boring and evaluator-friendly: classic PHP theme, locally built assets, no CDN dependency, no runtime build step on the evaluator machine. [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side; _bmad-output/planning-artifacts/prd.md#2-users]
- Theme assets are part of the product evidence. Since compiled assets are committed, the dev agent owns both source assets and the generated build output in the same story. [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]

### File structure requirements

- Expected NEW files in this story:
  - `wp-theme/ia-news-theme/package.json`
  - `wp-theme/ia-news-theme/package-lock.json`
  - `wp-theme/ia-news-theme/vite.config.*`
  - `wp-theme/ia-news-theme/functions.php`
  - `wp-theme/ia-news-theme/src/` for theme Sass/JS sources
  - `wp-theme/ia-news-theme/assets/dist/` for committed build output
- Expected UPDATED files in this story:
  - `wp-theme/ia-news-theme/style.css`
  - `wp-theme/ia-news-theme/index.php`
- Optional NEW files only if they reduce churn for S3.2 without delivering its scope early:
  - `header.php`
  - `footer.php`
  - `single.php` stub

### Technical requirements

- Use npm because the story's acceptance criterion explicitly requires `npm run build` as the reproducibility path.
- Keep the build self-contained inside the theme folder. The repo currently has no Node/Vite setup anywhere, so the theme must not assume a root-level JS workspace.
- Build assets must be referenced through WordPress theme helpers such as `get_theme_file_uri()`/`get_theme_file_path()` and enqueued through `wp_enqueue_scripts`, not hardcoded as relative HTML tags in templates. [Source: https://developer.wordpress.org/reference/hooks/wp_enqueue_scripts/; https://developer.wordpress.org/reference/functions/wp_enqueue_style/; https://developer.wordpress.org/reference/functions/wp_enqueue_script/]
- If the Vite build emits hashed filenames, load them through the Vite manifest in PHP. Hardcoding hashed output names would make repeated builds fragile.
- Preserve a clean separation:
  - `style.css` = WP metadata / root theme file
  - source Sass/JS = under `src/`
  - compiled production assets = committed under a deterministic output directory

### Testing requirements

- Minimum verification for the dev-story run:
  - `npm install` (or `npm ci` once lockfile exists) succeeds inside `wp-theme/ia-news-theme/`
  - `npm run build` succeeds and regenerates the committed assets
  - `docker compose up` path still results in `ia-news-theme` as the active theme
  - Front-end HTML loads the compiled local asset files from the theme directory
- This story does not require blind aesthetic QA yet. The goal is scaffolding correctness and reproducibility; the stronger visual assertions land in S3.2 and S4.3. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2; _bmad-output/planning-artifacts/epics-stories.md#S4.3]

### Latest tech information

- Vite's current official Getting Started guide says current Vite requires Node.js `20.19+` or `22.12+`. The theme build should therefore avoid older Node assumptions and record the expected Node floor if a tooling note is added. [Source: https://vite.dev/guide/]
- Vite's official release pages currently show the latest stable line as `v8.1.3` (released 2026-07-02). Unless the implementation finds a compatibility blocker with WordPress/PHP theme usage, prefer the current stable Vite major rather than scaffolding around an older tutorial. [Source: https://github.com/vitejs/vite/releases]
- Bootstrap's official site currently advertises npm install with `bootstrap@5.3.8`. Use Bootstrap from npm source files, not the CDN build, because architecture requires Sass-variable customization. [Source: https://getbootstrap.com/; https://getbootstrap.com/docs/5.3/customize/sass/]
- Sass officially deprecated `@import` in Dart Sass `1.80.0`, and the legacy JS API is deprecated with warnings since `1.79.0`. Avoid custom build glue that relies on deprecated Sass APIs; let Vite's current Sass integration handle compilation and keep the theme's own Sass code modern where practical. [Source: https://sass-lang.com/blog/import-is-deprecated/; https://sass-lang.com/documentation/breaking-changes/legacy-js-api/]
- WordPress front-end scripts/styles should be enqueued on the `wp_enqueue_scripts` hook rather than printed directly in templates. [Source: https://developer.wordpress.org/reference/hooks/wp_enqueue_scripts/]

### Git intelligence summary

- Recent implementation work has been additive bootstrap work only. There is no pre-existing JS toolchain in the repo, so S3.1 will set the initial conventions for theme asset packaging. [Source: `git log --oneline -5`]
- The latest commit after S0.2 adjusted BMAD agent behavior only; it did not introduce code conventions for the theme, so Epic 3 still owns its own local build conventions. [Source: `git show --stat --name-only --oneline HEAD`]

### Project Structure Notes

- No UX artifact exists. That is acceptable for this story because the build scaffold is objective, but it increases the importance of leaving S3.2 room to express the visual centerpiece clearly. [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#UX-Alignment-Assessment]
- Because the evaluator persona wins, favor the most boring reproducible asset pipeline over clever frontend tooling. If there is a choice between a fancy setup and a dead-simple committed build that works inside WordPress, choose the latter. [Source: _bmad-output/planning-artifacts/prd.md#2-users]

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S4.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#2-decision-log]
- [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/architecture.md#6-repository-layout]
- [Source: _bmad-output/planning-artifacts/prd.md#2-users]
- [Source: _bmad-output/planning-artifacts/prd.md#4-scope]
- [Source: _bmad-output/planning-artifacts/prd.md#5-functional-requirements]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md#UX-Alignment-Assessment]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/index.php]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/style.css]
- [Source: C:/Repos/ia-news-pipeline/docker-compose.yml]
- [Source: C:/Repos/ia-news-pipeline/docker/wp-init.sh]
- [Source: https://vite.dev/guide/]
- [Source: https://github.com/vitejs/vite/releases]
- [Source: https://getbootstrap.com/]
- [Source: https://getbootstrap.com/docs/5.3/customize/sass/]
- [Source: https://sass-lang.com/blog/import-is-deprecated/]
- [Source: https://sass-lang.com/documentation/breaking-changes/legacy-js-api/]
- [Source: https://developer.wordpress.org/reference/hooks/wp_enqueue_scripts/]
- [Source: https://developer.wordpress.org/reference/functions/wp_enqueue_style/]
- [Source: https://developer.wordpress.org/reference/functions/wp_enqueue_script/]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Story target was explicitly provided by the user as `3-1-theme-scaffold-build`; no auto-discovery was used for story selection.
- No `project-context.md` files were present in the workspace during story creation.
- No dedicated UX artifact exists; S3.1 therefore focuses on objective scaffold/build guidance and leaves the subjective visual criteria to S3.2.
- Existing theme code is still the placeholder created in S0.1: only `style.css` and `index.php` exist under `wp-theme/ia-news-theme/`; no `functions.php`, no `package.json`, no Vite config, no Sass source tree.
- Review follow-up pass targeted findings around Node runtime declaration, manifest-read hardening, and stronger runtime-oriented coverage before closing the story.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created
- Story packaged with explicit guardrails for preserving theme activation, keeping compiled assets committed, and avoiding premature implementation of S3.2's visual centerpiece.
- Implemented a theme-local Vite + Bootstrap + Sass pipeline under `wp-theme/ia-news-theme/`, with deterministic `package-lock.json` and committed production output in `assets/dist/`.
- Added `functions.php` manifest loading so WordPress enqueues hashed build artifacts from the theme directory only, with no CDN, HMR, or Node runtime dependency during page requests.
- Replaced the placeholder fallback with a minimal editorial shell in `index.php` and added low-churn `header.php`, `footer.php`, and `single.php` stubs without pre-building the S3.2 centerpiece.
- Added a lightweight Node test (`npm test`) that verifies theme metadata, manifest-aware asset wiring, minimal shell presence, and committed build output.
- Verification commands and outcomes:
  - `npm install` in `wp-theme/ia-news-theme/` succeeded and generated `package-lock.json`.
  - `npm test` passed (`4/4`).
  - `npm run build` passed and regenerated `assets/dist/.vite/manifest.json`, `assets/dist/assets/theme-BwSOVTfs.css`, and `assets/dist/assets/theme-BlpOkkor.js`.
  - `dotnet test service/IaNewsPipeline.sln` passed (`9/9`); existing `AWSSDK.Core` low-severity vulnerability warnings remained pre-existing and unrelated to this story.
- `docker compose up -d mysql elasticmq wordpress wp-init` completed after one transient Docker recreate conflict on `wordpress`; the retry succeeded.
- `docker compose exec -T wordpress php -r "require '/var/www/html/wp-load.php'; echo wp_get_theme()->get_stylesheet();"` returned `ia-news-theme`.
- `Invoke-WebRequest http://localhost:8080` returned HTML containing `wp-content/themes/ia-news-theme/assets/dist/assets/theme-BwSOVTfs.css` and `wp-content/themes/ia-news-theme/assets/dist/assets/theme-BlpOkkor.js`.
- Bootstrap 5.3.8 currently emits Sass deprecation warnings during `npm run build` because its upstream Sass sources still rely on deprecated APIs; the build remains successful and reproducible.
- Review follow-up changes:
  - Added `engines.node` to `package.json` with the Vite-supported Node floor (`>=20.19.0 || >=22.12.0`).
  - Split manifest loading into a fail-closed helper in `functions.php` so unreadable, empty, and corrupted manifest content resolves to an empty asset set safely.
  - Added `tests/theme-functions-runtime.php` and verified it through the real PHP runtime in the `wordpress` container.
  - Switched `src/styles/main.scss` from `@import` to `@use`; the remaining Sass deprecation warnings now come only from upstream Bootstrap 5.3.8 internals.
- Review follow-up verification commands and outcomes:
  - `npm test` passed (`5/5`).
  - `npm run build` passed and regenerated `assets/dist/.vite/manifest.json`, `assets/dist/assets/theme-B4A9oVHy.css`, and `assets/dist/assets/theme-BvJ0kMl2.js`.
  - `docker compose exec -T wordpress php -l /var/www/html/wp-content/themes/ia-news-theme/functions.php` passed with no syntax errors.
  - `docker compose exec -T wordpress php /var/www/html/wp-content/themes/ia-news-theme/tests/theme-functions-runtime.php` passed.
  - `docker compose exec -T wordpress php -r "require '/var/www/html/wp-load.php'; echo wp_get_theme()->get_stylesheet();"` returned `ia-news-theme`.
  - `curl.exe -s http://localhost:8080` returned HTML containing the rebuilt local asset URLs for the theme CSS and JS bundles.
  - `dotnet test service/IaNewsPipeline.sln` passed (`11/11`); the pre-existing low-severity `AWSSDK.Core` advisory warning remains unrelated to this story.

### File List

- .gitignore (modified)
- _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md (modified)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)
- wp-theme/ia-news-theme/assets/dist/.vite/manifest.json (new)
- wp-theme/ia-news-theme/assets/dist/assets/theme-B4A9oVHy.css (new)
- wp-theme/ia-news-theme/assets/dist/assets/theme-BvJ0kMl2.js (new)
- wp-theme/ia-news-theme/footer.php (new)
- wp-theme/ia-news-theme/functions.php (new)
- wp-theme/ia-news-theme/header.php (new)
- wp-theme/ia-news-theme/index.php (modified)
- wp-theme/ia-news-theme/package-lock.json (new)
- wp-theme/ia-news-theme/package.json (new)
- wp-theme/ia-news-theme/single.php (new)
- wp-theme/ia-news-theme/src/scripts/main.js (new)
- wp-theme/ia-news-theme/src/styles/main.scss (new)
- wp-theme/ia-news-theme/style.css (modified)
- wp-theme/ia-news-theme/tests/theme-functions-runtime.php (new)
- wp-theme/ia-news-theme/tests/theme-scaffold.test.mjs (new)
- wp-theme/ia-news-theme/vite.config.mjs (new)

## Change Log

- 2026-07-07: Story created via `bmad-create-story` for Epic 3 S3.1. Status -> ready-for-dev.
- 2026-07-07: Implemented the WordPress theme-local Vite/Bootstrap/Sass scaffold, committed production assets, and moved status to `review`.
- 2026-07-07: Closed review follow-ups for Node engine declaration, manifest hardening, and runtime-oriented theme coverage; story advanced to `done`.
