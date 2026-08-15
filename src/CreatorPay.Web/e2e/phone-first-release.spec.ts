import { expect, test } from "./fixtures";

test.describe.configure({ mode: "serial", timeout: 120_000 });

const api = process.env.E2E_API_URL!,
  seedPassword = process.env.E2E_SHOPPER_PASSWORD!,
  password = "Phone-first@123";
const mobileProject = (name: string) => name === "mobile",
  suffix = (name: string) => (mobileProject(name) ? "2" : name === "iphone" ? "3" : "1");
const phones = (name: string) => ({
  shopper: `091288900${suffix(name)}`,
  creator: `091288901${suffix(name)}`,
  business: `091288912${suffix(name)}`,
  cashier: `091288913${suffix(name)}`,
});

async function noOverflow(page: any) {
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    ),
  ).toBeLessThanOrEqual(1);
}
async function reset(page: any) {
  if (page.url() === "about:blank") await page.goto("/");
  await page.evaluate(() => localStorage.clear());
  await page.goto("/");
}
async function openSignIn(page: any) {
  const welcome = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await welcome.isVisible()) await welcome.click();
}
async function signIn(page: any, phone: string, secret = password) {
  await reset(page);
  await openSignIn(page);
  const form = page.locator("form");
  await expect(form.getByLabel("Phone Number")).toBeVisible();
  await expect(form.getByLabel("Email")).toHaveCount(0);
  await form.getByLabel("Phone Number").fill(phone);
  await form.getByLabel("Password").fill(secret);
  await form.getByRole("button", { name: "Sign In", exact: true }).click();
  const setup = page.getByRole("heading", { name: "Complete your secure setup" });
  try {
    await setup.waitFor({ state: "visible", timeout: 2500 });
    await page.getByLabel("Day").fill("2");
    await page.getByLabel("Month").fill("1");
    await page.getByLabel("Year").fill("1990");
    await page.getByLabel("Create 5-digit PIN").fill("12345");
    await page.getByLabel("Confirm PIN").fill("12345");
    await page.getByRole("button", { name: "Create PIN" }).click();
  } catch {}
}
async function verify(page: any) {
  await expect(
    page.getByRole("heading", { name: "Verify your phone" }),
  ).toHaveCount(0);
  await expect(page.getByRole("status")).toContainText(
    /sign in now|pending Platform review/i,
  );
}
async function admin(request: any) {
  const login = await request.post(`${api}/api/v1/auth/login`, {
    data: {
      email: "admin@e2e.invalid",
      password: seedPassword,
    },
  });
  expect(login.ok()).toBeTruthy();
  const body = await login.json();
  expect(body.user.role).toBe("PlatformAdmin");
  return { Authorization: `Bearer ${body.accessToken}` };
}
async function approveCreator(request: any, name: string) {
  const headers = await admin(request),
    pending = await request.get(`${api}/api/v1/creators/pending`, { headers });
  expect(pending.ok()).toBeTruthy();
  const row = (await pending.json()).find((x: any) => x.displayName === name);
  expect(row).toBeTruthy();
  expect(
    (
      await request.post(`${api}/api/v1/creators/approve`, {
        headers,
        data: { creatorId: row.creatorId },
      })
    ).ok(),
  ).toBeTruthy();
}
async function approveBusiness(request: any, name: string) {
  const headers = await admin(request),
    pending = await request.get(`${api}/api/v1/admin/merchants/pending`, {
      headers,
    });
  expect(pending.ok()).toBeTruthy();
  const row = (await pending.json()).find((x: any) => x.tradingName === name);
  expect(row).toBeTruthy();
  expect(
    (
      await request.post(`${api}/api/v1/admin/merchants/approve`, {
        headers,
        data: { merchantId: row.merchantId },
      })
    ).ok(),
  ).toBeTruthy();
  const settings = await request.get(`${api}/api/v1/admin/financial-settings`, {
    headers,
  });
  expect(settings.ok()).toBeTruthy();
  const value = await settings.json();
  const result = await request.get(
    `${api}/api/v1/admin/merchants?q=${encodeURIComponent(name)}&page=1&pageSize=25`,
    { headers },
  );
  expect(result.ok()).toBeTruthy();
  const item = (await result.json()).items.find(
    (x: any) => x.tradingName === name,
  );
  expect(item.status).toBe("Active");
  expect(item.walletBalance).toBe(0);
  expect(item.requiredMinimum).toBe(value.minimumBusinessWalletBalance);
  expect(item.fundingStatus).toBe(
    value.minimumBusinessWalletBalance > 0 ? "Insufficient Funds" : "Active",
  );
}

test("Shopper phone registration, activation, login, and support-assisted password reset", async ({
  page,
}, info) => {
  const phone = phones(info.project.name).shopper;
  await reset(page);
  await page.getByRole("button", { name: "Shopper Registration" }).click();
  await page
    .getByLabel("Display Name")
    .fill(`Browser Shopper ${suffix(info.project.name)}`);
  await page.getByLabel("Phone").fill(phone);
  await expect(page.getByLabel("Email")).not.toHaveAttribute("required", "");
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm Password").fill(password);
  await page.getByRole("button", { name: "Create shopper account" }).click();
  await verify(page);
  await signIn(page, phone);
  await expect(
    page.getByRole("heading", { name: "Shopper" }),
  ).toBeVisible();
  await reset(page);
  await openSignIn(page);
  await page.getByRole("button", { name: "Forgot Password" }).click();
  await page.getByLabel("Phone Number").fill(phone);
  await page.getByLabel("Day").fill("2");
  await page.getByLabel("Month").fill("1");
  await page.getByLabel("Year").fill("1990");
  await page.getByRole("button", { name: "Request Password Reset" }).click();
  await expect(page.getByRole("status")).toContainText(
    "waiting for support approval",
  );
  await expect(page.getByText("Send verification code")).toHaveCount(0);
  await noOverflow(page);
});

