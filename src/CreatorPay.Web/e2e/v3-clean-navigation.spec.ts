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

type LifecycleRole = "Creator" | "MerchantAdmin";

async function installLifecycleSession(context: BrowserContext, role: LifecycleRole,
    options: { isOnboarding: boolean; status?: string; lifecycleStatus?: string } ) {
  const destination = role === "Creator" ? "/creator" : "/business";
  const detailed = role === "Creator" ? "/onboarding/creator" : "/onboarding/business";
  const mutations: string[] = [];
  await context.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() !== "GET") mutations.push(path);
    if (path === "/api/v1/integration/v3/session") {
      await route.fulfill({ json: { role, status: options.status ?? (options.isOnboarding ? "Onboarding" : "PendingApproval"),
        isOnboarding: options.isOnboarding, destination: options.isOnboarding ? detailed : destination } });
      return;
    }
    if (path === "/api/v1/creators/me") {
      await route.fulfill({ json: { displayName: "Lifecycle Creator", publicCreatorId: "CR-LIFECYCLE",
        creatorCode: "123456", phoneNumber: "+251900000000", email: "", city: "Addis Ababa",
        accountStatus: options.status ?? "PendingApproval", effectiveStatus: options.status === "Active" ? "Active" : "Inactive",
        creatorStatus: options.lifecycleStatus ?? "PendingApproval", nextStep: "Creator lifecycle next step." } });
      return;
    }
    if (path === "/api/v1/merchants/me") {
      await route.fulfill({ json: { tradingName: "Lifecycle Business",
        accountStatus: options.status ?? "PendingApproval", effectiveStatus: options.status === "Active" ? "Active" : "Inactive",
        merchantStatus: options.lifecycleStatus ?? "PendingReview",
        nextStep: options.lifecycleStatus === "CorrectionRequested"
          ? "Review the requested corrections and update your profile." : "Business lifecycle next step." } });
      return;
    }
    if (path === "/api/v1/notifications") return route.fulfill({ json: { items: [], total: 0 } });
    if (path === "/api/v1/notifications/unread-count") return route.fulfill({ json: { count: 0 } });
    if (path.includes("earnings/summary")) return route.fulfill({ json: { confirmedSalesCount: 0, upcomingPayoutAmount: 0 } });
    if (path.includes("/wallet")) return route.fulfill({ json: { availableBalance: 0, currencyCode: "ETB", status: "Active", advertisingEligible: true } });
    if (path.includes("dashboard-metrics")) return route.fulfill({ json: { confirmedSales: 0, period: "Pilot" } });
    await route.fulfill({ json: [] });
  });
  return mutations;
}

for (const role of ["Creator", "MerchantAdmin"] as const) {
  const label = role === "Creator" ? "Creator" : "Business";
  const detailed = role === "Creator" ? "/onboarding/creator" : "/onboarding/business";
  const root = role === "Creator" ? "/creator" : "/business";

  test(`${label} registration Back returns to profiles without mutation`, async ({ context, page }) => {
    const mutations = await installLifecycleSession(context, role, { isOnboarding: true });
    await context.route("**/onboarding", route => route.fulfill({ contentType: "text/html", body:
      "<!doctype html><html><body><main><h1>Choose a profile</h1></main></body></html>" }));

    await page.goto(detailed);
    await page.getByRole("button", { name: "Back to profiles" }).click();
    await expect(page).toHaveURL(/\/onboarding$/);
    await expect(page.getByRole("heading", { name: "Choose a profile" })).toBeVisible();
    expect(mutations).toEqual([]);
  });

  test(`direct ${root} with ${label} missing recovers without raw 403`, async ({ context, page }) => {
    await installLifecycleSession(context, role, { isOnboarding: true });
    await context.route("**/onboarding", route => route.fulfill({ contentType: "text/html", body:
      "<!doctype html><html><body><main><h1>Choose a profile</h1></main></body></html>" }));

    await page.goto(root);
    await expect(page).toHaveURL(/\/onboarding$/);
    await expect(page.getByRole("heading", { name: "Choose a profile" })).toBeVisible();
    await expect(page.getByText("Request failed (403)")).toHaveCount(0);
  });

  test(`${label} Pending shows a clean review state`, async ({ context, page }) => {
    await installLifecycleSession(context, role, { isOnboarding: false });
    await page.goto(root);
    await expect(page.getByText(/under review/i)).toBeVisible();
    await expect(page.getByText("Request failed (403)")).toHaveCount(0);
  });

  test(`${label} correction request shows an actionable lifecycle state`, async ({ context, page }) => {
    await installLifecycleSession(context, role, { isOnboarding: false, lifecycleStatus: "CorrectionRequested" });
    await page.goto(root);
    await expect(page.getByText(/requested corrections/i)).toBeVisible();
    await expect(page.getByText("Request failed (403)")).toHaveCount(0);
  });

  test(`${label} Active opens its workspace`, async ({ context, page }) => {
    await installLifecycleSession(context, role, { isOnboarding: false, status: "Active", lifecycleStatus: "Active" });
    await page.goto(root);
    await expect(page.getByRole("heading", { name: label, exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();
  });
}
