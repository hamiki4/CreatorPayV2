import { chromium } from "playwright";
import { expect } from "@playwright/test";

const webUrl = process.env.PILOT_WEB_URL ?? "https://pilot.weymela.com";
const phone = process.env.PILOT_PHONE ?? "+251911046240";
const newPassword = process.env.PILOT_NEW_PASSWORD;
const holdMs = Number(process.env.PILOT_HOLD_MS ?? "45000");

async function hold(label) {
  console.log(`WAITING_${label}=1`);
  await new Promise((resolve) => setTimeout(resolve, holdMs));
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1200 } });
let lastPost = null;
page.on("response", async (response) => {
  const url = response.url();
  if (url.includes("/api/v1/auth/password-reset-requests") && response.request().method() === "POST") {
    lastPost = response;
  }
});

await page.goto(webUrl, { waitUntil: "networkidle" });
if (await page.getByRole("button", { name: "Already have an account? Sign In" }).isVisible().catch(() => false)) {
  await page.getByRole("button", { name: "Already have an account? Sign In" }).click();
}
await expect(page.getByRole("button", { name: "Forgot Password" })).toBeVisible();
await page.getByRole("button", { name: "Forgot Password" }).click();
await expect(page.getByRole("heading", { name: "Forgot Password" })).toBeVisible();

await page.getByLabel("Phone Number").fill(phone);
await page.getByRole("button", { name: "Request Password Reset" }).click();
await expect(page.getByRole("status")).toContainText("Status: Pending");
await expect(page.getByRole("status")).toContainText("waiting for support approval");
const firstPost = lastPost ?? await page.waitForResponse((r) => r.url().includes("/api/v1/auth/password-reset-requests") && r.request().method() === "POST");
const firstBody = await firstPost.json();
console.log(`REFERENCE_1=${firstBody.reference}`);
console.log(`STATUS_1=${firstBody.status}`);

await page.getByRole("button", { name: "Check Approval Status" }).click();
await expect(page.getByRole("status")).toContainText("Status: Pending");
console.log("PENDING_UI_OK=1");

await hold("BEFORE_REJECT");
await page.getByRole("button", { name: "Check Approval Status" }).click();
await expect(page.getByRole("status")).toContainText("Status: Rejected");
await expect(page.getByLabel("New Password")).toHaveCount(0);
await expect(page.getByRole("button", { name: "Request New Password Reset" })).toBeVisible();
console.log("REJECTED_UI_OK=1");

await page.getByRole("button", { name: "Request New Password Reset" }).click();
await expect(page.getByLabel("Phone Number")).toBeVisible();
await page.getByLabel("Phone Number").fill(phone);
await page.getByRole("button", { name: "Request Password Reset" }).click();
await expect(page.getByRole("status")).toContainText("Status: Pending");
const secondPost = await page.waitForResponse((r) => r.url().includes("/api/v1/auth/password-reset-requests") && r.request().method() === "POST" && r !== firstPost);
const secondBody = await secondPost.json();
console.log(`REFERENCE_2=${secondBody.reference}`);
console.log(`STATUS_2=${secondBody.status}`);

await hold("BEFORE_APPROVE");
await page.getByRole("button", { name: "Check Approval Status" }).click();
await expect(page.getByRole("status")).toContainText("Status: Approved");
await expect(page.getByText("Create a new password.")).toBeVisible();
await expect(page.getByLabel("New Password")).toBeVisible();
await expect(page.getByLabel("Confirm Password")).toBeVisible();
console.log("APPROVED_UI_OK=1");
if (!newPassword) throw new Error("PILOT_NEW_PASSWORD is required");

await page.getByLabel("New Password").fill(newPassword);
await page.getByLabel("Confirm Password").fill(newPassword);
await page.getByRole("button", { name: "Reset Password" }).click();
await expect(page.getByText("Password reset. You can sign in now.")).toBeVisible();
console.log("RESET_COMPLETE_OK=1");

await browser.close();
console.log("DONE=1");
