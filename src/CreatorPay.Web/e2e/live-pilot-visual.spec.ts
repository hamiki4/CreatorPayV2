import {expect, test} from './fixtures'
import type {Page} from '@playwright/test'
import {login} from './auth-helpers'

const liveMode = Boolean(process.env.E2E_EXTERNAL_WEB_URL)
const shots = process.env.E2E_SCREENSHOT_DIR ?? '/tmp/creatorpay-live-shots'
const selectedTab = process.env.E2E_VISUAL_TAB

type RoleCase = {
  role: 'customer' | 'creator' | 'business'
  identity: string
  navigation: string
  accent: string
  tabs: Array<{label: string; slug: string}>
}

const roles: RoleCase[] = [
  {
    role: 'customer',
    identity: 'shopper@e2e.invalid',
    navigation: 'Customer navigation',
    accent: 'rgb(31, 138, 59)',
    tabs: [
      {label: 'Discover', slug: 'discover'},
      {label: 'Cashback', slug: 'cashback'},
      {label: 'Profile', slug: 'profile'},
    ],
  },
  {
    role: 'creator',
    identity: 'creator@e2e.invalid',
    navigation: 'Creator sections',
    accent: 'rgb(124, 77, 255)',
    tabs: [
      {label: 'Find Businesses', slug: 'find'},
      {label: 'Active Ads', slug: 'active-ads'},
      {label: 'Requests', slug: 'requests'},
      {label: 'Confirmed Sales', slug: 'confirmed-sales'},
      {label: 'Payout', slug: 'payout'},
      {label: 'Profile', slug: 'profile'},
    ],
  },
  {
    role: 'business',
    identity: 'business-1@e2e.invalid',
    navigation: 'Business sections',
    accent: 'rgb(37, 99, 235)',
    tabs: [
      {label: 'Creators', slug: 'creators'},
      {label: 'Active Ads', slug: 'active-ads'},
      {label: 'Requests', slug: 'requests'},
      {label: 'Confirmed Sales', slug: 'confirmed-sales'},
      {label: 'Checkout', slug: 'checkout'},
      {label: 'Profile', slug: 'profile'},
    ],
  },
]

async function assertLiveGeometry(page: Page, role: RoleCase, mobile: boolean) {
  const nav = page.getByRole('navigation', {name: role.navigation})
  await expect(nav).toBeVisible()
  await expect(nav.locator('[aria-current="page"]')).toHaveCount(1)
  await expect(nav).toHaveCSS('position', mobile ? 'fixed' : 'static')
  const navBox = await nav.boundingBox()
  expect(navBox).not.toBeNull()
  expect(navBox!.height).toBeLessThanOrEqual(mobile ? 82 : 64)
  const icons = await nav.locator('.workspace-nav-icon svg').evaluateAll((nodes) =>
    nodes.map((node) => {
      const box = node.getBoundingClientRect()
      return {width: box.width, height: box.height}
    }),
  )
  expect(icons).toHaveLength(role.tabs.length)
  for (const icon of icons) {
    expect(icon.width).toBeGreaterThanOrEqual(18)
    expect(icon.width).toBeLessThanOrEqual(mobile ? 24 : 20)
    expect(icon.height).toBeGreaterThanOrEqual(18)
    expect(icon.height).toBeLessThanOrEqual(mobile ? 24 : 20)
  }
  const buttons = await nav.locator('button').evaluateAll((nodes) =>
    nodes.map((node) => {
      const box = node.getBoundingClientRect()
      const style = getComputedStyle(node)
      return {
        width: box.width,
        height: box.height,
        color: style.color,
        display: style.display,
        opacity: style.opacity,
        visibility: style.visibility,
      }
    }),
  )
  expect(buttons).toHaveLength(role.tabs.length)
  for (const button of buttons) {
    expect(button.width).toBeGreaterThanOrEqual(mobile ? 48 : 100)
    expect(button.height).toBeGreaterThanOrEqual(44)
    expect(button.display).not.toBe('none')
    expect(button.visibility).toBe('visible')
    expect(button.opacity).toBe('1')
    expect(button.color).not.toBe('rgba(0, 0, 0, 0)')
    expect(button.color).not.toBe('rgb(255, 255, 255)')
  }
  const navContents = await nav.locator('button').evaluateAll((nodes) =>
    nodes.map((node) => {
      const label = node.querySelector('.workspace-nav-label') as HTMLElement
      const icon = node.querySelector('.workspace-nav-icon') as HTMLElement
      const box = node.getBoundingClientRect()
      const topmost = document.elementFromPoint(box.x + box.width / 2, box.y + box.height / 2)
      return {
        label: label.textContent,
        labelDisplay: getComputedStyle(label).display,
        labelColor: getComputedStyle(label).color,
        labelWidth: label.getBoundingClientRect().width,
        iconDisplay: getComputedStyle(icon).display,
        iconColor: getComputedStyle(icon).color,
        iconWidth: icon.getBoundingClientRect().width,
        topmostIsButton: topmost?.closest('button') === node,
      }
    }),
  )
  expect(navContents.map((item) => item.label)).toEqual(role.tabs.map((tab) => tab.label))
  for (const content of navContents) {
    expect(content.labelDisplay).not.toBe('none')
    expect(content.labelColor).not.toBe('rgb(255, 255, 255)')
    expect(content.labelWidth).toBeGreaterThan(1)
    expect(content.iconDisplay).not.toBe('none')
    expect(content.iconColor).not.toBe('rgb(255, 255, 255)')
    expect(content.iconWidth).toBeGreaterThanOrEqual(18)
    expect(content.topmostIsButton).toBe(true)
  }
  await expect(nav.locator('[aria-current="page"]')).toHaveCSS('color', role.accent)
  await expect(page.locator('.account-shell')).toBeVisible()
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}

