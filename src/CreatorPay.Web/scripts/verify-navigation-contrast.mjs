import { chromium, devices } from 'playwright'
import assert from 'node:assert/strict'

const baseUrl = process.env.WEB_BASE_URL ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch({ headless: true })
const rgb = value => value.match(/[\d.]+/g).slice(0, 3).map(Number)
const luminance = value => rgb(value).map(x => x / 255).map(x => x <= .03928 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4).reduce((sum, x, i) => sum + x * [.2126, .7152, .0722][i], 0)
const contrast = (foreground, background) => {
  const [lighter, darker] = [luminance(foreground), luminance(background)].sort((a, b) => b - a)
  return (lighter + .05) / (darker + .05)
}

try {
  for (const [name, options] of [['desktop Chromium', { viewport: { width: 1440, height: 900 } }], ['Pixel 5', devices['Pixel 5']]]) {
    const context = await browser.newContext(options)
    const page = await context.newPage()
    await page.goto(baseUrl, { waitUntil: 'networkidle' })
    await page.locator('#root').evaluate(root => {
      root.innerHTML = `<main class="app role-business"><nav class="creator-tabs"><button class="active">Cashiers</button><button>Wallet</button></nav></main>
        <main class="app role-creator"><nav class="creator-tabs"><button class="active">Active Ads</button></nav></main>
        <main class="role-shopper shopper"><nav><button class="active">Discover Businesses</button></nav></main>
        <main class="app role-cashier"><nav class="creator-tabs"><button class="active">New Purchase</button></nav></main>
        <aside class="admin-sidebar"><nav><a href="#" aria-current="page">Accounts</a></nav></aside>
        <article class="shopper-relationship-row"><strong data-label="Business">471 mart</strong><span data-label="Creator Promoting">AYELE</span><strong class="shopper-creator-code" data-label="Creator ID">1002</strong><span data-label="Status">Active</span><span data-label="Days Left">30 Days</span></article>`
    })
    const selected = page.locator('.creator-tabs button.active,.shopper nav button.active,.admin-sidebar a[aria-current="page"]')
    for (let index = 0; index < await selected.count(); index++) {
      const colors = await selected.nth(index).evaluate(element => ({ color: getComputedStyle(element).color, background: getComputedStyle(element).backgroundColor }))
      assert.ok(contrast(colors.color, colors.background) >= 4.5, `${name} selected contrast failed: ${JSON.stringify(colors)}`)
    }
    const hover = page.getByRole('button', { name: 'Wallet' })
    await hover.hover()
    await page.waitForTimeout(200)
    const hoverColors = await hover.evaluate(element => ({ color: getComputedStyle(element).color, background: getComputedStyle(element).backgroundColor }))
    assert.ok(contrast(hoverColors.color, hoverColors.background) >= 4.5, `${name} hover contrast failed`)
    await hover.focus()
    assert.notEqual(await hover.evaluate(element => getComputedStyle(element).outlineStyle), 'none')
    assert.ok(await page.getByText('1002', { exact: true }).isVisible())
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    const wide = await page.evaluate(() => [...document.querySelectorAll('*')].filter(element => element.scrollWidth > element.clientWidth + 1).map(element => `${element.tagName}.${element.className}:${element.clientWidth}/${element.scrollWidth}`).slice(0, 10))
    assert.ok(overflow <= 1, `${name} has ${overflow}px horizontal overflow (${wide.join(', ')})`)
    await context.close()
    console.log(`${name}: selected, hover, focus, Creator ID, and responsive layout passed`)
  }
} finally {
  await browser.close()
}
