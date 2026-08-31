import {expect, test} from './fixtures'

const screenshotDir = process.env.E2E_SCREENSHOT_DIR
const viewports = [
  {name: '375', width: 375, height: 812},
  {name: '390', width: 390, height: 844},
  {name: '393', width: 393, height: 852},
  {name: '430', width: 430, height: 932},
  {name: 'desktop', width: 1280, height: 800},
]

for (const viewport of viewports) {
  test(`approved password recovery survives return and reload at ${viewport.name}`, async ({page, browserName}) => {
    await page.setViewportSize(viewport)
    await page.addInitScript(() => {
      if (sessionStorage.getItem('password-reset-e2e-initialized') === '1') return
      sessionStorage.setItem('password-reset-e2e-initialized', '1')
      localStorage.setItem('weymela_welcome_seen', '1')
      localStorage.removeItem('weymela_password_reset_recovery')
      localStorage.removeItem('creatorpay_access_token')
      localStorage.removeItem('creatorpay_refresh_token')
    })

    let status = 'Pending'
    let resetCompletions = 0
    await page.route('**/api/v1/auth/**', async route => {
      const request = route.request()
      const url = new URL(request.url())
      const json = (value: unknown, responseStatus = 200) => route.fulfill({
        status: responseStatus,
        contentType: 'application/json',
        body: JSON.stringify(value),
      })
      if (url.pathname === '/api/v1/auth/password-reset-requests' && request.method() === 'POST') {
        return json({reference: 'PWR-1234567890ABCDEF1234567890AB', status: 'Pending', message: 'Password reset request submitted successfully. Your request is waiting for admin approval.'})
      }
      if (url.pathname.startsWith('/api/v1/auth/password-reset-requests/') && request.method() === 'GET') {
        return json({status, message: status === 'Approved' ? 'Your request was approved. Create a new password.' : 'Your password reset request is waiting for admin approval.'})
      }
      if (url.pathname === '/api/v1/auth/reset-password' && request.method() === 'POST') {
        resetCompletions += 1
        return json({succeeded: true})
      }
      return json({detail: 'Unexpected test request.'}, 404)
    })

    await page.goto('/', {waitUntil: 'domcontentloaded'})
    await page.getByRole('button', {name: 'Forgot Password'}).click()
    await page.getByLabel('Phone Number').fill('0911000001')
    await page.getByRole('button', {name: 'Request Password Reset'}).click()
    await expect(page.getByRole('status')).toContainText('Status: Pending')
    await expect.poll(() => page.evaluate(() => localStorage.getItem('weymela_password_reset_recovery'))).not.toBeNull()

    await page.getByRole('button', {name: 'Back to Sign In'}).click()
    status = 'Approved'
    await page.reload({waitUntil: 'domcontentloaded'})
    await page.getByRole('button', {name: 'Forgot Password'}).click()
    await expect(page.getByRole('heading', {name: 'Password Reset Approved'})).toBeVisible()
    await expect(page.getByRole('status')).toContainText('Status: Approved')
    await expect(page.getByRole('status')).toContainText('Your request was approved. Create a new password.')

    const newPassword = page.getByLabel('New Password')
    const confirmation = page.getByLabel('Confirm Password')
    for (const input of [newPassword, confirmation]) {
      const box = await input.boundingBox()
      expect(box).not.toBeNull()
      expect(box!.x).toBeGreaterThanOrEqual(0)
      expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width + .5)
      expect(box!.height).toBeGreaterThanOrEqual(44)
    }
    await newPassword.fill('Replacement-password-2!')
    await confirmation.fill('Replacement-password-2!')
    await page.getByRole('button', {name: 'Show password'}).first().click()
    await expect(newPassword).toHaveAttribute('type', 'text')
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
    if (screenshotDir) await page.screenshot({path: `${screenshotDir}/password-reset-${browserName}-${viewport.name}.png`, animations: 'disabled'})

    await page.getByRole('button', {name: 'Reset Password'}).click()
    await expect(page.getByRole('status')).toContainText('Password reset. You can sign in now.')
    expect(resetCompletions).toBe(1)
    expect(await page.evaluate(() => localStorage.getItem('weymela_password_reset_recovery'))).toBeNull()
  })
}
