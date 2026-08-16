import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const read = path => readFile(new URL(`../${path}`, import.meta.url), "utf8");

test("manifest defines a scoped standalone Weymela application", async () => {
  const manifest = JSON.parse(await read("public/manifest.webmanifest"));
  assert.equal(manifest.name, "Weymela");
  assert.equal(manifest.short_name, "Weymela");
  assert.equal(manifest.display, "standalone");
  assert.equal(manifest.start_url, "/");
  assert.equal(manifest.scope, "/");
  assert.equal(manifest.theme_color, "#153c27");
  assert.ok(manifest.icons.some(icon => icon.sizes === "512x512"));
});

test("document advertises iPhone Home Screen metadata and safe viewport", async () => {
  const html = await read("index.html");
  assert.match(html, /viewport-fit=cover/);
  assert.match(html, /apple-mobile-web-app-capable" content="yes"/);
  assert.match(html, /apple-mobile-web-app-title" content="Weymela"/);
  assert.match(html, /apple-touch-icon" sizes="180x180"/);
});

test("iPhone install hint is Safari-only, standalone-aware and dismissible", async () => {
  const source = await read("src/IosInstallHint.tsx");
  assert.match(source, /iPhone\|iPad\|iPod/);
  assert.match(source, /display-mode: standalone/);
  assert.match(source, /Add to Home Screen/);
  assert.match(source, /weymela_ios_install_hint_dismissed/);
});

test("service worker does not cache API or authenticated requests", async () => {
  const worker = await read("public/sw.js");
  assert.match(worker, /url\.pathname\.startsWith\('\/api\/'\)/);
  assert.match(worker, /event\.request\.headers\.has\('Authorization'\)/);
  assert.match(worker, /event\.request\.method!==\'GET\'/);
});

test("safe-area rules cover overlays and bottom actions", async () => {
  const css = await read("src/styles.css");
  const safeAreaCss = await read("src/pwa-safe-area.css");
  const pwaCss = `${css}\n${safeAreaCss}`;
  assert.match(pwaCss, /safe-area-inset-top/);
  assert.match(pwaCss, /safe-area-inset-bottom/);
  assert.match(pwaCss, /\.notification-drawer/);
  assert.match(pwaCss, /\.settings-menu/);
  assert.match(pwaCss, /\.ios-install-hint/);
});
