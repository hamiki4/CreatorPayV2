import {expect,test} from './fixtures'

test.beforeEach(async({page})=>{
  await page.goto('/help')
  await page.evaluate(()=>localStorage.clear())
})

test('first visit welcomes users, registration routes return to Sign In, and returning visits skip welcome',async({page})=>{
  await page.goto('/')
  await expect(page.getByRole('heading',{name:'Welcome to Weymela'})).toBeVisible()
  await expect(page.getByText('Shop. Promote. Earn.')).toBeVisible()

  const registrations=[
    ['Shopper Registration','Display Name'],
    ['Content Creator Registration','Legal First Name'],
    ['Business Owner Registration','Legal Business Name'],
  ] as const
  for(const [action,field] of registrations){
    await page.getByRole('button',{name:action,exact:true}).click()
    await expect(page.getByRole('heading',{name:'Create your account'})).toBeVisible()
    await expect(page.getByLabel(field,{exact:true})).toBeVisible()
    const back=page.getByRole('button',{name:'Back to Sign In',exact:true})
    await expect(back).toBeVisible()
    const normal=await back.evaluate(element=>{const style=getComputedStyle(element);return {background:style.backgroundColor,color:style.color,border:style.borderColor,width:element.getBoundingClientRect().width}})
    expect(normal.background).not.toBe('rgba(0, 0, 0, 0)')
    expect(normal.border).not.toBe(normal.background)
    expect(normal.width).toBeGreaterThan(200)
    await back.hover()
    await expect.poll(()=>back.evaluate(element=>getComputedStyle(element).backgroundColor)).not.toBe(normal.background)
    await page.keyboard.press('Tab')
    await expect(back).toBeFocused()
    expect(await back.evaluate(element=>getComputedStyle(element).outlineStyle)).not.toBe('none')
    await back.click()
    await expect(page.getByRole('heading',{name:'Sign In'})).toBeVisible()
    await page.getByRole('button',{name:'Sign Up to Weymela',exact:true}).click()
    await expect(page.getByRole('heading',{name:'Sign Up to Weymela'})).toBeVisible()
  }

  await page.getByRole('button',{name:'Back to Sign In',exact:true}).click()
  await page.reload()
  await expect(page.getByRole('heading',{name:'Sign In'})).toBeVisible()
  await expect(page.getByRole('heading',{name:'Welcome to Weymela'})).toHaveCount(0)

  const headingSize=await page.getByRole('heading',{name:'Sign In'}).evaluate(element=>parseFloat(getComputedStyle(element).fontSize))
  expect(headingSize).toBeLessThanOrEqual(page.viewportSize()!.width<=420?38:48)
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
})

test('first-visit Sign In action and registration validation remain available',async({page})=>{
  await page.goto('/')
  await page.getByRole('button',{name:'Already have an account? Sign In',exact:true}).click()
  await expect(page.getByLabel('Phone Number')).toBeVisible()
  await page.getByRole('button',{name:'Sign Up to Weymela',exact:true}).click()
  await page.getByRole('button',{name:'Shopper Registration',exact:true}).click()
  await page.getByRole('button',{name:'Create shopper account'}).click()
  await expect(page.getByLabel('Display Name')).toHaveAttribute('required','')
})