test("Creator phone registration, Platform approval, and phone login", async ({
  page,
  request,
}, info) => {
  const phone = phones(info.project.name).creator,
    name = `Browser Creator ${suffix(info.project.name)}`;
  await reset(page);
  await page
    .getByRole("button", { name: "Content Creator Registration" })
    .click();
  await page.getByLabel("Legal First Name").fill("Browser");
  await page.getByLabel("Father's Name").fill("Creator");
  await page.getByLabel("Public Display Name").fill(name);
  await page.getByLabel("Phone").fill(phone);
  await expect(page.getByLabel("Email")).not.toHaveAttribute("required", "");
  await page
    .getByLabel("Social Profile URL")
    .fill(`https://example.test/creator-${suffix(info.project.name)}`);
  await page.getByLabel("Estimated Follower Count").fill("100");
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm Password").fill(password);
  await page.getByLabel(/accept the Terms of Service/i).check();
  await page
    .getByRole("button", { name: "Create content creator account" })
    .click();
  await verify(page);
  await expect(page.getByRole("status")).toContainText(
    "pending Platform review",
  );
  await approveCreator(request, name);
  await signIn(page, phone);
  await expect(page.getByRole("heading", { name: "Creator" })).toBeVisible();
  await noOverflow(page);
});

test("Business phone registration, approval, funding state, and phone login", async ({
  page,
  request,
}, info) => {
  const phone = phones(info.project.name).business,
    name = `Browser Business ${suffix(info.project.name)}`;
  await reset(page);
  await page
    .getByRole("button", { name: "Business Owner Registration" })
    .click();
  await page.getByLabel("Trading Name").fill(name);
  await page.getByLabel("Business Type").selectOption({ index: 1 });
  await page.getByLabel("Primary Contact Name").fill("Browser Owner");
  await page.getByLabel("Phone").fill(phone);
  await expect(page.getByLabel("Email")).not.toHaveAttribute("required", "");
  await page.getByLabel("Business Address").fill("Addis Ababa");
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm Password").fill(password);
  await page.getByLabel(/accept the Terms of Service/i).check();
  await page
    .getByRole("button", { name: "Create business owner account" })
    .click();
  await verify(page);
  await expect(page.getByRole("status")).toContainText(
    "pending Platform review",
  );
  await approveBusiness(request, name);
  await signIn(page, phone);
  await expect(
    page.getByRole("heading", { name: "Business" }),
  ).toBeVisible();
  await noOverflow(page);
});

test("Business creates phone-only Cashier and routes to Cashier dashboard", async ({
  page,
  request,
}, info) => {
  const business = phones(info.project.name).business,
    cashier = phones(info.project.name).cashier;
  await signIn(page, business);
  await page.getByRole("button", { name: "Settings" }).click();
  await page.getByRole("menuitem", { name: "Cashier Management" }).click();
  await page.getByRole("button", { name: "Create Cashier" }).click();
  await page
    .getByLabel("Cashier Name")
    .fill(`Browser Cashier ${suffix(info.project.name)}`);
  await page.getByLabel("Phone Number").fill(cashier);
  await expect(page.getByLabel(/Username|Email/)).toHaveCount(0);
  await page.getByLabel("Temporary Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm Temporary Password").fill(password);
  await expect(page.getByText("Birth Date")).toHaveCount(0);
  await page
    .getByRole("button", { name: "Create Cashier", exact: true })
    .click();
  await expect(page.getByText("Cashier account created.")).toBeVisible();
  await signIn(page, cashier);
  await expect(
    page.getByRole("heading", { name: "Cashier Dashboard" }),
  ).toBeVisible();
  await noOverflow(page);
});

test("phone-first copy and PlatformAdmin compatibility remain secure", async ({
  page,
  request,
}, info) => {
  await reset(page);
  await openSignIn(page);
  const form = page.locator("form");
  await expect(form.getByLabel("Phone Number")).toBeVisible();
  await expect(form.getByLabel(/Email|Username/)).toHaveCount(0);
  await form.getByLabel("Phone Number").fill("0912999999");
  await form.getByLabel("Password").fill("Incorrect-password@1");
  await form.getByRole("button", { name: "Sign In", exact: true }).click();
  await expect(page.getByRole("status")).toHaveText(
    "Invalid phone number or password.",
  );

  const adminFailure = await request.post(`${api}/api/v1/auth/login`, {
    data: { email: "admin@e2e.invalid", password: "Incorrect-password@1" },
  });
  expect(adminFailure.status()).toBe(400);
  expect((await adminFailure.json()).detail).toBe("Invalid email or password.");

  const response = await request.post(`${api}/api/v1/customers/register`, {
    data: {
      displayName: `Response Secrecy ${suffix(info.project.name)}`,
      phoneNumber: `091288920${suffix(info.project.name)}`,
      password,
      confirmation: password,
    },
  });
  expect(response.status()).toBe(201);
  const serialized = JSON.stringify(await response.json());
  expect(serialized).not.toMatch(/"(?:otp|code|testCode)"/i);
});
