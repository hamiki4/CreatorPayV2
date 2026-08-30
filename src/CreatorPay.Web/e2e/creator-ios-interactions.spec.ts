import {devices, type Locator, type Page} from '@playwright/test'
import {expect, test} from './fixtures'
import {login} from './auth-helpers'

const iphone = devices['iPhone 13']
const screenshotDir = process.env.E2E_SCREENSHOT_DIR
test.use({
  userAgent: iphone.userAgent,
  deviceScaleFactor: iphone.deviceScaleFactor,
  isMobile: true,
  hasTouch: true,
})

async function assertNoOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}

async function assertCreatorControl(control: Locator) {
  const style = await control.evaluate((node) => {
    const computed = getComputedStyle(node)
    return {
      backgroundColor: computed.backgroundColor,
      backgroundImage: computed.backgroundImage,
      userSelect: computed.getPropertyValue('user-select') || computed.getPropertyValue('-webkit-user-select'),
      tapHighlight: computed.getPropertyValue('-webkit-tap-highlight-color'),
    }
  })
  expect(style.userSelect).toBe('none')
  expect(style.backgroundColor).not.toBe('rgb(21, 60, 39)')
  expect(style.backgroundColor).not.toBe('rgb(32, 93, 57)')
  expect(style.backgroundImage).not.toContain('rgb(21, 60, 39)')
  expect(style.tapHighlight).toContain('124, 77, 255')
}

async function assertPurpleCurrent(nav: Locator, name: string) {
  const item = nav.getByRole('button', {name, exact: true})
  await expect(item).toHaveAttribute('aria-current', 'page')
  await expect(item).toHaveCSS('color', 'rgb(124, 77, 255)')
}

const mobileViewports = [
  {width: 375, height: 812},
  {width: 390, height: 844},
  {width: 393, height: 852},
  {width: 430, height: 932},
]

