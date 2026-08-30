import {mkdir} from 'node:fs/promises'
import path from 'node:path'
import {expect,test} from './fixtures'
import type {APIRequestContext,Page} from '@playwright/test'

const api=process.env.E2E_API_URL!
const password=process.env.E2E_ADMIN_PASSWORD!
const output=process.env.E2E_SCREENSHOT_DIR ?? path.resolve(process.cwd(),'../../.artifacts/admin-refinement')

const platformTabs=['Dashboard','Reports','Creator Review','Business Review','Admin Accounts','Password Reset Requests','Business Accounts','Creator Accounts','Customer Accounts','Cashier Accounts','Commission','Deposits','Wallets','Payouts','Fraud','System']
const operationsTabs=['Creator Review','Business Review','Password Reset Requests','Business Accounts','Creator Accounts','Customer Accounts','Cashier Accounts','Deposits','Wallets','Payouts','Fraud']
const sizes=[
  ['375',{width:375,height:812}],
  ['390',{width:390,height:844}],
  ['393',{width:393,height:852}],
  ['430',{width:430,height:932}],
  ['tablet',{width:768,height:1024}],
  ['desktop',{width:1440,height:1000}],
] as const

async function loginAdmin(page:Page,email:string){
  await page.goto('/help',{waitUntil:'domcontentloaded'})
  await page.evaluate(()=>{localStorage.clear();sessionStorage.clear()})
  await page.goto('/',{waitUntil:'domcontentloaded'})
  const welcome=page.getByRole('button',{name:'Already have an account? Sign In',exact:true})
  const identity=page.getByLabel('Phone Number')
  await expect(identity.or(welcome)).toBeVisible()
  if(!(await identity.isVisible()))await welcome.click()
  await identity.fill(email)
  await page.getByRole('textbox',{name:'Password',exact:true}).fill(password)
  const response=page.waitForResponse(item=>item.request().method()==='POST'&&new URL(item.url()).pathname==='/api/v1/auth/login')
  await page.locator('form').getByRole('button',{name:'Sign In',exact:true}).click()
  expect((await response).ok()).toBeTruthy()
  await expect(page.locator('.admin-shell')).toBeVisible()
}

async function createOperationsAdmin(request:APIRequestContext){
  const login=await request.post(`${api}/api/v1/auth/login`,{data:{email:process.env.E2E_ADMIN_EMAIL,password}})
  expect(login.ok(),await login.text()).toBeTruthy()
  const token=(await login.json()).accessToken as string
  const email='operations-responsive@e2e.invalid'
  const created=await request.post(`${api}/api/v1/admin/accounts/create`,{
    headers:{Authorization:`Bearer ${token}`},
    data:{role:'OperationsAdmin',email,password,confirmation:password},
  })
  expect([201,409]).toContain(created.status())
  return email
}

async function expectNoOverflow(page:Page){
  expect(await page.evaluate(()=>({
    document:document.documentElement.scrollWidth<=document.documentElement.clientWidth,
    body:document.body.scrollWidth<=document.body.clientWidth,
  }))).toEqual({document:true,body:true})
}

async function captureWidths(page:Page,role:'platform'|'operations'){
  await mkdir(output,{recursive:true})
  for(const [name,viewport] of sizes){
    await page.setViewportSize(viewport)
    await expect(page.locator('.admin-shell')).toBeVisible()
    await expectNoOverflow(page)
    if(viewport.width<=768){
      const toggle=page.getByRole('button',{name:'Open admin menu'})
      await expect(toggle).toBeVisible()
      await toggle.click()
      await expect(page.getByRole('navigation',{name:'Admin navigation'})).toBeVisible()
      await expect(page.locator('.admin-sidebar')).toHaveClass(/is-open/)
      if(name==='375')await page.screenshot({path:path.join(output,`${role}-${name}-menu.png`),fullPage:true})
      await page.locator('.admin-menu-close').click()
      await expect(page.locator('.admin-sidebar')).not.toHaveClass(/is-open/)
      await expect.poll(async()=>{
        const box=await page.locator('.admin-sidebar').boundingBox()
        return Math.round((box?.x ?? 0)+(box?.width ?? 0))
      }).toBeLessThanOrEqual(0)
    }else{
      await expect(page.locator('.admin-sidebar')).toBeVisible()
    }
    await page.screenshot({path:path.join(output,`${role}-${name}.png`),fullPage:true})
  }
}

