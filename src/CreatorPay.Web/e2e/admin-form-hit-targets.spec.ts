import {devices,expect,test} from '@playwright/test'

const paths=[
  '/admin/admin-accounts',
  '/admin/creator-accounts',
  '/admin/business-accounts',
  '/admin/customer-accounts',
  '/admin/cashier-accounts',
]

const sizes=[
  ['375',{width:375,height:812}],
  ['390',{width:390,height:844}],
  ['393',{width:393,height:852}],
  ['430',{width:430,height:932}],
  ['tablet',{width:768,height:1024}],
  ['desktop',{width:1440,height:1000}],
] as const

const roleClaim='http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
const payload=Buffer.from(JSON.stringify({role:'PlatformAdmin',[roleClaim]:'PlatformAdmin'})).toString('base64url')
const accessToken=`e30.${payload}.signature`

test.use({...devices['iPhone 13']})

test('all five admin create forms expose the complete visible control as the touch target',async({page},testInfo)=>{
  test.skip(testInfo.project.name==='desktop','The mobile Chromium project and WebKit project cover touch; each also exercises desktop dimensions.')
  test.setTimeout(180_000)
  await page.addInitScript(token=>{
    localStorage.setItem('creatorpay_access_token',token)
    localStorage.setItem('creatorpay_refresh_token','hit-target-test')
  },accessToken)
  await page.route('**/api/v1/**',async route=>{
    const pathname=new URL(route.request().url()).pathname
    const body=pathname.endsWith('/unread-count')?{count:0}
      :pathname.includes('/notifications')?{items:[]}
      :pathname.endsWith('/dashboard/summary')?{}
      :{items:[],page:1,total:0,totalPages:1}
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(body)})
  })

  for(const [,viewport] of sizes){
    await page.setViewportSize(viewport)
    for(const path of paths){
      await page.goto(path,{waitUntil:'domcontentloaded'})
      const form=page.locator('.admin-create-card form')
      await expect(form).toBeVisible()
      const controls=form.locator('input:not([type=hidden]),select,textarea')
      for(let index=0;index<await controls.count();index++){
        const control=controls.nth(index)
        await control.scrollIntoViewIfNeeded()
        const box=await control.boundingBox()
        expect(box).not.toBeNull()
        expect(box!.height).toBeGreaterThanOrEqual(44)
        const points=[
          [box!.x+3,box!.y+box!.height/2],
          [box!.x+box!.width/2,box!.y+3],
          [box!.x+box!.width-3,box!.y+box!.height/2],
          [box!.x+box!.width/2,box!.y+box!.height-3],
          [box!.x+box!.width/2,box!.y+box!.height/2],
        ] as const
        for(const [x,y] of points){
          expect(await page.evaluate(([hitX,hitY])=>document.elementFromPoint(hitX,hitY)?.matches('input,select,textarea')??false,[x,y])).toBeTruthy()
          await page.touchscreen.tap(x,y)
          await expect(control).toBeFocused()
        }
        await expect(control).toHaveCSS('pointer-events','auto')
        const tag=await control.evaluate(element=>element.tagName)
        if(tag==='SELECT'){
          await expect(control).toHaveCSS('-webkit-user-select','none')
          const options=control.locator('option')
          if(await options.count()>1){
            const value=await options.nth(1).getAttribute('value')
            if(value!==null)await control.selectOption(value)
          }
        }else{
          await expect(control).toHaveCSS('-webkit-user-select','text')
          await control.fill('Touch input check')
          await expect(control).not.toHaveValue('')
        }
      }
      expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy()
    }
  }
})
