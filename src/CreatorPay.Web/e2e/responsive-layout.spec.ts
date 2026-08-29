import { expect, test } from './fixtures'
import type { Page } from '@playwright/test'
import { login } from './auth-helpers'

const widths = [375, 390, 393, 430]

async function assertNoOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}

async function assertNavIconSize(page: Page, selector: string) {
  const sizes = await page.locator(selector).evaluateAll((nodes) =>
    nodes.map((node) => {
      const style = getComputedStyle(node)
      return { width: Number.parseFloat(style.width), height: Number.parseFloat(style.height) }
    }),
  )
  expect(sizes.length).toBeGreaterThan(0)
  for (const size of sizes) {
    expect(size.width).toBeGreaterThanOrEqual(18)
    expect(size.width).toBeLessThanOrEqual(26)
    expect(size.height).toBeGreaterThanOrEqual(18)
    expect(size.height).toBeLessThanOrEqual(26)
  }
}

async function assertNavGeometry(page: Page, selector: string, expectedCount: number, minWidth: number) {
  const nav = page.locator(selector)
  await expect(nav).toBeVisible()
  await expect(nav.locator('button')).toHaveCount(expectedCount)
  await expect(nav).toHaveCSS('position', 'fixed')
  await expect(nav.locator('[aria-current="page"]')).toHaveCount(1)
  const navBox = await nav.boundingBox()
  expect(navBox).not.toBeNull()
  expect(navBox!.height).toBeGreaterThanOrEqual(64)
  expect(navBox!.height).toBeLessThanOrEqual(82)
  const boxes = await nav.locator('button').evaluateAll((nodes) =>
    nodes.map((node) => {
      const box = node.getBoundingClientRect()
      return { width: box.width, height: box.height }
    }),
  )
  expect(boxes.length).toBe(expectedCount)
  for (const box of boxes) {
    expect(box.width).toBeGreaterThanOrEqual(minWidth)
    expect(box.height).toBeGreaterThanOrEqual(44)
    expect(box.height).toBeLessThanOrEqual(84)
  }
  await expect(page.locator('main, .account-shell').first()).toBeVisible()
}

