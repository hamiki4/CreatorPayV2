import { expect, test } from "./fixtures";

const api = process.env.E2E_API_URL!;
const adminEmail = process.env.E2E_ADMIN_EMAIL!;
const adminPassword = process.env.E2E_ADMIN_PASSWORD!;
const businessTypes = [
  "Restaurant / Café",
  "Grocery / Mini-market",
  "Clothing / Boutique",
  "Beauty / Salon",
  "Furniture",
  "Electronics",
  "Hotel / Travel",
  "Professional Services",
  "Other",
];

async function signInAsPlatformAdmin(page: any, request: any) {
  const login = await request.post(`${api}/api/v1/auth/login`, {
    data: { email: adminEmail, password: adminPassword },
  });
  expect(login.ok(), await login.text()).toBeTruthy();
  const body = await login.json();
  expect(body.user.role).toBe("PlatformAdmin");
  await page.addInitScript(
    ({ accessToken, refreshToken }) => {
      localStorage.setItem("creatorpay_access_token", accessToken);
      localStorage.setItem("creatorpay_refresh_token", refreshToken);
    },
    { accessToken: body.accessToken, refreshToken: body.refreshToken },
  );
}

test("PlatformAdmin can open and save the business type editor from the live business accounts rows", async ({
  page,
  request,
}) => {
  await signInAsPlatformAdmin(page, request);
  const responsePromise = page.waitForResponse((r) => {
    const url = new URL(r.url());
    return (
      r.request().method() === "GET" &&
      r.ok() &&
      url.pathname === "/api/v1/admin/accounts" &&
      url.searchParams.get("role") === "MerchantAdmin"
    );
  });
  await page.goto("/admin/business-accounts");
  const response = await responsePromise;
  const payload = await response.json();
  const row = payload.items.find(
    (item: Record<string, unknown>) =>
      item.merchantId && (item.businessName || item.publicBusinessId),
  );
  expect(row, "expected at least one business row with merchantId").toBeTruthy();
  expect(row.merchantId, "merchantId should be present").toBeTruthy();

  const currentType = String(row.merchantBusinessType ?? "Other");
  const nextType = businessTypes.find((type) => type !== currentType) ?? "Furniture";
  const rowLocator = page.getByRole("row").filter({
    hasText: String(row.businessName ?? row.publicBusinessId ?? row.name ?? row.merchantId),
  });

  await expect(rowLocator.getByRole("button", { name: "Edit Business Type" })).toBeVisible();
  await rowLocator.getByRole("button", { name: "Edit Business Type" }).click();

  const dialog = page.getByRole("dialog", { name: "Edit Business Type" });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText(new RegExp(`Current Business Type:\\s*${currentType.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}`))).toBeVisible();

  const select = dialog.getByLabel("New Business Type");
  await expect(select).toHaveValue(currentType);
  await select.selectOption(nextType);

  const update = page.waitForResponse((r) => {
    const url = new URL(r.url());
    return (
      r.request().method() === "PUT" &&
      r.ok() &&
      url.pathname === `/api/v1/admin/merchants/${row.merchantId}/business-type`
    );
  });
  await dialog.getByRole("button", { name: "Save" }).click();
  await update;
  await expect(page.getByRole("status")).toContainText("Business Type updated.");

  await page.reload();
  await expect(
    page
      .getByRole("row")
      .filter({ hasText: String(row.businessName ?? row.publicBusinessId ?? row.name ?? row.merchantId) }),
  ).toContainText(nextType);

  await page
    .getByRole("row")
    .filter({ hasText: String(row.businessName ?? row.publicBusinessId ?? row.name ?? row.merchantId) })
    .getByRole("button", { name: "Edit Business Type" })
    .click();
  const restoreDialog = page.getByRole("dialog", { name: "Edit Business Type" });
  await expect(restoreDialog).toBeVisible();
  await restoreDialog.getByLabel("New Business Type").selectOption(currentType);
  await Promise.all([
    page.waitForResponse((r) => {
      const url = new URL(r.url());
      return (
        r.request().method() === "PUT" &&
        r.ok() &&
        url.pathname === `/api/v1/admin/merchants/${row.merchantId}/business-type`
      );
    }),
    restoreDialog.getByRole("button", { name: "Save" }).click(),
  ]);
  await page.reload();
  await expect(
    page
      .getByRole("row")
      .filter({ hasText: String(row.businessName ?? row.publicBusinessId ?? row.name ?? row.merchantId) }),
  ).toContainText(currentType);
});
