import { expect, test, type BrowserContext, type Page } from "@playwright/test";

async function installExternalAdmin(context: BrowserContext) {
  const requests: { path: string; resourceType: string; navigation: boolean }[] = [];
  await context.route("**/api/v1/**", async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.pathname === "/api/v1/integration/v3/session") {
      await route.fulfill({ json: {
        role: "PlatformAdmin", status: "Active", isOnboarding: false, destination: "/admin",
      }});
      return;
    }
    if (url.pathname === "/api/v1/integration/v3/switch-profile"
        || url.pathname === "/api/v1/integration/v3/logout") {
      requests.push({ path: url.pathname, resourceType: request.resourceType(),
        navigation: request.isNavigationRequest() });
      expect(request.method()).toBe("POST");
      await expect(request.headerValue("x-weymela-product-request")).resolves.toBe("1");
      await route.fulfill({ json: { redirectUrl: url.pathname.endsWith("logout")
        ? "/integration/sign-out" : "/onboarding" } });
      return;
    }
    if (url.pathname === "/api/v1/notifications") {
      await route.fulfill({ json: { items: [] } });
      return;
    }
    if (url.pathname === "/api/v1/notifications/unread-count") {
      await route.fulfill({ json: { count: 0 } });
      return;
    }
    await route.fulfill({ json: {} });
  });
  await context.route("**/api/session/sign-out", async route => {
    const request = route.request();
    requests.push({ path: new URL(request.url()).pathname, resourceType: request.resourceType(),
      navigation: request.isNavigationRequest() });
    expect(request.method()).toBe("POST");
    await expect(request.headerValue("x-weymela-request")).resolves.toBe("1");
    await route.fulfill({ status: 204 });
  });
  return requests;
}

async function openAdminFromCleanHistory(page: Page) {
  await page.goto("/help");
  await expect(page.getByRole("heading", { name: /Help/i })).toBeVisible();
  await page.goto("/admin");
  await expect(page.getByRole("button", { name: "Switch profile" })).toBeVisible();
}

test("Switch Profile replaces Admin with the clean V3 profile route", async ({ context, page }) => {
  const requests = await installExternalAdmin(context);
  await context.route("**/onboarding", route => route.fulfill({ contentType: "text/html", body:
    "<!doctype html><html><body><main><h1>Choose a profile</h1></main></body></html>" }));
  const navigations: string[] = [];
  page.on("framenavigated", frame => {
    if (frame === page.mainFrame()) navigations.push(new URL(frame.url()).pathname);
  });

  await openAdminFromCleanHistory(page);
  await page.getByRole("button", { name: "Switch profile" }).click();
  await expect(page).toHaveURL(/\/onboarding$/);
  await expect(page.getByRole("heading", { name: "Choose a profile" })).toBeVisible();
  expect(requests).toEqual([{
    path: "/api/v1/integration/v3/switch-profile", resourceType: "fetch", navigation: false,
  }]);
  expect(navigations).not.toContain("/api/v1/integration/v3/switch-profile");

  await page.goBack();
  await expect(page).toHaveURL(/\/help$/);
});

test("Logout clears both product and authority sessions before replacing with sign-in", async ({ context, page }) => {
  const requests = await installExternalAdmin(context);
  await context.route("**/sign-in", route => route.fulfill({ contentType: "text/html", body:
    "<!doctype html><html><body><main><h1>Sign in</h1></main></body></html>" }));
  const navigations: string[] = [];
  page.on("framenavigated", frame => {
    if (frame === page.mainFrame()) navigations.push(new URL(frame.url()).pathname);
  });

  await openAdminFromCleanHistory(page);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/sign-in$/);
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  expect(requests).toEqual([
    { path: "/api/v1/integration/v3/logout", resourceType: "fetch", navigation: false },
    { path: "/api/session/sign-out", resourceType: "fetch", navigation: false },
  ]);
  expect(navigations).not.toContain("/integration/sign-out");

  await page.goBack();
  await expect(page).toHaveURL(/\/help$/);
});
