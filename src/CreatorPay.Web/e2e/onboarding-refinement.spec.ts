import { expect, test, type BrowserContext } from '@playwright/test'

type OnboardingRole = 'Creator' | 'MerchantAdmin'

async function installOnboarding(context: BrowserContext, role: OnboardingRole) {
  const mutations: { path: string; body: unknown }[] = []
  let submitted = false
  await context.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/integration/v3/session') {
      await route.fulfill({ json: {
        role, status: submitted ? 'PendingApproval' : 'Onboarding', isOnboarding: !submitted,
        destination: submitted ? (role === 'Creator' ? '/creator' : '/business') : null,
        accountEmail: 'owner@weymela.test', accountPhone: '+251911111111',
      } })
      return
    }
    if (path === `/api/v1/integration/v3/onboarding/${role === 'Creator' ? 'creator' : 'business'}`) {
      mutations.push({ path, body: JSON.parse(request.postData() ?? '{}') })
      submitted = true
      await route.fulfill({ json: { destination: role === 'Creator' ? '/creator' : '/business' } })
      return
    }
    if (path === '/api/v1/creators/me') {
      await route.fulfill({ json: { displayName: 'Mimi Creates', publicCreatorId: 'CR-MIMI', creatorCode: '1234',
        creatorStatus: 'PendingApproval', accountStatus: 'PendingApproval', effectiveStatus: 'Inactive',
        effectiveStatusReason: 'Pending approval', email: '', phoneNumber: '', city: 'Addis Ababa',
        socialProfiles: [] } })
      return
    }
    if (path === '/api/v1/merchants/me') {
      await route.fulfill({ json: { tradingName: 'Mimi Cafe', merchantStatus: 'PendingReview',
        accountStatus: 'PendingApproval', effectiveStatus: 'Inactive', effectiveStatusReason: 'Pending review' } })
      return
    }
    await route.fulfill({ json: {} })
  })
  return mutations
}

test('Creator onboarding keeps V3 identity read-only, submits multiple socials, and lands directly', async ({ context, page }) => {
  const mutations = await installOnboarding(context, 'Creator')
  await page.goto('/onboarding/creator')
  await expect(page.getByLabel('Account email')).toHaveValue('owner@weymela.test')
  await expect(page.getByLabel('Account phone')).toHaveValue('+251911111111')
  await expect(page.getByLabel('Account email')).toHaveAttribute('readonly', '')
  await expect(page.getByLabel('Account phone')).toHaveAttribute('readonly', '')
  await page.getByLabel('Legal First Name').fill('Mimi')
  await page.getByLabel("Father's / Last Name").fill('Abebe')
  await page.getByLabel('Public Display Name').fill('Mimi Creates')
  const first = page.locator('.social-profile-row').first()
  await first.getByLabel('Profile URL').fill('https://www.tiktok.com/@mimi')
  await first.getByLabel('Follower count').fill('1200')
  await page.getByRole('button', { name: 'Add social platform' }).click()
  const second = page.locator('.social-profile-row').nth(1)
  await expect(second.getByLabel('Platform')).toHaveValue('Instagram')
  await second.getByLabel('Profile URL').fill('https://instagram.com/mimi')
  await second.getByLabel('Follower count').fill('900')
  await page.getByRole('button', { name: 'Continue' }).click()
  await expect(page).toHaveURL(/\/creator$/)
  expect(mutations).toHaveLength(1)
  expect(mutations[0].body).toMatchObject({ socialProfiles: [
    { platform: 'TikTok', audienceCount: 1200 },
    { platform: 'Instagram', audienceCount: 900 },
  ] })
  expect(JSON.stringify(mutations[0].body)).not.toMatch(/owner@weymela|251911111111|password|pin/i)
})

for (const [role, path] of [['Creator', '/onboarding/creator'], ['MerchantAdmin', '/onboarding/business']] as const) {
  test(`${role} Back to profiles is normal navigation with no mutation`, async ({ context, page }) => {
    const mutations = await installOnboarding(context, role)
    await context.route('**/onboarding', route => route.fulfill({ contentType: 'text/html',
      body: '<!doctype html><html><body><main><h1>Choose a profile</h1></main></body></html>' }))
    await page.goto(path)
    await page.getByRole('link', { name: 'Back to profiles' }).click()
    await expect(page).toHaveURL(/\/onboarding$/)
    await expect(page.getByRole('heading', { name: 'Choose a profile' })).toBeVisible()
    expect(mutations).toEqual([])
  })
}

