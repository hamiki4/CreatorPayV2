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

const roles = [
  {name: 'customer', identity: 'shopper@e2e.invalid', accent: 'rgb(31, 138, 59)'},
  {name: 'creator', identity: 'creator@e2e.invalid', accent: 'rgb(124, 77, 255)'},
  {name: 'business', identity: 'business-1@e2e.invalid', accent: 'rgb(37, 99, 235)'},
] as const

const viewports = [
  {width: 375, height: 812},
  {width: 390, height: 844},
  {width: 393, height: 852},
  {width: 430, height: 932},
]

async function controlStyle(control: Locator) {
  return control.evaluate((node) => {
    const style = getComputedStyle(node)
    return {
      background: style.backgroundColor,
      color: style.color,
      userSelect: style.userSelect || style.getPropertyValue('-webkit-user-select'),
      tapHighlight: style.getPropertyValue('-webkit-tap-highlight-color'),
    }
  })
}

async function expectNoOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
}

for (const {width, height} of viewports) {
  test(`shared notification controls stay neutral on iPhone at ${width}px`, async ({page, browserName}) => {
    test.setTimeout(120_000)
    await page.setViewportSize({width, height})
    expect(await page.evaluate(() => matchMedia('(hover: hover) and (pointer: fine)').matches)).toBe(false)

    for (const role of roles) {
      await login(page, role.identity)
      const trigger = page.getByRole('button', {name: 'Notifications', exact: true})
      const initial = await controlStyle(trigger)
      expect(initial.background).not.toBe('rgb(32, 93, 57)')
      expect(initial.userSelect).toBe('none')
      expect(initial.tapHighlight).toBe('rgba(0, 0, 0, 0)')

      await trigger.tap()
      const drawer = page.getByRole('complementary', {name: 'Notifications'})
      await expect(drawer).toBeVisible()
      await expect(trigger).toHaveAttribute('aria-expanded', 'true')
      await expect(trigger).toHaveCSS('color', role.accent)
      const expanded = await controlStyle(trigger)
      expect(expanded.background).not.toBe('rgb(32, 93, 57)')

      const badge = trigger.locator('.notification-count')
      if (await badge.count()) await expect(badge).toHaveCSS('background-color', 'rgb(216, 50, 43)')
      for (const row of await drawer.locator('.notification-item').all()) {
        const style = await controlStyle(row)
        expect(style.background).not.toBe('rgb(32, 93, 57)')
        expect(style.userSelect).toBe('none')
      }
      const action = drawer.locator('.notification-action')
      if (await action.count()) {
        const style = await controlStyle(action)
        expect(style.background).not.toBe('rgb(32, 93, 57)')
        expect(style.userSelect).toBe('none')
      }

      expect(await page.evaluate(() => getSelection()?.toString() ?? '')).toBe('')
      await expectNoOverflow(page)
      await page.waitForTimeout(200)
      if (screenshotDir) await page.screenshot({path: `${screenshotDir}/${role.name}-notifications-${browserName}-${width}.png`, animations: 'disabled'})
      await drawer.locator('.notification-close').tap()
      await expect(drawer).toBeHidden()

      await trigger.tap()
      await expect(drawer).toBeVisible()
      expect((await controlStyle(trigger)).background).not.toBe('rgb(32, 93, 57)')
      await drawer.locator('.notification-close').tap()
    }
  })
}
