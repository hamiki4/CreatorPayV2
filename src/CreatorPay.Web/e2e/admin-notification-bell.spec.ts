import {devices, type Page, type Route} from '@playwright/test'
import {expect, test} from './fixtures'

const iphone = devices['iPhone 13']
const screenshotDir = process.env.E2E_SCREENSHOT_DIR
test.use({
  userAgent: iphone.userAgent,
  deviceScaleFactor: iphone.deviceScaleFactor,
  isMobile: true,
  hasTouch: true,
})

const roles = ['PlatformAdmin', 'OperationsAdmin'] as const
const viewports = [
  {width: 375, height: 812},
  {width: 390, height: 844},
  {width: 393, height: 852},
  {width: 430, height: 932},
]

function fakeToken(role: typeof roles[number]) {
  const payload = {
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role,
    account_status: 'Active',
    sub: role === 'PlatformAdmin' ? 'platform-test' : 'operations-test',
  }
  return `x.${Buffer.from(JSON.stringify(payload)).toString('base64url')}.x`
}

function notices() {
  const now = new Date().toISOString()
  return [
    {notificationId: 'NTF-ONE', type: 'SystemOperationalAlert', title: 'Admin task ready', body: 'Review the current admin task.', createdAtUtc: now, data: {}},
    {notificationId: 'NTF-TWO', type: 'MerchantRegistrationReceived', title: 'Business review ready', body: 'A Business is waiting for review.', createdAtUtc: now, data: {TargetPath: '/admin/business-review'}},
    {notificationId: 'NTF-THREE', type: 'SecurityAlert', title: 'Security review', body: 'Review the current security alert.', createdAtUtc: now, data: {}},
  ]
}

async function installAdminSessionAndApi(page: Page, role: typeof roles[number]) {
  await page.addInitScript(({accessToken}) => {
    localStorage.setItem('creatorpay_access_token', accessToken)
    localStorage.removeItem('creatorpay_refresh_token')
  }, {accessToken: fakeToken(role)})

  let rows = notices()
  await page.route('https://api-pilot.weymela.com/**', async (route: Route) => {
    const request = route.request()
    const url = new URL(request.url())
    const json = (value: unknown, status = 200) => route.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify(value),
      headers: {'access-control-allow-origin': '*'},
    })

    if (url.pathname === '/api/v1/notifications/unread-count') return json({count: rows.filter((x) => !('readAtUtc' in x)).length})
    if (url.pathname === '/api/v1/notifications' && request.method() === 'GET') return json({items: rows, page: 1, pageSize: 30, total: rows.length})
    const read = url.pathname.match(/^\/api\/v1\/notifications\/(NTF-[A-Z]+)\/read$/)
    if (read && request.method() === 'POST') {
      rows = rows.map((x) => x.notificationId === read[1] ? {...x, readAtUtc: new Date().toISOString()} : x)
      return route.fulfill({status: 204, body: ''})
    }
    if (url.pathname === '/api/v1/notifications/read-all' && request.method() === 'POST') {
      rows = rows.map((x) => ({...x, readAtUtc: new Date().toISOString()}))
      return route.fulfill({status: 204, body: ''})
    }
    if (url.pathname === '/api/v1/admin/dashboard/summary') return json({})
    if (url.pathname === '/api/v1/creators/pending' || url.pathname === '/api/v1/merchants/pending') return json([])
    return json([])
  })
}

