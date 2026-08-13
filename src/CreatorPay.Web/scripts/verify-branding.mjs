import { chromium, devices } from 'playwright'
import assert from 'node:assert/strict'

const baseUrl = process.env.WEB_BASE_URL ?? 'http://127.0.0.1:8080'
const browser = await chromium.launch({ headless: true })
try {
  for (const [name, options] of [
    ['desktop Chromium', { viewport: { width: 1440, height: 900 } }],
    ['Pixel 5', devices['Pixel 5']],
  ]) {
    const context = await browser.newContext(options)
    const page = await context.newPage()
    await page.goto(baseUrl, { waitUntil: 'networkidle' })
    assert.match(await page.title(), /^Weymela/)
    assert.equal(await page.locator('link[rel="icon"][href="/favicon.ico"]').count(), 1)
    assert.equal(await page.locator('link[rel="apple-touch-icon"][href="/icons/weymela-180x180.png"]').count(), 1)
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    assert.ok(overflow <= 1, `${name} has ${overflow}px horizontal overflow`)
    await context.close()
    console.log(`${name}: title, WM favicon links, and responsive layout passed`)
  }
  const response = await fetch(`${baseUrl}/manifest.webmanifest`)
  assert.ok(response.ok)
  const manifest = await response.json()
  assert.deepEqual(manifest.icons.map(icon => [icon.src, icon.sizes, icon.purpose]), [
    ['/icons/weymela-192x192.png', '192x192', 'any'],
    ['/icons/weymela-512x512.png', '512x512', 'any'],
  ])
  for (const asset of ['/favicon.ico', ...manifest.icons.map(icon => icon.src)]) {
    const assetResponse = await fetch(`${baseUrl}${asset}`)
    assert.ok(assetResponse.ok, `${asset} returned ${assetResponse.status}`)
    assert.ok(Number(assetResponse.headers.get('content-length')) > 0, `${asset} was empty`)
  }
  console.log('Manifest and served favicon/PWA icon assets passed')
} finally {
  await browser.close()
}
