import {mkdir} from 'node:fs/promises'
import path from 'node:path'
import {expect,test} from './fixtures'
import type {APIRequestContext,Locator,Page} from '@playwright/test'

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
    data:{role:'OperationsAdmin',displayName:'Responsive Operations Admin',email,password,confirmation:password},
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

async function expectEditable(field:Locator,value:string){
  await field.click()
  await expect(field).toBeFocused()
  await field.fill(value)
  await expect(field).toHaveValue(value)
  await expect(field).toHaveCSS('pointer-events','auto')
  await expect(field).toHaveCSS('-webkit-user-select','text')
}

async function verifySharedCreateFields(page:Page,path:string){
  await page.goto(path)
  const form=page.locator('.admin-create-card form')
  await expect(form).toBeVisible()
  await expectEditable(form.getByLabel('Email'),'typing-check@example.com')
  await expectEditable(form.getByLabel('Phone'),'+251911000999')
  await expectEditable(form.getByLabel('Temporary password'),'E2e-test-password-1!')
  await expectEditable(form.getByLabel('Confirm password'),'E2e-test-password-1!')
  await expectNoOverflow(page)
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

    await page.goto('/admin/admin-accounts')
    const adminCreate=page.locator('.admin-create-card form')
    await expect(adminCreate.getByLabel('Role')).toHaveValue('PlatformAdmin')
    await expect(adminCreate.getByLabel('Role').locator('option')).toHaveText(['Platform Admin','Operations Admin'])
    await adminCreate.getByLabel('Role').selectOption('OperationsAdmin')
    await expect(adminCreate.getByLabel('Role')).toHaveValue('OperationsAdmin')
    await expectEditable(adminCreate.getByLabel('Full Name'),'Mobile Operations Admin')
    await expect(page.locator('.account-filters').getByLabel('Business')).toHaveCount(0)
    await expect(page.locator('.account-filters').getByLabel('Role')).toHaveValue('')

    for(const path of ['/admin/admin-accounts','/admin/creator-accounts','/admin/business-accounts','/admin/customer-accounts','/admin/cashier-accounts']){
      await verifySharedCreateFields(page,path)
    }
    await page.goto('/admin/creator-accounts')
    await expectEditable(page.locator('.admin-create-card').getByLabel('First name'),'Creator')
    await expectEditable(page.locator('.admin-create-card').getByLabel('Last name'),'Typing')
    await expectEditable(page.locator('.admin-create-card').getByLabel('Display name'),'Creator Typing')
    await page.goto('/admin/customer-accounts')
    await expectEditable(page.locator('.admin-create-card').getByLabel('Display name'),'Customer Typing')
    await page.goto('/admin/cashier-accounts')
    await expectEditable(page.locator('.admin-create-card').getByLabel('First name'),'Cashier')
    await expectEditable(page.locator('.admin-create-card').getByLabel('Last name'),'Typing')
    await page.locator('.admin-create-card').getByLabel('Business').selectOption({index:0})
    await page.goto('/admin/business-accounts')
    await expectEditable(page.locator('.admin-create-card').getByLabel('Legal business name'),'Typing Business PLC')
    await page.locator('.admin-create-card').getByLabel('Business type').selectOption('Professional Services')

    for(const [name,viewport] of sizes){
      await page.setViewportSize(viewport)
      await page.goto('/admin/admin-accounts')
      const card=page.locator('.admin-create-card')
      await expect(card.getByLabel('Role')).toBeVisible()
      await expect(card.getByLabel('Full Name')).toBeVisible()
      await expect(card.getByLabel('Email')).toBeVisible()
      await expectNoOverflow(page)
      const cardBox=await card.boundingBox()
      expect(cardBox).not.toBeNull()
      expect(cardBox!.x).toBeGreaterThanOrEqual(0)
      expect(cardBox!.x+cardBox!.width).toBeLessThanOrEqual(viewport.width)
      await page.screenshot({path:path.join(output,`platform-admin-account-form-${name}.png`),fullPage:true})
    }

    for(const [name,viewport] of sizes.filter(([,size])=>size.width<=430)){
      await page.setViewportSize(viewport)
      await page.goto('/admin/business-accounts')
      const card=page.locator('.admin-create-card')
      await expect(card).toBeVisible()
      const cardBox=await card.boundingBox()
      expect(cardBox).not.toBeNull()
      expect(cardBox!.x).toBeGreaterThanOrEqual(0)
      expect(cardBox!.x+cardBox!.width).toBeLessThanOrEqual(viewport.width)
      for(const field of await card.locator('input:not([type=hidden]),select,textarea,button').all()){
        const box=await field.boundingBox()
        if(!box)continue
        expect(box.x).toBeGreaterThanOrEqual(cardBox!.x)
        expect(box.x+box.width).toBeLessThanOrEqual(cardBox!.x+cardBox!.width+1)
      }
      await expectNoOverflow(page)
      await page.screenshot({path:path.join(output,`platform-business-account-form-${name}.png`),fullPage:true})
    }

    for(const [name,viewport] of sizes.filter(([,size])=>size.width<=430)){
      await page.setViewportSize(viewport)
      await page.goto('/admin/payouts')
      await page.getByRole('button',{name:'Customers',exact:true}).click()
      const filters=page.locator('.payout-filter-bar')
      await expect(filters.getByLabel('Search Customer Name')).toBeVisible()
      const fromField=filters.locator('label').filter({hasText:/^From/}).locator('input')
      const toField=filters.locator('label').filter({hasText:/^To/}).locator('input')
      const statusField=filters.locator('label').filter({hasText:/^Status/}).locator('select')
      await expect(fromField).toBeVisible();await expect(toField).toBeVisible();await expect(statusField).toBeVisible()
      const toBox=await toField.boundingBox()
      const statusBox=await statusField.boundingBox()
      expect(toBox).not.toBeNull();expect(statusBox).not.toBeNull()
      expect(toBox!.y+toBox!.height).toBeLessThanOrEqual(statusBox!.y+1)
      await expect(page.locator('.payout-cycle-summary article')).toHaveCount(6)
      await expectNoOverflow(page)
      await page.screenshot({path:path.join(output,`platform-customer-payout-${name}.png`),fullPage:true})
    }

    await page.setViewportSize({width:1440,height:1000})
    await page.goto('/admin/business-accounts')
    const desktopAction=page.locator('td[data-label="Action"] .actions button').first()
    if(await desktopAction.count())await expect(desktopAction).toHaveCSS('white-space','nowrap')

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
