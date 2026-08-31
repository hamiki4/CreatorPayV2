import {expect,test} from './fixtures'
import type {Page,APIRequestContext} from '@playwright/test'

const api=process.env.E2E_API_URL!,password=process.env.E2E_SHOPPER_PASSWORD!
const ownerPhone=process.env.E2E_OWNER_PHONE!,creatorPhone=process.env.E2E_CREATOR_PHONE!,creatorCode=process.env.E2E_CREATOR_CODE!
const phones=JSON.parse(process.env.E2E_CONFIRMATION_PHONES!) as Record<string,string>
test.describe.configure({timeout:180_000})

async function login(page:Page,phone:string){
  await page.goto('/');await page.evaluate(()=>localStorage.clear());await page.goto('/');const signIn=page.getByRole('button',{name:'Already have an account? Sign In'});if(await signIn.isVisible())await signIn.click()
  const pinUnlock=page.getByRole('heading',{name:'Enter your 5-digit PIN'})
  await expect(pinUnlock.or(page.getByLabel('Phone Number'))).toBeVisible()
  if(await pinUnlock.isVisible()){await page.getByRole('textbox',{name:'5-digit PIN',exact:true}).fill('12345');await page.locator('form').getByRole('button',{name:'Sign In'}).click();await expect(page.locator('main.app')).toBeVisible();return}
  await page.getByLabel('Phone Number').fill(phone);await page.getByRole('textbox',{name:'Password'}).fill(password);const pending=page.waitForResponse(r=>r.url().includes('/api/v1/auth/login')&&r.request().method()==='POST');await page.locator('form').getByRole('button',{name:'Sign In'}).click();expect((await pending).ok()).toBeTruthy();await page.waitForFunction(()=>location.pathname!=='/')
  const setup=page.getByRole('heading',{name:/^(Complete your secure setup|Create 5-digit PIN)$/})
  const workspace=page.locator('main.app')
  await expect(setup.or(workspace)).toBeVisible()
  if(await workspace.isVisible())return
  const pinInputs=page.locator('label.pin-entry input');await expect(pinInputs).toHaveCount(2);await pinInputs.nth(0).fill('12345');await pinInputs.nth(1).fill('12345');await page.getByRole('button',{name:'Create PIN'}).click()
  await expect(workspace).toBeVisible()
}
async function logout(page:Page){await page.evaluate(()=>localStorage.clear());await page.goto('/')}

async function cashierCreates(page:Page,key:string,amount:string){
  const phone=`09117700${key.includes('yes')?'1':'2'}${key.includes('mobile')?'2':'1'}`
  await login(page,creatorPhone);await page.getByRole('button',{name:'Settings'}).click();await page.getByRole('menuitem',{name:'Profile'}).click();await expect(page.getByRole('heading',{name:'Profile'})).toBeVisible();await expect(page.getByText(creatorCode,{exact:true})).toBeVisible();await expect(page.locator('.creator-id-profile')).toBeVisible();await expect(page.getByRole('button',{name:'Copy Creator ID'})).toBeVisible();await logout(page)
  await login(page,ownerPhone);await page.getByRole('button',{name:'Settings'}).click();await page.getByRole('menuitem',{name:'Cashier Management'}).click();await page.getByRole('button',{name:'Create Cashier'}).click();await page.getByLabel('Cashier Name').fill('Direct E2E Cashier');await page.getByLabel('Phone Number').fill(phone);await page.getByLabel('Temporary Password',{exact:true}).fill(password);await page.getByLabel('Confirm Temporary Password').fill(password);await page.getByRole('button',{name:'Create Cashier'}).click();await expect(page.getByText('Cashier account created.')).toBeVisible();await logout(page)
  await login(page,phone);await expect(page.getByRole('heading',{name:'Cashier',exact:true})).toBeVisible();await expect(page.getByRole('heading',{name:'Cashier Dashboard'})).toHaveCount(0);await expect(page.getByRole('button',{name:'New Purchase'})).toBeVisible();await expect(page.getByLabel('Creator ID')).toHaveValue('');await expect(page.getByLabel('Creator ID')).toHaveAttribute('placeholder','Creator ID');await expect(page.getByLabel('Customer Phone Number')).toHaveValue('');await expect(page.getByLabel('Customer Phone Number')).toHaveAttribute('placeholder','Customer Phone Number');await expect(page.getByLabel('Purchase Amount')).toHaveValue('');await expect(page.getByLabel('Purchase Amount')).toHaveAttribute('placeholder','Purchase Amount');await page.getByLabel('Creator ID').fill(creatorCode);await page.getByLabel('Customer Phone Number').fill(phones[key]);await page.getByLabel('Purchase Amount').fill(amount);page.once('dialog',dialog=>dialog.accept());const validation=page.waitForResponse(r=>r.url().includes('/api/v1/cashier/checkouts/validate-creator')&&r.request().method()==='POST');const response=page.waitForResponse(r=>r.url().includes('/api/v1/cashier/checkouts/by-creator')&&r.request().method()==='POST');await page.getByRole('button',{name:'Submit Purchase'}).click();expect((await validation).ok()).toBeTruthy();const body=await (await response).json();expect(body.code).toBe('awaiting_shopper_confirmation');await expect(page.getByText('Purchase submitted — awaiting Shopper confirmation.',{exact:true})).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();return body.checkout.id as string
}
async function repeatDecision(request:APIRequestContext,page:Page,id:string){const token=await page.evaluate(()=>localStorage.getItem('creatorpay_access_token'));return request.post(`${api}/api/v1/customer/checkouts/${id}/approve`,{headers:{Authorization:`Bearer ${token}`,'Idempotency-Key':crypto.randomUUID()},data:{}})}

