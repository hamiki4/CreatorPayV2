import { expect, test } from "@playwright/test";

test("PWA metadata is valid for iPhone Home Screen installation", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator('meta[name="viewport"]')).toHaveAttribute("content", /viewport-fit=cover/);
  await expect(page.locator('meta[name="apple-mobile-web-app-capable"]')).toHaveAttribute("content", "yes");
  await expect(page.locator('meta[name="apple-mobile-web-app-title"]')).toHaveAttribute("content", "Weymela");
  await expect(page.locator('link[rel="apple-touch-icon"]')).toHaveAttribute("href", "/icons/weymela-180x180.png");
  const manifest = await page.request.get("/manifest.webmanifest");
  expect(manifest.ok()).toBeTruthy();
  expect(await manifest.json()).toMatchObject({ name: "Weymela", short_name: "Weymela", display: "standalone", start_url: "/" });
});

test("install hint is limited to iPhone Safari and remembers dismissal", async ({ page }, testInfo) => {
  await page.goto("/");
  const hint = page.getByLabel("Install Weymela on iPhone");
  if (testInfo.project.name === "iphone") {
    await expect(hint).toBeVisible();
    await expect(hint).toContainText("Safari → Share → Add to Home Screen");
    await hint.getByRole("button", { name: "Dismiss iPhone installation instructions" }).click();
    await expect(hint).toHaveCount(0);
    await page.reload();
    await expect(hint).toHaveCount(0);
  } else {
    await expect(hint).toHaveCount(0);
  }
});

test("public authentication layout has no horizontal overflow", async ({ page }) => {
  await page.goto("/");
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
});