async function expectBoundedPanel(page: Page) {
  const drawer = page.getByRole('complementary', {name: 'Notifications'})
  const box = await drawer.boundingBox()
  expect(box).not.toBeNull()
  expect(box!.x).toBeGreaterThanOrEqual(0)
  expect(box!.y).toBeGreaterThanOrEqual(0)
  expect(box!.x + box!.width).toBeLessThanOrEqual((await page.evaluate(() => innerWidth)) + 0.5)
  expect(box!.y + box!.height).toBeLessThanOrEqual((await page.evaluate(() => innerHeight)) + 0.5)
  const layers = await page.evaluate(() => ({
    drawer: Number(getComputedStyle(document.querySelector('.notification-drawer')!).zIndex),
    scrim: Number(getComputedStyle(document.querySelector('.admin-notification-scrim')!).zIndex),
    scrimBackground: getComputedStyle(document.querySelector('.admin-notification-scrim')!).backgroundColor,
  }))
  expect(layers.drawer).toBeGreaterThan(layers.scrim)
  expect(layers.scrimBackground).toBe('rgba(15, 23, 42, 0.18)')
  const firstItem = drawer.locator('.notification-item').first()
  const itemBox = await firstItem.boundingBox()
  const contentBox = await firstItem.locator('span').nth(1).boundingBox()
  expect(contentBox).not.toBeNull()
  expect(itemBox).not.toBeNull()
  expect(contentBox!.width).toBeGreaterThan(itemBox!.width * .5)
  expect(itemBox!.height).toBeLessThan(150)
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
}

for (const role of roles) {
  for (const viewport of viewports) {
    test(`${role} bell is bounded, readable, and live at ${viewport.width}px`, async ({page, browserName}) => {
      test.setTimeout(90_000)
      await page.setViewportSize(viewport)
      await installAdminSessionAndApi(page, role)
      await page.goto(role === 'PlatformAdmin' ? '/admin/dashboard' : '/admin/creator-review', {waitUntil: 'domcontentloaded'})

      const trigger = page.locator('.admin-notification-trigger')
      await expect(trigger).toHaveAttribute('aria-label', 'Notifications, 3 unread')
      await expect(trigger.locator('.notification-count')).toHaveText('3')
      await expect(trigger.locator('.notification-count')).toHaveCSS('background-color', 'rgb(180, 35, 24)')
      await expect(trigger.locator('.notification-count')).toHaveCSS('color', 'rgb(255, 255, 255)')
      const triggerBox = await trigger.boundingBox()
      expect(triggerBox!.width).toBeLessThanOrEqual(44)
      expect(triggerBox!.height).toBeLessThanOrEqual(44)

      await trigger.tap()
      await expect(page.getByRole('complementary', {name: 'Notifications'})).toBeVisible()
      await expectBoundedPanel(page)
      if (screenshotDir) await page.screenshot({path: `${screenshotDir}/${role.toLowerCase()}-${browserName}-${viewport.width}.png`, animations: 'disabled'})

      await page.getByRole('button', {name: /Admin task ready/}).tap()
      await expect(trigger).toHaveAttribute('aria-label', 'Notifications, 2 unread')
      await expect(trigger.locator('.notification-count')).toHaveText('2')

      await trigger.tap()
      await expectBoundedPanel(page)
      await page.getByRole('button', {name: /Business review ready/}).tap()
      await expect(page).toHaveURL(/\/admin\/business-review$/)
    })
  }

  test(`${role} bell remains compact and bounded on desktop`, async ({browser, browserName}) => {
    test.setTimeout(90_000)
    const context = await browser.newContext({viewport: {width: 1440, height: 900}, hasTouch: false, isMobile: false})
    const page = await context.newPage()
    await installAdminSessionAndApi(page, role)
    await page.goto(role === 'PlatformAdmin' ? 'https://pilot.weymela.com/admin/dashboard' : 'https://pilot.weymela.com/admin/creator-review', {waitUntil: 'domcontentloaded'})

    const trigger = page.locator('.admin-notification-trigger')
    await expect(trigger).toHaveAttribute('aria-label', 'Notifications, 3 unread')
    await trigger.click()
    await expectBoundedPanel(page)
    if (screenshotDir) await page.screenshot({path: `${screenshotDir}/${role.toLowerCase()}-${browserName}-desktop.png`, animations: 'disabled'})
    await page.locator('.admin-notification-close').click()
    await expect(page.getByRole('complementary', {name: 'Notifications'})).toBeHidden()
    await context.close()
  })
}