test('Cashier Creator ID purchase waits for Shopper YES and confirms once',async({page,request},testInfo)=>{
  const device=testInfo.project.name==='mobile'?'mobile':'desktop',key=`yes-${device}`,amount=device==='mobile'?'511':'510',id=await cashierCreates(page,key,amount);await logout(page);await login(page,phones[key]);await page.getByRole('button',{name:/Purchase confirmations/}).click()
  const pending=page.locator('.pending-confirmations article').filter({hasText:'Active E2E Business'});await expect(pending).toContainText(`${amount}.00`);await expect(pending).not.toContainText('ETB');await expect(pending).toContainText('Is this your purchase?');await pending.getByRole('button',{name:"YES, IT'S ME",exact:true}).click();await expect(page.getByText('Purchase confirmed.')).toBeVisible();await expect(pending).toHaveCount(0)
  const history=page.locator('article.confirmation-row').filter({hasText:`${amount}.00`});await expect(history).toContainText('Completed');await expect(history).not.toContainText('ETB');await expect(history.getByRole('button')).toHaveCount(0);expect((await repeatDecision(request,page,id)).ok()).toBeTruthy();await expect(history).toContainText('Completed')
  await logout(page);const cashierPhone=`091177001${device==='mobile'?'2':'1'}`;await login(page,cashierPhone);await page.getByRole('button',{name:'Recent Transactions'}).click();const headings=page.locator('.cashier-transaction-row.headings');for(const label of ['Amount','Status','Creator','Creator ID','Date','Time','Reference'])await expect(headings).toContainText(label);const transaction=page.locator('.cashier-transaction-row').nth(1);await expect(transaction).toContainText('Active E2E Creator');await expect(transaction).toContainText(creatorCode);await expect(transaction).toContainText(/#[A-Z0-9]{4}/);await expect(transaction).not.toContainText(/CP-|[0-9a-f]{8}-[0-9a-f]{4}/i);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy()
})

test('Cashier Creator ID purchase waits for Shopper NO with no settlement',async({page},testInfo)=>{
  const device=testInfo.project.name==='mobile'?'mobile':'desktop',key=`no-${device}`,amount=device==='mobile'?'521':'520';await cashierCreates(page,key,amount);await logout(page);await login(page,phones[key]);await page.getByRole('button',{name:/Purchase confirmations/}).click()
  const pending=page.locator('.pending-confirmations article').filter({hasText:'Active E2E Business'});await expect(pending).toContainText(`${amount}.00`);await expect(pending).not.toContainText('ETB');await pending.getByRole('button',{name:"NO, IT'S NOT ME",exact:true}).click();await expect(page.getByText('Purchase rejected.')).toBeVisible();await expect(pending).toHaveCount(0)
  const history=page.locator('article.confirmation-row').filter({hasText:`${amount}.00`});await expect(history).toContainText('Rejected');await expect(history).not.toContainText('ETB');await expect(history.getByRole('button')).toHaveCount(0);await expect(page.getByText('Purchase confirmed.')).toHaveCount(0)
})
