import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const themeRoot = path.resolve(import.meta.dirname, "..");

function readThemeFile(...parts) {
  return fs.readFileSync(path.join(themeRoot, ...parts), "utf8");
}

test("theme keeps the required WordPress metadata header", () => {
  const styleCss = readThemeFile("style.css");

  assert.match(styleCss, /Theme Name:\s+IA News Theme/);
  assert.match(styleCss, /Text Domain:\s+ia-news-theme/);
});

test("theme enqueues compiled assets through a manifest-aware function", () => {
  const functionsPhp = readThemeFile("functions.php");

  assert.match(functionsPhp, /wp_enqueue_scripts/);
  assert.match(functionsPhp, /manifest\.json/);
  assert.match(functionsPhp, /wp_enqueue_style/);
  assert.match(functionsPhp, /wp_enqueue_script/);
});

test("theme exposes a minimal intentional shell instead of the placeholder copy", () => {
  const indexPhp = readThemeFile("index.php");

  assert.doesNotMatch(indexPhp, /placeholder/i);
  assert.match(indexPhp, /site-shell/);
});

test("build output includes a manifest and compiled theme assets", () => {
  const manifestPath = path.join(themeRoot, "assets", "dist", ".vite", "manifest.json");
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  const entry = manifest["src/scripts/main.js"];

  assert.ok(entry, "expected manifest entry for src/scripts/main.js");
  assert.match(entry.file, /^assets\/theme-.*\.js$/);
  assert.ok(Array.isArray(entry.css) && entry.css.length > 0, "expected at least one compiled css asset");
});
