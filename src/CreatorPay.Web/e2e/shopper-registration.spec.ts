import {expect,test} from './fixtures'

test.describe.configure({timeout:120_000})

test('Shopper password confirmation uses submitted values and sends one request',async({page},testInfo)=>{
  const mobile=testInfo.project.name==='mobile',phone=mobile?'0911779012':'0911779011',requests:string[]=[]
  page.on('request',request=>{if(request.url().includes('/api/v1/customers/register')&&request.method()==='POST')requests.push(request.postData()??'')})
  await page.goto('/');await page.getByRole('button',{name:'Shopper Registration'}).click();await page.getByLabel('Display Name').fill('Password Test Shopper');await page.getByLabel('Phone').fill(phone)

  const form=page.locator('form');await page.getByLabel('Password',{exact:true}).fill('Test@1234');await form.evaluate((node:HTMLFormElement)=>node.requestSubmit());expect(requests).toHaveLength(0)

  await page.getByLabel('Confirm Password').fill('weakpass');await page.getByLabel('Password',{exact:true}).fill('weakpass');await form.evaluate((node:HTMLFormElement)=>node.requestSubmit());await expect(page.getByRole('status')).toContainText('Use at least 8 characters with uppercase, lowercase, a number, and a special character.');expect(requests).toHaveLength(0)

  await page.getByLabel('Password',{exact:true}).fill('Test@1234');await page.getByLabel('Confirm Password').fill('Test@1235');await form.evaluate((node:HTMLFormElement)=>node.requestSubmit());await expect(page.getByRole('status')).toContainText('Passwords do not match.');expect(requests).toHaveLength(0)

  await page.locator('#shopper-password').evaluate((input:HTMLInputElement)=>input.value='Test@1234');await page.locator('#shopper-confirm-password').evaluate((input:HTMLInputElement)=>input.value='Test@1234')
  const response=page.waitForResponse(value=>value.url().includes('/api/v1/customers/register')&&value.request().method()==='POST');await form.evaluate((node:HTMLFormElement)=>{node.requestSubmit();node.requestSubmit()});expect((await response).ok()).toBeTruthy();await expect(page.getByRole('status')).toContainText('Shopper account created. Verify your phone to continue.');await expect(page.getByRole('heading',{name:'Verify your phone'})).toBeVisible();expect(requests).toHaveLength(1);const body=JSON.parse(requests[0]);expect(body.email).toBe('');expect(body.password).toBe('Test@1234');expect(body.confirmation).toBe('Test@1234')
})