for (const {width, height} of mobileViewports) {
  test(`Creator iOS interactions remain control-like and purple at ${width}px`, async ({page, browserName}) => {
    test.setTimeout(90_000)
    await page.setViewportSize({width, height})
    await login(page, 'creator@e2e.invalid')

    const nav = page.getByRole('navigation', {name: 'Creator sections'})
    await assertPurpleCurrent(nav, 'Home')
    for (const item of await nav.getByRole('button').all()) await assertCreatorControl(item)

    const confirmed = page.locator('.creator-home-stat').filter({hasText: 'Confirmed Sales'})
    const pending = page.locator('.creator-home-stat').filter({hasText: 'Pending Requests'})
    const upcoming = page.locator('.creator-home-stat').filter({hasText: 'Upcoming Payout'})
    await expect(confirmed.locator('strong')).toHaveText('2')
    await expect(pending.locator('strong')).toHaveText('1')

    await nav.getByRole('button', {name: 'Find Businesses', exact: true}).tap()
    await expect(page.getByRole('heading', {name: 'Find Businesses'})).toBeVisible()
    await assertPurpleCurrent(nav, 'Find Businesses')
    expect(await page.evaluate(() => getSelection()?.toString() ?? '')).toBe('')

    await nav.getByRole('button', {name: 'Home', exact: true}).tap()
    const start = page.getByRole('button', {name: /Start advertising/})
    await assertCreatorControl(start)
    await start.tap()
    await expect(page.getByRole('heading', {name: 'Find Businesses'})).toBeVisible()
    expect(await page.evaluate(() => getSelection()?.toString() ?? '')).toBe('')

    await nav.getByRole('button', {name: 'Home', exact: true}).tap()
    const pendingAgain = page.locator('.creator-home-stat').filter({hasText: 'Pending Requests'})
    await assertCreatorControl(pendingAgain)
    await pendingAgain.tap()
    await expect(page.getByRole('heading', {name: 'Requests'})).toBeVisible()
    await expect(page.locator('.request-groups .status-badge').filter({hasText: /^Pending$/})).toHaveCount(1)

    await nav.getByRole('button', {name: 'Home', exact: true}).tap()
    const upcomingAgain = page.locator('.creator-home-stat').filter({hasText: 'Upcoming Payout'})
    await assertCreatorControl(upcomingAgain)
    await upcomingAgain.tap()
    await expect(page.getByRole('heading', {name: 'Payout Overview'})).toBeVisible()
    await expect(page.getByText('40.00', {exact: true})).toBeVisible()

    await nav.getByRole('button', {name: 'Active Ads', exact: true}).tap()
    const awaiting = page.locator('.creator-ads-row').filter({hasText: 'Approved Metric Business'})
    const addVideo = awaiting.getByRole('button', {name: 'Add Promo Video', exact: true})
    await expect(awaiting.getByText('Awaiting Video', {exact: true})).toBeVisible()
    await expect(addVideo).toBeVisible()
    const addVideoBox = await addVideo.boundingBox()
    expect(addVideoBox).not.toBeNull()
    expect(addVideoBox!.height).toBeGreaterThanOrEqual(30)
    expect(addVideoBox!.height).toBeLessThanOrEqual(34)
    expect(addVideoBox!.width).toBeLessThanOrEqual(140)
    await expect(awaiting.locator('.creator-ads-activated')).toBeEmpty()
    await expect(awaiting.locator('.creator-ads-days')).toBeEmpty()

    await addVideo.tap()
    const dialog = page.getByRole('dialog', {name: 'Add Promo Video'})
    await expect(dialog).toBeVisible()
    await expect(dialog.getByText('Paste the exact TikTok video link, not a profile page.')).toBeVisible()
    const submit = dialog.getByRole('button', {name: 'Submit for Approval'})
    await expect(submit).toHaveCSS('background-color', 'rgb(124, 77, 255)')
    await expect(submit).toHaveCSS('color', 'rgb(255, 255, 255)')
    const dialogBox = await dialog.boundingBox()
    expect(dialogBox).not.toBeNull()
    expect(dialogBox!.x).toBeGreaterThanOrEqual(0)
    expect(dialogBox!.x + dialogBox!.width).toBeLessThanOrEqual(width)
    expect(dialogBox!.height).toBeLessThan(height)
    await dialog.getByLabel('Promotion Video Link').fill('https://www.tiktok.com/@profile-only')
    await submit.tap()
    await expect(dialog.getByRole('alert')).toHaveText('Enter a TikTok video link, not a profile link.')
    await dialog.getByRole('button', {name: 'Cancel'}).tap()
    await expect(dialog).toBeHidden()

    const header = page.locator('.role-creator .account-header')
    const avatar = header.locator('.account-header-avatar')
    const title = header.getByRole('heading', {name: 'Creator', exact: true})
    const identity = header.locator('.account-identity--creator')
    const actions = header.locator('.account-actions')
    const [headerBox, avatarBox, titleBox, identityBox, actionsBox] = await Promise.all([
      header.boundingBox(), avatar.boundingBox(), title.boundingBox(), identity.boundingBox(), actions.boundingBox(),
    ])
    expect(headerBox).not.toBeNull()
    expect(avatarBox).not.toBeNull()
    expect(titleBox).not.toBeNull()
    expect(identityBox).not.toBeNull()
    expect(actionsBox).not.toBeNull()
    expect(avatarBox!.x - (titleBox!.x + titleBox!.width)).toBeGreaterThanOrEqual(8)
    expect(avatarBox!.x - (titleBox!.x + titleBox!.width)).toBeLessThanOrEqual(12)
    expect(Math.abs((avatarBox!.y + avatarBox!.height / 2) - (titleBox!.y + titleBox!.height / 2))).toBeLessThanOrEqual(1)
    expect(avatarBox!.width).toBeGreaterThanOrEqual(44)
    expect(avatarBox!.width).toBeLessThanOrEqual(48)
    expect(avatarBox!.height).toBeGreaterThanOrEqual(44)
    expect(avatarBox!.height).toBeLessThanOrEqual(48)
    expect(identityBox!.y).toBeGreaterThanOrEqual(avatarBox!.y + avatarBox!.height)
    expect(avatarBox!.x + avatarBox!.width).toBeLessThanOrEqual(actionsBox!.x)
    expect(headerBox!.x + headerBox!.width - (actionsBox!.x + actionsBox!.width)).toBeLessThanOrEqual(1)
    expect(headerBox!.height).toBeLessThanOrEqual(104)

    await assertNoOverflow(page)
    expect(await page.evaluate(() => getSelection()?.toString() ?? '')).toBe('')
    if (screenshotDir) await page.screenshot({path: `${screenshotDir}/creator-header-${browserName}-${width}.png`, animations: 'disabled'})
  })
}