test('Business onboarding separates optional public contacts from read-only account identity', async ({ context, page }) => {
  await installOnboarding(context, 'MerchantAdmin')
  await page.goto('/onboarding/business')
  await expect(page.getByLabel('Account email')).toHaveAttribute('readonly', '')
  await expect(page.getByLabel('Account phone')).toHaveAttribute('readonly', '')
  await expect(page.getByLabel('Business Contact Phone')).toBeEditable()
  await expect(page.getByLabel('Business Contact Email')).toBeEditable()
  await expect(page.getByLabel('Business Contact Phone')).not.toHaveAttribute('required', '')
  await expect(page.getByLabel('Business Contact Email')).not.toHaveAttribute('required', '')
})

test('active Creator can add, update, and remove socials but cannot remove the final profile', async ({ context, page }) => {
  let socialProfiles: Record<string, unknown>[] = [{ platform: 'TikTok', profileUrl: 'https://www.tiktok.com/@mimi',
    followerCount: 1200, verificationStatus: 'Unverified', isPrimary: true }]
  let saved: unknown
  await context.route('**/api/v1/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname
    if (path === '/api/v1/integration/v3/session') {
      await route.fulfill({ json: { role: 'Creator', status: 'Active', isOnboarding: false,
        destination: '/creator', accountEmail: 'owner@weymela.test', accountPhone: '+251911111111' } })
      return
    }
    if (path === '/api/v1/creators/me') {
      await route.fulfill({ json: { displayName: 'Mimi Creates', publicCreatorId: 'CR-MIMI', creatorCode: '1234',
        creatorStatus: 'Active', accountStatus: 'Active', effectiveStatus: 'Active', effectiveStatusReason: '',
        email: 'legacy-role-contact@example.test', phoneNumber: '0911223344',
        city: 'Addis Ababa', socialProfiles } })
      return
    }
    if (path === '/api/v1/integration/v3/creator/social-profiles') {
      saved = JSON.parse(request.postData() ?? '{}')
      socialProfiles = (saved as { socialProfiles: Record<string, unknown>[] }).socialProfiles.map((row, index) => ({
        ...row, followerCount: (row as unknown as { audienceCount: number }).audienceCount,
        verificationStatus: 'Unverified', isPrimary: index === 0,
      }))
      await route.fulfill({ json: socialProfiles })
      return
    }
    if (path.endsWith('/summary')) await route.fulfill({ json: {} })
    else await route.fulfill({ json: [] })
  })
  await page.goto('/creator')
  await page.getByRole('navigation', { name: 'Creator sections' }).getByRole('button', { name: 'Profile', exact: true }).click()
  await expect(page.getByText('Weymela account email')).toBeVisible()
  await expect(page.getByText('owner@weymela.test')).toBeVisible()
  await expect(page.getByText('+251911111111')).toBeVisible()
  await expect(page.getByText('legacy-role-contact@example.test')).toHaveCount(0)
  await expect(page.getByText('0911223344')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Remove TikTok' })).toBeDisabled()
  await page.getByRole('button', { name: 'Add social platform' }).click()
  const rows = page.locator('.creator-social-editor .social-profile-row')
  await rows.nth(0).getByLabel('Follower count').fill('1300')
  await rows.nth(1).getByLabel('Profile URL').fill('https://instagram.com/mimi')
  await rows.nth(1).getByLabel('Follower count').fill('800')
  await page.getByRole('button', { name: 'Remove TikTok' }).click()
  await expect(page.getByRole('button', { name: 'Remove Instagram' })).toBeDisabled()
  await page.getByRole('button', { name: 'Save social profiles' }).click()
  await expect(page.getByText('Social profiles updated.')).toBeVisible()
  expect(saved).toEqual({ socialProfiles: [{ platform: 'Instagram',
    profileUrl: 'https://instagram.com/mimi', audienceCount: 800 }] })
})
