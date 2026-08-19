import {expect,test} from '@playwright/test'

const api=process.env.E2E_API_URL!,password=process.env.E2E_SHOPPER_PASSWORD!,oldPin='12345',newPin='54321'
test.setTimeout(240_000)
async function openSignIn(page:any){const button=page.getByRole('button',{name:'Already have an account? Sign In'});if(await button.isVisible())await button.click()}

async function pin(page:any,value:string){await page.getByLabel('5-digit PIN',{exact:true}).fill(value);await page.locator('form').getByRole('button',{name:'Sign In',exact:true}).click()}
async function recover(page:any,request:any,phone:string){
  const seeded=await request.post(`${api}/api/v1/e2e/locked-pin-user`,{data:{phoneNumber:phone,password,pin:oldPin}});expect(seeded.status()).toBe(200)
  await page.goto('/');await page.evaluate(value=>localStorage.setItem('weymela_trusted_phone',value),phone);await page.reload()
  await page.getByRole('button',{name:'Forgot PIN'}).click();await expect(page.getByRole('heading',{name:'Reset 5-digit PIN'})).toBeVisible();await page.getByLabel('Password',{exact:true}).fill(password)
  await page.getByLabel('New PIN',{exact:true}).fill(newPin);await page.getByLabel('Confirm PIN',{exact:true}).fill(newPin);await page.getByRole('button',{name:'Reset PIN'}).click();await expect(page.getByText('PIN reset. Sign in with your new PIN.')).toBeVisible();await page.getByRole('button',{name:'Back'}).click()
}

test('password-based PIN recovery rejects the old PIN',async({page,request},testInfo)=>{
  const phone=testInfo.project.name==='mobile'?'+251977210002':'+251977210001';await recover(page,request,phone);await pin(page,oldPin);await expect(page.getByRole('alert')).toContainText('Invalid phone number or PIN.')
})

test('locked user recovers with account password and signs in with the new PIN',async({page,request},testInfo)=>{
  const phone=testInfo.project.name==='mobile'?'+251977200002':'+251977200001'
  const seeded=await request.post(`${api}/api/v1/e2e/locked-pin-user`,{data:{phoneNumber:phone,password,pin:oldPin}})
  expect(seeded.status()).toBe(200)
  await page.goto('/')
  await page.evaluate(value=>localStorage.setItem('weymela_trusted_phone',value),phone)
  await page.reload()
  await expect(page.getByRole('heading',{name:'Enter your 5-digit PIN'})).toBeVisible()
  await page.getByRole('button',{name:'Forgot PIN'}).click()
  await expect(page.getByRole('heading',{name:'Reset 5-digit PIN'})).toBeVisible()
  await page.getByLabel('Password',{exact:true}).fill(password)
  await page.getByLabel('New PIN',{exact:true}).fill(newPin);await page.getByLabel('Confirm PIN',{exact:true}).fill(newPin)
  await page.getByRole('button',{name:'Reset PIN'}).click()
  await expect(page.getByText('PIN reset. Sign in with your new PIN.')).toBeVisible()
  await page.getByRole('button',{name:'Back'}).click()
  await pin(page,newPin)
  await expect(page).toHaveURL(/\/shopper/)
})

test('PlatformAdmin keeps full credential login and no PIN enrollment',async({page})=>{
  await page.goto('/')
  await openSignIn(page)
  await page.getByLabel('Phone Number').fill(process.env.E2E_ADMIN_EMAIL!)
  await page.getByRole('textbox',{name:'Password'}).fill(password)
  await page.locator('form').getByRole('button',{name:'Sign In',exact:true}).click()
  await expect(page).toHaveURL(/\/admin/)
  await expect(page.getByRole('heading',{name:'Dashboard'})).toBeVisible()
  await expect(page.getByRole('heading',{name:/5-digit PIN/i})).toHaveCount(0)
  await expect.poll(()=>page.evaluate(()=>localStorage.getItem('weymela_trusted_phone'))).toBeNull()
})
