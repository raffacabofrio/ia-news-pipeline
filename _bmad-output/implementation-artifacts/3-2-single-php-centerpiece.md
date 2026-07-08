---
baseline_commit: 8630f1c39b9cdc2d2e7e35b3c70f913d0b919a00
---

# Story 3.2: single.php centerpiece

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Michel (the evaluator),
I want a polished single-post page that clearly looks intentional and production-minded,
so that the generated WordPress post reads like the visual centerpiece of the challenge instead of a default blog template.

## Acceptance Criteria

1. `wp-theme/ia-news-theme/single.php` becomes the visual centerpiece of the site and renders a polished single-post layout, not the current minimal scaffold from S3.1. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side; _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
2. The content column has a constrained readable measure and does not read as full-width on desktop; the layout remains stable and readable at `375px` mobile width. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
3. The page renders the post excerpt as a visibly styled lead paragraph near the top of the article, using the actual WordPress excerpt field already persisted by the pipeline/plugin path. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
4. A source attribution block is present on the single-post page and links the `source_url` post meta value stored by the pipeline receiver; the block must fail gracefully when the meta is absent instead of fatally breaking the template. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2; _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
5. An "AI-generated" badge is visibly present in the article chrome. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
6. Story verification remains independent from Epics 1 and 2: objective checks must pass with a manually created post carrying the pipeline meta fields (`_pipeline_job_id`, `_pipeline_source_url`, `_pipeline_model` / model metadata), without requiring a live pipeline-created post. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
7. The S3.1 asset/build path remains intact: theme slug stays `ia-news-theme`, assets still load from committed local build output, and any styling/template changes continue to work with `npm run build` plus the existing WordPress bootstrap flow. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1; _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Acceptance-Criteria]

## Tasks / Subtasks

- [x] Task 1: Replace the S3.1 single-post stub with a real centerpiece template (AC: 1, 5)
  - [x] Evolve `wp-theme/ia-news-theme/single.php` from the minimal stub into a composed editorial layout with stronger hierarchy, article chrome, and dedicated metadata regions.
  - [x] Keep the implementation as a classic PHP theme template; do not introduce blocks, a JS SPA shell, or new runtime dependencies.
  - [x] Preserve standard WordPress template behavior (`have_posts()`, `the_post()`, `the_content()`, `get_header()`, `get_footer()`).
- [x] Task 2: Enforce objective readability and responsive checks through theme structure and styles (AC: 2, 7)
  - [x] Add or refine theme classes in `single.php` and `src/styles/main.scss` so the article body sits in a constrained reading column on larger screens.
  - [x] Ensure the mobile presentation holds at `375px` width without clipped chrome, horizontal overflow, or unreadable spacing.
  - [x] Rebuild and commit any changed theme assets through the existing Vite flow if source assets change.
- [x] Task 3: Surface excerpt and pipeline attribution metadata correctly (AC: 3, 4, 5)
  - [x] Render the excerpt as a distinct lead paragraph only when present; avoid duplicating body copy if the excerpt is empty.
  - [x] Read the source URL from post meta with WordPress-native template helpers and render a source attribution block with a safe external link.
  - [x] Surface an "AI-generated" badge in a way that remains visible on desktop and mobile.
- [x] Task 4: Prove the story with independent, objective verification (AC: 2, 4, 6, 7)
  - [x] Verify against a manually created post carrying `_pipeline_job_id`, `_pipeline_source_url`, and model metadata instead of depending on a live pipeline publish.
  - [x] Add or extend focused tests/checks so the objective requirements are guarded: readable-measure class/hooks present, excerpt lead rendered, attribution block wired to meta, badge visible, and the template remains resilient when source meta is missing.
  - [x] Keep S3.1's existing build/runtime verification green.

## Dev Notes

### Why this story exists

