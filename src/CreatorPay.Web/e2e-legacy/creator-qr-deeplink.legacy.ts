import{expect,test}from'./fixtures'
import type{Page}from'@playwright/test'

const password=process.env.E2E_SHOPPER_PASSWORD!,cashier=process.env.E2E_CASHIER_PHONE!,shopper=process.env.E2E_SHOPPER_PHONE!,creator=process.env.E2E_CREATOR_PHONE!,owner=process.env.E2E_OWNER_PHONE!,admin=process.env.E2E_ADMIN_EMAIL!,payload=process.env.E2E_CREATOR_QR_PAYLOAD!,missingCampaign=process.env.E2E_NO_CAMPAIGN_QR_PAYLOAD!
test.describe.configure({timeout:120_000})

async function submitLogin(page:Page,identity:string){await page.getByLabel('Phone Number').fill(identity);await page.getByRole('textbox',{name:'Password'}).fill(password);const response=page.waitForResponse(value=>value.url().includes('/api/v1/auth/login')&&value.request().method()==='POST');await page.locator('form').getByRole('button',{name:'Sign In'}).click();expect((await response).ok()).toBeTruthy()}
async function login(page:Page,identity:string){await page.goto('/');await submitLogin(page,identity);await page.waitForFunction(()=>location.pathname!=='/')}
async function clear(page:Page){await page.goto('/help');await page.evaluate(()=>localStorage.clear());await page.goto('/')}
async function eligible(page:Page){const panel=page.getByLabel('Eligible Creator');await expect(panel).toContainText('Creator: Selam Active');await expect(panel).toContainText('Business: Active E2E Business');await expect(panel).toContainText('Status: Eligible');await expect(page.getByLabel('Customer Phone Number')).toBeVisible();await expect(page.getByLabel('Purchase Amount (ETB)')).toBeVisible()}

test('native-camera deep link survives Cashier sign-in and opens checkout',async({page})=>{await page.goto(payload);await expect(page.getByRole('heading',{name:'Welcome to Weymela'})).toBeVisible();expect(new URL(page.url()).pathname).toBe('/');const validation=page.waitForResponse(value=>value.url().includes('/cashier/checkouts/validate-offer'));await submitLogin(page,cashier);expect((await validation).ok()).toBeTruthy();await expect(page).toHaveURL(/\/cashier$/);await eligible(page)})

test('authenticated Cashier deep link opens checkout directly',async({page})=>{await login(page,cashier);const validation=page.waitForResponse(value=>value.url().includes('/cashier/checkouts/validate-offer'));await page.goto(payload);expect((await validation).ok()).toBeTruthy();await expect(page).toHaveURL(/\/cashier$/);await eligible(page)})

test('non-Cashier roles never receive checkout controls',async({page})=>{for(const identity of[shopper,creator,owner,admin]){await clear(page);await login(page,identity);await page.goto(payload);await expect(page.getByRole('heading',{name:'Creator QR'})).toBeVisible();await expect(page.getByText('Cashier checkout requires an active Cashier account.')).toBeVisible();await expect(page.getByLabel('Customer Phone Number')).toHaveCount(0)}})

test('tampered and campaign-less permanent QRs are rejected safely',async({page})=>{await login(page,cashier);const url=new URL(payload),token=url.searchParams.get('t')!;url.searchParams.set('t',`${token.slice(0,-1)}${token.endsWith('A')?'B':'A'}`);for(const invalid of[url.toString(),missingCampaign]){const validation=page.waitForResponse(value=>value.url().includes('/cashier/checkouts/validate-offer'));await page.goto(invalid);expect((await validation).ok()).toBeTruthy();await expect(page.getByText('This Creator promotion is not currently eligible at this Business.')).toBeVisible();await expect(page.getByLabel('Customer Phone Number')).toHaveCount(0)}})

test('Pixel-sized deep-link checkout has no horizontal overflow',async({page},info)=>{test.skip(info.project.name!=='mobile');await login(page,cashier);const validation=page.waitForResponse(value=>value.url().includes('/cashier/checkouts/validate-offer'));await page.goto(payload);await validation;await eligible(page);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy()})
