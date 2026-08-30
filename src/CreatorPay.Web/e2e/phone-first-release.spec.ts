import { expect, test } from "./fixtures";

test.describe.configure({ mode: "serial", timeout: 120_000 });

const api = process.env.E2E_API_URL!,
  otp = process.env.E2E_TEST_OTP!,
  registrationBypass = process.env.E2E_PILOT_REGISTRATION_BYPASS === "true",
  password = "Phone-first@123",
  nextPassword = "Phone-first@456";
const mobileProject = (name: string) => name === "mobile",
  suffix = (name: string) => (mobileProject(name) ? "2" : "1");
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
  await page.evaluate(() => sessionStorage.clear());
  await page.context().clearCookies();
  await page.goto("/");
}
async function signIn(page: any, phone: string, secret = password) {
  await reset(page);
  const transition = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await transition.isVisible()) await transition.click();
  const form = page.locator("form");
  await expect(form.getByLabel("Phone Number")).toBeVisible();
  await expect(form.getByLabel("Email")).toHaveCount(0);
  await form.getByLabel("Phone Number").fill(phone);
  await form.getByRole('textbox',{name:'Password'}).fill(secret);
  const loginResponse=page.waitForResponse((r:any)=>r.request().method()==='POST'&&new URL(r.url()).pathname==='/api/v1/auth/login',{timeout:30000});
  await form.getByRole("button", { name: "Sign In", exact: true }).dispatchEvent('click');
  await loginResponse;
  const setup=page.getByRole('heading',{name:/^(Complete your secure setup|Create 5-digit PIN)$/}),workspace=page.locator('main.app');
  await expect(setup.or(workspace)).toBeVisible({timeout:30000});
  if(await setup.isVisible()){
    const pinInputs=page.locator('label.pin-entry input');
    await expect(pinInputs).toHaveCount(2,{timeout:30000});
    await pinInputs.nth(0).fill('12345');
    await pinInputs.nth(1).fill('12345');
    await page.getByRole('button',{name:'Create PIN'}).click();
  }
  await expect(workspace).toBeVisible({timeout:30000});
}
async function attemptSignIn(page: any, phone: string, secret: string) {
  await reset(page);
  const transition = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await transition.isVisible()) await transition.click();
  const form = page.locator("form");
  await expect(form.getByLabel("Phone Number")).toBeVisible();
  await form.getByLabel("Phone Number").fill(phone);
  await form.getByRole("textbox", { name: "Password" }).fill(secret);
  await form.getByRole("button", { name: "Sign In", exact: true }).click();
}
async function verify(page: any) {
  await expect(page.getByRole("status")).toContainText(
    /sign in now|pending Platform review/i,
  );
}
async function admin(request: any) {
  const login = await request.post(`${api}/api/v1/auth/login`, {
    data: {
      email: "admin@e2e.invalid",
      password: process.env.E2E_ADMIN_PASSWORD,
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
    pendingUrl = `${api}/api/v1/admin/merchants/pending`;
  let row: any;
  for (let attempt = 0; attempt < 20 && !row; attempt++) {
    const pending = await request.get(pendingUrl, { headers });
    expect(pending.ok(), await pending.text()).toBeTruthy();
    row = (await pending.json()).find((x: any) => x.tradingName === name);
    if (!row) await new Promise((resolve) => setTimeout(resolve, 250));
  }
  expect(row).toBeTruthy();
  const approval = await request.post(`${api}/api/v1/admin/merchants/approve`, {
    headers,
    data: { merchantId: row.merchantId },
  });
  const approvalBody = await approval.text();
  if (!approval.ok())
    throw new Error(`Merchant approval ${approval.status()}: ${approvalBody}`);
  const settings = await request.get(`${api}/api/v1/admin/financial-settings`, {
    headers,
  });
  expect(settings.ok()).toBeTruthy();
  const value = await settings.json();
  const settingsUpdate = await request.put(`${api}/api/v1/admin/financial-settings`, {
    headers,
    data: {
      ...value,
      minimumBusinessWalletBalance: 1000,
      payoutScheduleEffectiveFromUtc: new Date(Date.now() + 60000).toISOString(),
    },
  });
  if (!settingsUpdate.ok())
    throw new Error(`Financial settings update ${settingsUpdate.status()}: ${await settingsUpdate.text()}`);
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
  expect(item.requiredMinimum).toBe(1000);
  expect(item.fundingStatus).toBe("Insufficient Funds");
}

test("Shopper phone registration, activation, login, and supported password reset", async ({
  page,
  request,
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
  const signInTransition = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await signInTransition.isVisible()) await signInTransition.click();
  await expect(page.getByRole("button", { name: "Forgot Password" })).toBeVisible();
  await page.getByRole("button", { name: "Forgot Password" }).click();
  await page.getByLabel("Phone Number").fill(phone);
  await page.getByRole("button", { name: "Request Password Reset" }).click();
  await expect(page.getByRole("status")).toContainText("Status: Pending");
  await expect(page.getByRole("status")).toContainText("waiting for admin approval");
  const adminHeaders = await admin(request);
  const queue = await request.get(`${api}/api/v1/admin/password-reset-requests`, { headers: adminHeaders });
  expect(queue.ok()).toBeTruthy();
  const normalizedPhone = phone.replace(/\D/g, "").slice(-9);
  const resetRequest = (await queue.json()).find((x: any) =>
    String(x.phone ?? x.phoneNumber ?? "").replace(/\D/g, "").slice(-9) === normalizedPhone,
  );
  expect(resetRequest).toBeTruthy();
  const approval = await request.post(`${api}/api/v1/admin/password-reset-requests/${resetRequest.id}/approve`, { headers: adminHeaders, data: {} });
  expect(approval.ok()).toBeTruthy();
  await Promise.all([
    page.waitForResponse((response) =>
      response.request().method() === "GET" &&
      response.url().includes("/api/v1/auth/password-reset-requests/") &&
      response.ok(),
    ),
    page.getByRole("button", { name: "Check Approval Status" }).click(),
  ]);
  await expect(page.getByRole("status")).toContainText("Status: Approved");
  await expect(page.getByRole("status")).toContainText("Your request was approved. Create a new password.");
  await page.getByLabel("New Password").fill(nextPassword);
  await page.getByLabel("Confirm Password").fill(nextPassword);
  await page.getByRole("button", { name: "Reset Password" }).click();
  await expect(page.getByRole("status")).toContainText("Password reset.");
  await attemptSignIn(page, phone, password);
  await expect(page.getByRole("status")).toContainText(/invalid/i);
  await signIn(page, phone, nextPassword);
  await expect(
    page.getByRole("heading", { name: "Shopper" }),
  ).toBeVisible();
  await reset(page);
  const signInTransitionAgain = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await signInTransitionAgain.isVisible()) await signInTransitionAgain.click();
  await expect(page.getByRole("button", { name: "Forgot Password" })).toBeVisible();
  await page.getByRole("button", { name: "Forgot Password" }).click();
  await page.getByLabel("Phone Number").fill(phone);
  await page.getByRole("button", { name: "Request Password Reset" }).click();
  await expect(page.getByRole("status")).toContainText("waiting for admin approval");
  const rejectedQueue = await request.get(`${api}/api/v1/admin/password-reset-requests`, { headers: adminHeaders });
  expect(rejectedQueue.ok()).toBeTruthy();
  const rejectedRequest = (await rejectedQueue.json()).find((x: any) =>
    String(x.phone ?? x.phoneNumber ?? "").replace(/\D/g, "").slice(-9) === phone.replace(/\D/g, "").slice(-9) &&
    x.status === "Pending",
  );
  expect(rejectedRequest).toBeTruthy();
  const rejection = await request.post(`${api}/api/v1/admin/password-reset-requests/${rejectedRequest.id}/reject`, { headers: adminHeaders, data: {} });
  expect(rejection.ok()).toBeTruthy();
  await Promise.all([
    page.waitForResponse((response) =>
      response.request().method() === "GET" &&
      response.url().includes("/api/v1/auth/password-reset-requests/") &&
      response.ok(),
    ),
    page.getByRole("button", { name: "Check Approval Status" }).click(),
  ]);
  await expect(page.getByRole("status")).toContainText("Status: Rejected");
  await expect(page.getByRole("status")).toContainText("Your password reset request was rejected.");
  await expect(page.getByLabel("New Password")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Request New Password Reset" })).toBeVisible();
  await noOverflow(page);
});

test("Creator phone registration, OTP, Platform approval, and phone login", async ({
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
  await page.getByLabel(/accept the Terms of Service and Privacy Notice/i).check();
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

test("Business phone registration, OTP, approval, funding state, and phone login", async ({
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
  await page.getByLabel(/accept the Terms of Service and Privacy Notice/i).check();
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

test("Business creates phone-only Cashier; OTP activation routes Cashier dashboard", async ({
  page,
  request,
}, info) => {
  const business = phones(info.project.name).business,
    cashier = phones(info.project.name).cashier;
  await signIn(page, business);
  await page.getByRole("button", { name: "Settings", exact: true }).click();
  await page
    .getByRole("menu")
    .getByRole("menuitem", { name: "Cashier Management", exact: true })
    .click();
  await page.getByRole("button", { name: "Create Cashier" }).click();
  await page
    .getByLabel("Cashier Name")
    .fill(`Browser Cashier ${suffix(info.project.name)}`);
  await page.getByLabel("Phone Number").fill(cashier);
  await expect(page.getByLabel(/Username|Email/)).toHaveCount(0);
  await page.getByLabel("Temporary Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm Temporary Password").fill(password);
  await page
    .getByRole("button", { name: "Create Cashier", exact: true })
    .click();
  await expect(page.getByText("Cashier account created.")).toBeVisible();
  if (!registrationBypass) {
    const activated = await request.post(`${api}/api/v1/auth/verify-phone`, {
      data: { phoneNumber: cashier, code: otp },
    });
    expect(activated.ok(), await activated.text()).toBeTruthy();
  }
  await signIn(page, cashier);
  await expect(
    page.getByRole("heading", { name: "Cashier", exact: true }),
  ).toBeVisible();
  await noOverflow(page);
});

test("phone-first copy, PlatformAdmin compatibility, and OTP response secrecy", async ({
  page,
  request,
}, info) => {
  await reset(page);
  const signInTransition = page.getByRole("button", { name: "Already have an account? Sign In" });
  if (await signInTransition.isVisible()) await signInTransition.click();
  const form = page.locator("form");
  await expect(form.getByLabel("Phone Number")).toBeVisible();
  await expect(form.getByLabel(/Email|Username/)).toHaveCount(0);
  await form.getByLabel("Phone Number").fill("0912999999");
  await form.getByRole('textbox',{name:'Password'}).fill("Incorrect-password@1");
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
  expect(serialized).not.toContain(otp);
  expect(serialized).not.toMatch(/"(?:otp|code|testCode)"/i);
});
