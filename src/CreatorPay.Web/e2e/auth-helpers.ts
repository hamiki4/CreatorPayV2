import { expect } from './fixtures'
import type { Page } from '@playwright/test'

const password=process.env.E2E_SHOPPER_PASSWORD!

function phoneFor(identity:string){
  if(identity==='shopper@e2e.invalid')return process.env.E2E_SHOPPER_PHONE!
  if(identity==='creator@e2e.invalid')return process.env.E2E_CREATOR_PHONE!
  if(identity==='business-1@e2e.invalid')return process.env.E2E_OWNER_PHONE!
  if(identity==='owner@e2e.invalid')return process.env.E2E_OWNER_PHONE!
  if(identity==='cashier@e2e.invalid')return process.env.E2E_CASHIER_PHONE!
  const match=identity.match(/^(creator-request|creator-invite|creator-pending|business)-(\d)@/)
  if(!match)throw Error(`No E2E phone mapping for ${identity}`)
  const prefixes:Record<string,string>={'creator-request':'+25194400000','creator-invite':'+25195500000','creator-pending':'+25196600000','business':'+25193400000'}
  return `${prefixes[match[1]]}${match[2]}`
}

export async function login(page:Page,identity:string){
  const admin=identity==='admin@e2e.invalid'
  // The SPA can replace the document while Vite is still completing the
  // full load event.  Use DOM readiness for the auth reset and retry an
  // aborted navigation once; this is runner synchronization, not product
  // behavior.
  for(let attempt=0;;attempt++){
    try{
      await page.goto('/help',{waitUntil:'domcontentloaded'})
      break
    }catch(error){
      if(attempt>=1 || !String(error).includes('ERR_ABORTED')) throw error
    }
  }
  await page.evaluate(()=>{localStorage.clear();sessionStorage.clear()})
  for(let attempt=0;;attempt++){
    try{
      await page.goto('/',{waitUntil:'domcontentloaded'})
      break
    }catch(error){
      if(attempt>=1 || !String(error).includes('ERR_ABORTED')) throw error
    }
  }
  const welcome=page.getByRole('button',{name:'Already have an account? Sign In',exact:true})
  const phone=page.getByLabel('Phone Number')
  await expect(phone.or(welcome)).toBeVisible({timeout:30000})
  if(!(await phone.isVisible())){
    await welcome.click()
    await expect(phone).toBeVisible({timeout:30000})
  }
  await phone.fill(admin?identity:phoneFor(identity))
  await page.getByRole('textbox',{name:'Password',exact:true}).fill(password)
  const signIn=page.locator('form').getByRole('button',{name:'Sign In',exact:true})
  const loginResponse=page.waitForResponse(response=>response.request().method()==='POST'&&new URL(response.url()).pathname==='/api/v1/auth/login',{timeout:30000})
  await signIn.dispatchEvent('click')
  await loginResponse
  if(admin)return
  // The auth response assigns the workspace route before the React tree has
  // finished hydrating. Wait for the root to be populated before inspecting
  // the PIN/workspace branches.
  await expect(page.locator('#root')).not.toBeEmpty({timeout:30000})
  const setup=page.getByRole('heading',{name:/^(Complete your secure setup|Create 5-digit PIN)$/})
  const app=page.locator('main.app')
  await expect(setup.or(app)).toBeVisible({timeout:30000})
  if(await app.isVisible()){
    if(identity.startsWith('creator-')) await expect(page.getByRole('button',{name:'Discover',exact:true}).first()).toBeVisible({timeout:30000})
    return
  }
  const pinInputs=page.locator('label.pin-entry input')
  await expect(pinInputs).toHaveCount(2)
  await pinInputs.nth(0).fill('12345')
  await pinInputs.nth(1).fill('12345')
  await page.getByRole('button',{name:'Create PIN',exact:true}).click()
  await expect(app).toBeVisible({timeout:30000})
}
