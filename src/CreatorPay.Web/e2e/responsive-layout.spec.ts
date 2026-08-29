import { expect, test } from './fixtures'
import type { Page } from '@playwright/test'
import { login } from './auth-helpers'

const widths = [375, 390, 430]

async function assertNoOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}

for (const width of widths) {
  test(`mobile customer, creator, and business layouts stay within ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 })

    await login(page, 'shopper@e2e.invalid')
    await expect(page.getByRole('heading', { name: 'Discover Promotions' })).toBeVisible()
    await expect(page.getByPlaceholder('Search business or creator')).toBeVisible()
    await expect(page.getByLabel('Open filters')).toBeVisible()
    await page.getByLabel('Open filters').click()
    await expect(page.getByRole('dialog', { name: 'Filters' })).toBeVisible()
    await expect(page.getByText('Business Type', { exact: true })).toBeVisible()
    await expect(page.getByText('All Business Types', { exact: true })).toBeVisible()
    await expect(page.getByText('Nearest', { exact: true })).toBeVisible()
    await expect(page.getByText('Recommended', { exact: true })).toBeVisible()
    await expect(page.getByText('📍 Near Me')).toBeVisible()
    await assertNoOverflow(page)

    await login(page, 'creator@e2e.invalid')
    await page.getByRole('button', { name: 'Active Ads', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Creator' })).toBeVisible()
    const activeRow = page.locator('.creator-ads-row.relationship-active').filter({ hasText: 'Active E2E Business' }).first()
    await expect(activeRow).toBeVisible()
    await expect(activeRow.getByRole('button', { name: 'Add Promo Video' })).toBeVisible()
    await assertNoOverflow(page)

    await login(page, 'business-1@e2e.invalid')
    await expect(page.getByRole('heading', { name: 'Business' })).toBeVisible()
    await page.getByRole('button', { name: 'Active Ads', exact: true }).click()
    await expect(page.getByRole('button', { name: 'Requests', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Checkout', exact: true })).toBeVisible()
    await assertNoOverflow(page)
  })
}