for (const width of widths) {
  test(`mobile customer, creator, and business layouts stay within ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 })

    await login(page, 'shopper@e2e.invalid')
    await expect(page.getByRole('heading', { name: 'Discover Promotions' })).toBeVisible()
    await expect(page.getByPlaceholder('Search business or creator')).toBeVisible()
    const customerNav = page.getByRole('navigation', { name: 'Customer navigation' })
    await expect(customerNav.getByRole('button')).toHaveText(['Discover', 'Cashback', 'Profile'])
    await expect(customerNav.getByRole('button', { name: 'Discover', exact: true })).toHaveAttribute('aria-current', 'page')
    await assertNavGeometry(page, 'nav[aria-label="Customer navigation"]', 3, 86)
    await assertNavIconSize(page, 'nav[aria-label="Customer navigation"] .workspace-nav-icon svg')
    await expect(page.getByLabel('Open filters')).toBeVisible()
    await page.getByLabel('Open filters').click()
    await expect(page.getByRole('dialog', { name: 'Filters' })).toBeVisible()
    await expect(page.getByText('Business Type', { exact: true })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Business Type' })).toHaveValue('')
    await expect(page.getByText('Nearest', { exact: true })).toBeVisible()
    await expect(page.getByText('Recommended', { exact: true })).toBeVisible()
    await expect(page.getByText('📍 Near Me')).toBeVisible()
    await page.getByRole('button', { name: 'Done', exact: true }).click()
    await expect(page.getByRole('dialog', { name: 'Filters' })).toBeHidden()
    await page.getByRole('button', { name: 'Profile', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Help', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Sign out', exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Cashback', exact: true }).click()
    await expect(page.getByText('Available Cashback', { exact: true })).toBeVisible()
    await assertNoOverflow(page)

    await login(page, 'creator@e2e.invalid')
    const creatorNav = page.getByRole('navigation', { name: 'Creator sections' })
    await expect(creatorNav.getByRole('button')).toHaveText(['Find Businesses', 'Active Ads', 'Requests', 'Payout', 'Profile'])
    await expect(creatorNav.getByRole('button', { name: 'Find Businesses', exact: true })).toHaveAttribute('aria-current', 'page')
    await assertNavGeometry(page, 'nav[aria-label="Creator sections"]', 5, 58)
    await assertNavIconSize(page, 'nav[aria-label="Creator sections"] .workspace-nav-icon svg')
    await expect(page.locator('.business-discovery-card .business-card-days')).toHaveCount(0)
    const creatorStatus = page.locator('.account-identity--creator .account-status')
    await expect(creatorStatus).toHaveCSS('color', 'rgb(23, 107, 70)')
    const settings = page.getByRole('button', { name: 'Settings', exact: true })
    await settings.click()
    const settingsMenu = page.getByRole('menu')
    await expect(settingsMenu).toBeVisible()
    const settingsBox = await settingsMenu.boundingBox()
    expect(settingsBox).not.toBeNull()
    expect(settingsBox!.x).toBeGreaterThanOrEqual(0)
    expect(settingsBox!.x + settingsBox!.width).toBeLessThanOrEqual(width)
    await settings.click()
    await page.getByRole('button', { name: 'Active Ads', exact: true }).click()
    await expect(creatorNav.getByRole('button', { name: 'Active Ads', exact: true })).toHaveAttribute('aria-current', 'page')
    await expect(creatorNav.getByRole('button', { name: 'Find Businesses', exact: true })).not.toHaveAttribute('aria-current', 'page')
    await page.getByRole('button', { name: 'Requests', exact: true }).click()
    await expect(creatorNav.getByRole('button', { name: 'Requests', exact: true })).toHaveAttribute('aria-current', 'page')
    await page.getByRole('button', { name: 'Payout', exact: true }).click()
    await expect(creatorNav.getByRole('button', { name: 'Payout', exact: true })).toHaveAttribute('aria-current', 'page')
    await page.getByRole('button', { name: 'Active Ads', exact: true }).click()
    await expect(creatorNav.getByRole('button', { name: 'Active Ads', exact: true })).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('heading', { name: 'Creator' })).toBeVisible()
    const activeRow = page.locator('.creator-ads-row.relationship-active').filter({ hasText: 'Active E2E Business' }).first()
    await expect(activeRow).toBeVisible()
    await expect(activeRow.getByRole('button', { name: 'Add Promo Video' })).toBeVisible()
    await expect(activeRow.locator('.creator-ads-days')).toContainText('days left')
    await page.getByRole('button', { name: 'Profile', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
    await expect(page.getByText('Creator ID', { exact: true })).toBeVisible()
    await assertNoOverflow(page)

    await login(page, 'business-1@e2e.invalid')
    const businessNav = page.getByRole('navigation', { name: 'Business sections' })
    await expect(page.getByRole('heading', { name: 'Business' })).toBeVisible()
    await expect(businessNav.getByRole('button')).toHaveText(['Creators', 'Active Ads', 'Requests', 'Checkout', 'Profile'])
    await expect(businessNav.getByRole('button', { name: 'Creators', exact: true })).toHaveAttribute('aria-current', 'page')
    await assertNavGeometry(page, 'nav[aria-label="Business sections"]', 5, 58)
    await assertNavIconSize(page, 'nav[aria-label="Business sections"] .workspace-nav-icon svg')
    await page.getByRole('button', { name: 'Active Ads', exact: true }).click()
    await expect(businessNav.getByRole('button', { name: 'Active Ads', exact: true })).toHaveAttribute('aria-current', 'page')
    await expect(businessNav.getByRole('button', { name: 'Creators', exact: true })).not.toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('button', { name: 'Requests', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Checkout', exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Requests', exact: true }).click()
    await expect(businessNav.getByRole('button', { name: 'Requests', exact: true })).toHaveAttribute('aria-current', 'page')
    await page.getByRole('button', { name: 'Checkout', exact: true }).click()
    await expect(businessNav.getByRole('button', { name: 'Checkout', exact: true })).toHaveAttribute('aria-current', 'page')
    await page.getByRole('button', { name: 'Profile', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
    await expect(page.getByText('Business Type')).toBeVisible()
    await assertNoOverflow(page)
  })
}

test('desktop role navigation stays in the top chrome and remains compact', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 })

  await login(page, 'shopper@e2e.invalid')
  const customerNav = page.getByRole('navigation', { name: 'Customer navigation' })
  await expect(customerNav).not.toHaveCSS('position', 'fixed')
  await expect(customerNav.getByRole('button')).toHaveText(['Discover', 'Cashback', 'Profile'])
  await assertNavIconSize(page, 'nav[aria-label="Customer navigation"] .workspace-nav-icon svg')
  expect((await customerNav.boundingBox())!.height).toBeLessThanOrEqual(64)

  await login(page, 'creator@e2e.invalid')
  const creatorNav = page.getByRole('navigation', { name: 'Creator sections' })
  await expect(creatorNav).not.toHaveCSS('position', 'fixed')
  await expect(creatorNav.getByRole('button')).toHaveText(['Find Businesses', 'Active Ads', 'Requests', 'Payout', 'Profile'])
  await assertNavIconSize(page, 'nav[aria-label="Creator sections"] .workspace-nav-icon svg')
  expect((await creatorNav.boundingBox())!.height).toBeLessThanOrEqual(64)

  await login(page, 'business-1@e2e.invalid')
  const businessNav = page.getByRole('navigation', { name: 'Business sections' })
  await expect(businessNav).not.toHaveCSS('position', 'fixed')
  await expect(businessNav.getByRole('button')).toHaveText(['Creators', 'Active Ads', 'Requests', 'Checkout', 'Profile'])
  await assertNavIconSize(page, 'nav[aria-label="Business sections"] .workspace-nav-icon svg')
  expect((await businessNav.boundingBox())!.height).toBeLessThanOrEqual(64)
  await assertNoOverflow(page)
})