test.describe.serial('responsive admin workspace',()=>{
  test('Platform Admin routes, empty forms, menu, cards, and responsive screenshots',async({page})=>{
    test.setTimeout(180_000)
    await page.setViewportSize({width:1440,height:1000})
    await loginAdmin(page,'admin@e2e.invalid')
    await expect(page).toHaveURL(/\/admin(?:\/dashboard)?$/)
    await expect(page.getByRole('button',{name:'Notifications'})).toBeVisible()

    const shortcuts=[
      ['Pending Creator Approvals','/admin/creator-review'],
      ['Pending Merchant Approvals','/admin/business-review'],
      ['Pending Deposits','/admin/deposits'],
      ['Active Creators','/admin/creator-accounts?status=Active'],
      ['Active Businesses','/admin/business-accounts?status=Active'],
      ['Pending Payouts','/admin/payouts'],
      ['Open Fraud Alerts','/admin/fraud'],
      ['Failed Checkouts','/admin/fraud'],
      ['System Health','/admin/system'],
    ] as const
    for(const [label,href] of shortcuts){
      const card=page.locator('.admin-metric-card').filter({hasText:label})
      await expect(card).toHaveAttribute('href',href)
      await card.evaluate(element=>element.setAttribute('target','_blank'))
      const opened=page.waitForEvent('popup')
      await card.click()
      const destination=await opened
      await destination.waitForLoadState('domcontentloaded')
      await expect(destination).toHaveURL(new RegExp(href.replace(/[?]/g,'\\?')))
      if(href.includes('status=Active'))await expect(destination.getByLabel('Status')).toHaveValue('Active')
      await destination.close()
    }

    await captureWidths(page,'platform')
    await page.setViewportSize({width:375,height:812})
    await page.goto('/admin/creator-accounts')
    await expect(page.getByRole('heading',{name:'Creator Accounts'})).toBeVisible()
    const creatorForm=page.locator('.admin-create-card form')
    for(const input of await creatorForm.locator('input:not([type=hidden])').all())await expect(input).toHaveValue('')
    await expectNoOverflow(page)
    await page.goto('/admin/business-accounts')
    await expect(page.getByLabel('Business type')).toHaveValue('')
    for(const input of await page.locator('.admin-create-card input:not([type=hidden])').all())await expect(input).toHaveValue('')
    await expectNoOverflow(page)

    await page.setViewportSize({width:375,height:812})
    await page.getByRole('button',{name:'Open admin menu'}).click()
    for(const tab of platformTabs)await expect(page.getByRole('navigation',{name:'Admin navigation'}).getByText(tab,{exact:true})).toBeVisible()
  })

  test('Operations Admin keeps its narrower access and responsive authorized workspace',async({page,request})=>{
    test.setTimeout(180_000)
    const email=await createOperationsAdmin(request)
    await page.setViewportSize({width:375,height:812})
    await loginAdmin(page,email)
    await expect(page).toHaveURL(/\/admin\/creator-review/)
    await expect(page.getByRole('button',{name:'Notifications'})).toBeVisible()
    await page.getByRole('button',{name:'Open admin menu'}).click()
    const nav=page.getByRole('navigation',{name:'Admin navigation'})
    for(const tab of operationsTabs)await expect(nav.getByText(tab,{exact:true})).toBeVisible()
    for(const blocked of ['Dashboard','Reports','Admin Accounts','Commission','System'])await expect(nav.getByText(blocked,{exact:true})).toHaveCount(0)
    await page.locator('.admin-menu-close').click()
    await page.goto('/admin/dashboard')
    await expect(page).toHaveURL(/\/admin\/creator-review/)
    await expectNoOverflow(page)
    await captureWidths(page,'operations')
  })
})