- PRD AC7 and Epic 3 make the single-post page the only explicit visual centerpiece in the whole challenge. This story is where the evaluator should feel that the project has an intentional front-end point of view rather than only backend rigor. [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral; _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- S3.1 already delivered the tooling and activation-safe scaffold. S3.2 must spend that budget on presentation quality and objective runtime checks, not on reinventing the build pipeline. [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Completion-Notes-List; _bmad-output/planning-artifacts/epics-stories.md#S3.1]

### Scope boundaries (do NOT build)

- Do not touch the service, queue, plugin receiver behavior, or payload contract. This story consumes the post and its metadata exactly as they already exist. [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen; _bmad-output/planning-artifacts/epics-stories.md#Epic-1-generation-service-net--parallel-workstream-a; _bmad-output/planning-artifacts/epics-stories.md#Epic-2-wordpress-plugin--parallel-workstream-b]
- Do not broaden the scope into a full site redesign. The centerpiece is `single.php`; keep surrounding theme churn minimal unless a tiny support tweak in shared styles/templates is required. [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
- Do not make verification depend on a pipeline-created post. The epic explicitly allows manual post setup with pipeline meta fields so this story stays independently verifiable. [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- Do not remove or weaken the S3.1 manifest/build/runtime safeguards just to speed up template work. [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Review-Findings]

### Previous story intelligence

- `3.1` already established the theme slug, Vite manifest loading, local asset enqueueing, `post-thumbnails` support, and committed build output. Reuse those conventions and keep `single.php` inside the existing classic theme structure. [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Acceptance-Criteria; C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/functions.php]
- The current `single.php` is intentionally minimal: date eyebrow, title, and body inside one `news-card`. That makes it the primary UPDATE target for this story. [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/single.php]
- `3.1` review follow-up already hardened theme runtime verification around manifest loading and enqueue behavior. `3.2` should extend tests toward template semantics, not reopen asset-pipeline fundamentals. [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Review-Findings; C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/tests/theme-functions-runtime.php]

### Architecture compliance

- Keep the implementation as a classic WordPress theme using template tags and post meta reads, because architecture explicitly chose a minimal WP theme with `single.php` as the centerpiece. [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side; https://developer.wordpress.org/themes/classic-themes/basics/template-tags/]
- `add_theme_support( 'post-thumbnails' )` is already enabled, so the story may use featured-image template helpers if helpful for the composition, but it must not assume every post has a thumbnail. [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/functions.php; https://developer.wordpress.org/reference/functions/get_the_post_thumbnail/]
- Read pipeline metadata through `get_post_meta()` and render it manually; do not rely on deprecated generic custom-field output helpers. [Source: https://developer.wordpress.org/reference/functions/get_post_meta/]

### Files likely UPDATED in this story

- `wp-theme/ia-news-theme/single.php`
  - Current state: minimal S3.1 stub with date, title, and content only.
  - What this story changes: becomes the primary visual composition with excerpt lead, attribution block, badge, and stronger layout structure.
  - What must be preserved: standard loop semantics and compatibility with the active classic theme shell.
- `wp-theme/ia-news-theme/src/styles/main.scss`
  - Current state: global shell/card styles from S3.1, but no dedicated single-post visual system or readable-measure rules.
  - What this story changes: adds the styling hooks needed to enforce objective layout checks and support the article chrome.
  - What must be preserved: the existing Bootstrap/Vite/Sass path and the general shell styling already validated by S3.1.
- `wp-theme/ia-news-theme/tests/theme-scaffold.test.mjs`
  - Current state: guards metadata header, manifest-aware enqueueing, placeholder-shell replacement, build outputs, and Node engine declaration.
  - What this story changes: extend or complement tests so the single-post requirements are covered by objective checks.
  - What must be preserved: existing S3.1 checks stay green.

### Files likely NEW in this story

- Optional additional verification helpers under `wp-theme/ia-news-theme/tests/` if needed to prove the single template against objective requirements.
- Avoid introducing new top-level apps, build tools, or unrelated theme directories.

### Critical implementation guardrails

- Treat `_pipeline_source_url` as the primary source-meta key because S2.1 persists that exact naming convention, even though the epic text says `source_url` conceptually. If support for a non-prefixed fallback is added for manual fixtures, it must not break the real `_pipeline_*` path. [Source: _bmad-output/implementation-artifacts/2-1-receiver-endpoint.md#Acceptance-Criteria; _bmad-output/implementation-artifacts/2-1-receiver-endpoint.md#Critical-implementation-guardrails]
- Use the real WordPress excerpt field (`get_the_excerpt()` / `the_excerpt()` behavior) for the lead paragraph rather than parsing the first body paragraph manually. [Source: https://developer.wordpress.org/reference/functions/get_the_excerpt/; https://developer.wordpress.org/reference/functions/the_excerpt/]
- Any source link rendered from post meta must be escaped and must fail gracefully when missing or malformed; the page should degrade to “no source block” or equivalent safe fallback, not PHP warnings or broken markup.
- Keep the objective checks visible in the markup/classes. Reviewers need to be able to confirm readable measure, excerpt lead, attribution, and badge without subjective guesswork.
- Prefer additive theme work over churn. If `header.php`, `footer.php`, `functions.php`, or `index.php` do not need changes, leave them alone.

### Testing requirements

- Minimum verification for the dev-story run:
  - `npm test` passes with coverage extended for the `single.php` centerpiece requirements.
  - `npm run build` passes and regenerates committed assets if Sass changes.
  - The local WordPress stack renders a manually created post with pipeline meta and shows:
    - constrained reading column
    - styled excerpt lead
    - source attribution block with link
    - visible "AI-generated" badge
    - no layout break at `375px` width
- If a helper script or runtime PHP verifier is added, keep it theme-local and focused on objective checks rather than introducing a heavyweight browser-test framework.

### Latest tech information

- WordPress classic-theme guidance still centers template tags as the preferred way to pull content into theme templates, which aligns with keeping this story in `single.php` rather than adding custom rendering layers. [Source: https://developer.wordpress.org/themes/classic-themes/basics/template-tags/]
- WordPress currently documents `get_post_meta()` for retrieving post meta and separately notes that generic `the_meta()` output is deprecated, so the attribution block should use explicit meta retrieval/rendering. [Source: https://developer.wordpress.org/reference/functions/get_post_meta/; https://developer.wordpress.org/reference/functions/the_meta/]
- WordPress documents `get_the_post_thumbnail()` / `the_post_thumbnail()` as the correct featured-image helpers when theme support exists; use them only as optional enhancement, not as a requirement for story completion. [Source: https://developer.wordpress.org/reference/functions/get_the_post_thumbnail/; https://developer.wordpress.org/reference/functions/the_post_thumbnail/]
- Bootstrap's current site still documents Sass-based customization and Vite integration for npm workflows, so any style expansion should stay inside the existing Sass/Vite pipeline rather than adding CDN or ad-hoc CSS compilation paths. [Source: https://getbootstrap.com/docs/5.3/customize/sass/; https://getbootstrap.com/docs/5.3/getting-started/vite/]

### Git intelligence summary

- Recent commits show Epic 3 already closed its scaffold/review loop before this story starts (`69e6c91`, `8630f1c`). Build on those conventions instead of reopening theme infrastructure questions. [Source: `git log --oneline -5`]
- Keep this story tightly scoped. The repo has unrelated uncommitted work elsewhere, and this front should only touch the theme files directly required for the centerpiece. [Source: `git status --short`]

### Project Structure Notes

- No dedicated UX artifact exists for this project. That means the objective checks listed in Epic 3 are the real contract for this story, and the template/styling should make those checks obvious in the rendered output. [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Project-Structure-Notes]
- Preserve the existing theme-local toolchain and committed asset strategy because Michel should be able to see the result without needing Node in normal evaluation flow. [Source: _bmad-output/planning-artifacts/prd.md#2-users; _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md#Architecture-compliance]

### References

- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.1]
- [Source: _bmad-output/planning-artifacts/epics-stories.md#S3.2]
- [Source: _bmad-output/planning-artifacts/architecture.md#4-wordpress-side]
- [Source: _bmad-output/planning-artifacts/architecture.md#5-contract-frozen]
- [Source: _bmad-output/planning-artifacts/prd.md#2-users]
- [Source: _bmad-output/planning-artifacts/prd.md#7-acceptance-criteria-behavioral]
- [Source: _bmad-output/implementation-artifacts/3-1-theme-scaffold-build.md]
- [Source: _bmad-output/implementation-artifacts/2-1-receiver-endpoint.md]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/single.php]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/functions.php]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/src/styles/main.scss]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/tests/theme-scaffold.test.mjs]
- [Source: C:/Repos/ia-news-pipeline/wp-theme/ia-news-theme/tests/theme-functions-runtime.php]
- [Source: https://developer.wordpress.org/themes/classic-themes/basics/template-tags/]
- [Source: https://developer.wordpress.org/reference/functions/get_post_meta/]
- [Source: https://developer.wordpress.org/reference/functions/get_the_excerpt/]
- [Source: https://developer.wordpress.org/reference/functions/the_excerpt/]
- [Source: https://developer.wordpress.org/reference/functions/get_the_post_thumbnail/]
- [Source: https://developer.wordpress.org/reference/functions/the_post_thumbnail/]
- [Source: https://developer.wordpress.org/reference/functions/the_meta/]
- [Source: https://getbootstrap.com/docs/5.3/customize/sass/]
- [Source: https://getbootstrap.com/docs/5.3/getting-started/vite/]

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Story target was explicitly provided as `3-2-single-php-centerpiece`; no backlog auto-discovery was needed for story selection.
- No `project-context.md` file was present anywhere in the repo during story creation.
- No dedicated UX artifact exists under `_bmad-output/planning-artifacts`; objective checks from Epic 3 were therefore treated as the full visual contract.
- `3-1-theme-scaffold-build` was read as the direct predecessor and source of theme/runtime guardrails for this story.
- Existing `single.php`, `functions.php`, `main.scss`, and theme tests were read completely to document update-file behavior before writing this story.
- Runtime verification was executed against the local WordPress stack with a manually created post (`?p=12`) carrying `_pipeline_job_id`, `_pipeline_source_url`, and `_pipeline_model`.
- Theme-local verification was extended with `tests/single-template-runtime.php`, then executed inside the `wordpress` container because no local PHP binary is present on the Windows host PATH.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story keeps verification independent from the pipeline by requiring a manually created post with pipeline meta fields.
- Story sharpens the S3.2 visual contract into objective runtime checks the dev agent can prove without inventing a separate UX artifact.
- `single.php` now renders an editorial hero, visible AI badge, optional excerpt lead, model pill, constrained reading column, and graceful source attribution sourced from `_pipeline_source_url` with a fallback to `source_url`.
- `main.scss` now defines the visual system and responsive rules for the centerpiece template, including a readable measure on desktop and tighter spacing at mobile widths.
- Objective checks now cover source/template hooks in `theme-scaffold.test.mjs`, a theme-local runtime verifier, `npm run build`, and live render inspection through the local WordPress stack.

### File List

- _bmad-output/implementation-artifacts/3-2-single-php-centerpiece.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- wp-theme/ia-news-theme/assets/dist/.vite/manifest.json
- wp-theme/ia-news-theme/assets/dist/assets/theme-BaH3NLR7.css
- wp-theme/ia-news-theme/assets/dist/assets/theme-BPoSbEhO.js
- wp-theme/ia-news-theme/single.php
- wp-theme/ia-news-theme/src/styles/main.scss
- wp-theme/ia-news-theme/tests/single-template-runtime.php
- wp-theme/ia-news-theme/tests/theme-scaffold.test.mjs

## Change Log

- 2026-07-07: Story created and contexted for development. Status -> ready-for-dev.
- 2026-07-07: Implemented the S3.2 single-post centerpiece, regenerated theme assets, extended objective verification, and completed the story.
