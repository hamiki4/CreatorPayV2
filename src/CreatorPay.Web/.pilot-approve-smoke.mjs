import { chromium } from "playwright";
import { expect } from "@playwright/test";

const webUrl = process.env.PILOT_WEB_URL ?? "https://pilot.weymela.com";
const phone = process.env.PILOT_PHONE ?? "+251911046240";
const newPassword = process.env.PILOT_NEW_PASSWORD;
const holdMs = Number(process.env.PILOT_HOLD_MS ?? "25000");

async function hold() {
  console.log("WAITING_FOR_APPROVE=1");
  await new Promise((resolve) => setTimeout(resolve, holdMs));
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1200 } });

await page.goto(webUrl, { waitUntil: "networkidle" });
if (await page.getByRole("button", { name: "Already have an account? Sign In" }).isVisible().catch(() => false)) {
  await page.getByRole("button", { name: "Already have an account? Sign In" }).click();
}
await page.getByRole("button", { name: "Forgot Password" }).click();
await page.getByLabel("Phone Number").fill(phone);
await page.getByRole("button", { name: "Request Password Reset" }).click();
await expect(page.getByRole("status")).toContainText("Status: Pending");
await expect(page.getByRole("status")).toContainText("waiting for support approval");
console.log(`REFERENCE=${process.env.PILOT_REFERENCE ?? "current-pending"}`);

await hold();
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

await page.getByRole("button", { name: "Back to Sign In" }).click();
await page.getByLabel("Phone Number").fill(phone);
await page.getByLabel("Password").fill(newPassword);
await page.getByRole("button", { name: "Sign In" }).click();
await page.waitForLoadState("networkidle");
console.log(`SIGNED_IN_URL=${page.url()}`);

await browser.close();
console.log("DONE=1");