for (const viewport of [
  {name: '375', width: 375, height: 812},
  {name: '390', width: 390, height: 844},
  {name: '393', width: 393, height: 852},
  {name: '430', width: 430, height: 932},
  {name: 'desktop', width: 1280, height: 900},
]) {
  for (const role of roles) {
    test(`live PILOT ${role.role} navigation and screenshots at ${viewport.name}`, async ({page}) => {
      test.skip(!liveMode, 'Runs only when the public PILOT bundle is selected.')
      test.setTimeout(90_000)
      await page.setViewportSize({width: viewport.width, height: viewport.height})
      await login(page, role.identity)
      const scriptAsset = await page.locator('script[type="module"]').getAttribute('src')
      const cssAsset = await page.locator('link[rel="stylesheet"]').getAttribute('href')
      expect(scriptAsset).toMatch(/^\/assets\/index-[A-Za-z0-9_-]+\.js$/)
      expect(cssAsset).toMatch(/^\/assets\/index-[A-Za-z0-9_-]+\.css$/)
      const nav = page.getByRole('navigation', {name: role.navigation})
      await expect(nav.getByRole('button')).toHaveText(role.tabs.map((tab) => tab.label))
      if (role.role === 'creator') {
        await expect(page.locator('.account-identity--creator .account-status')).toHaveCSS('color', 'rgb(23, 107, 70)')
        const nameBox = await page.locator('.account-identity-name').boundingBox()
        const avatarBox = await page.locator('.account-identity-avatar').boundingBox()
        expect(nameBox).not.toBeNull()
        expect(avatarBox).not.toBeNull()
        expect(avatarBox!.x).toBeGreaterThan(nameBox!.x)
        if (viewport.width < 651) {
          const settings = page.getByRole('button', {name: 'Settings', exact: true})
          await settings.click()
          const menu = page.getByRole('menu')
          await expect(menu).toBeVisible()
          const menuBox = await menu.boundingBox()
          expect(menuBox).not.toBeNull()
          expect(menuBox!.x).toBeGreaterThanOrEqual(0)
          expect(menuBox!.x + menuBox!.width).toBeLessThanOrEqual(viewport.width)
          await page.mouse.move(0, 0)
          await page.screenshot({path: `${shots}/creator-settings-${viewport.name}.png`, animations: 'disabled'})
          await settings.click()
        }
      }
      for (const tab of role.tabs.filter((item) => !selectedTab || item.slug === selectedTab)) {
        const tabButton = nav.getByRole('button', {name: tab.label, exact: true})
        if ((await tabButton.getAttribute('aria-current')) !== 'page') await tabButton.click()
        await expect(nav.getByRole('button', {name: tab.label, exact: true})).toHaveAttribute('aria-current', 'page')
        await expect.poll(() => page.evaluate(() => window.scrollY)).toBeLessThanOrEqual(1)
        await assertLiveGeometry(page, role, viewport.width < 651)
        if (role.role === 'creator' && tab.slug === 'find') {
          await expect(page.locator('.business-discovery-card .business-card-days')).toHaveCount(0)
        }
        if (role.role === 'creator' && tab.slug === 'active-ads') {
          await expect(page.locator('.creator-ads-row.relationship-active .creator-ads-days').first()).toContainText('days left')
        }
        if (role.role === 'creator' && tab.slug === 'profile') {
          const creatorIdSize = Number.parseFloat(await page.locator('.creator-id-profile > strong').evaluate((node) => getComputedStyle(node).fontSize))
          const copySize = Number.parseFloat(await page.getByRole('button', {name: 'Copy Creator ID'}).evaluate((node) => getComputedStyle(node).fontSize))
          expect(creatorIdSize).toBeLessThanOrEqual(44)
          expect(copySize).toBeLessThanOrEqual(13)
        }
        await page.mouse.move(0, 0)
        await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))))
        await page.waitForTimeout(500)
        await page.screenshot({path: `${shots}/${role.role}-${tab.slug}-${viewport.name}.png`, animations: 'disabled'})
      }
    })
  }
}
