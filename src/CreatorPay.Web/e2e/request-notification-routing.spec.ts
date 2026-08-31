import {expect,test,type Page,type Route} from './fixtures'

const screenshotDir=process.env.E2E_SCREENSHOT_DIR
const viewports=[
  {width:375,height:812},
  {width:390,height:844},
  {width:393,height:852},
  {width:430,height:932},
  {width:1280,height:900},
]

function token(role:'MerchantAdmin'|'Creator'){
  const payload={
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':role,
    account_status:'Active',
    merchant_id:role==='MerchantAdmin'?'merchant-test':undefined,
    sub:`${role.toLowerCase()}-test`,
  }
  return `x.${Buffer.from(JSON.stringify(payload)).toString('base64url')}.x`
}

async function install(page:Page,role:'MerchantAdmin'|'Creator'){
  await page.addInitScript(({accessToken})=>{
    localStorage.setItem('creatorpay_access_token',accessToken)
    localStorage.removeItem('creatorpay_refresh_token')
  },{accessToken:token(role)})
  const now=new Date().toISOString()
  const businessRows=[
    {id:'10000000-0000-0000-0000-000000000001',creatorId:'20000000-0000-0000-0000-000000000001',creatorPublicId:'4827',creatorName:'Creator Request',status:'Pending',requestedAtUtc:now,promotionActive:false,initiatedBy:'Creator'},
    {id:'10000000-0000-0000-0000-000000000002',creatorId:'20000000-0000-0000-0000-000000000002',creatorPublicId:'4830',creatorName:'Video Creator',status:'Approved',requestedAtUtc:now,promotionActive:false,initiatedBy:'Creator',promotionVideo:{id:'30000000-0000-0000-0000-000000000001',videoUrl:'https://www.tiktok.com/@weymela/video/1234567890123456789',platform:'TikTok',status:'Pending',submittedAtUtc:now}},
  ]
  const creatorRows=[
    {id:'10000000-0000-0000-0000-000000000003',merchantId:'40000000-0000-0000-0000-000000000001',merchantName:'Abc',status:'Pending',requestedAtUtc:now,promotionActive:false,initiatedBy:'Business'},
  ]
  let notices=role==='MerchantAdmin'
    ?[{notificationId:'NTF-BUSINESS-VIDEO',type:'PromotionVideoSubmitted',title:'New promotion video waiting for approval.',body:'Video Creator submitted a TikTok promotion video for approval.',createdAtUtc:now,data:{TargetPath:'/?view=requests&section=video'}}]
    :[{notificationId:'NTF-CREATOR-INVITE',type:'PartnershipRequested',title:'New Business invitation',body:'Abc invited you to promote their Business.',createdAtUtc:now,data:{TargetPath:'/?view=requests&section=invitations'}}]
  await page.route('https://api-pilot.weymela.com/**',async(route:Route)=>{
    const request=route.request(),url=new URL(request.url())
    const json=(value:unknown,status=200)=>route.fulfill({status,contentType:'application/json',body:JSON.stringify(value),headers:{'access-control-allow-origin':'*'}})
    if(url.pathname==='/api/v1/auth/pin/status')return json({isEligible:false,isPinEnrolled:false,isLocked:false,failedAttemptCount:0})
    if(url.pathname==='/api/v1/notifications/unread-count')return json({count:notices.filter(x=>!('readAtUtc' in x)).length})
    if(url.pathname==='/api/v1/notifications'&&request.method()==='GET')return json({items:notices,page:1,pageSize:30,total:notices.length})
    if(/^\/api\/v1\/notifications\/NTF-[A-Z-]+\/read$/.test(url.pathname)&&request.method()==='POST'){
      notices=notices.map(x=>({...x,readAtUtc:new Date().toISOString()}))
      return route.fulfill({status:204,body:''})
    }
    if(url.pathname==='/api/v1/merchant/partnerships')return json(businessRows)
    if(url.pathname==='/api/v1/merchant/creators/search')return json([])
    if(url.pathname==='/api/v1/merchants/me')return json({tradingName:'Abc',merchantStatus:'Active',effectiveStatus:'Active',effectiveStatusReason:''})
    if(url.pathname==='/api/v1/merchant/wallet')return json({availableBalance:1000,currencyCode:'ETB',status:'Active',minimumRequiredBalance:1000,advertisingEligible:true})
    if(url.pathname==='/api/v1/merchant/dashboard-metrics')return json({confirmedSales:0,period:'All Time'})
    if(url.pathname==='/api/v1/creators/me')return json({displayName:'adonay',publicCreatorId:'CR-INTERNAL',creatorCode:'4827',creatorStatus:'Active',accountStatus:'Active',effectiveStatus:'Active',effectiveStatusReason:'',email:'creator@example.invalid',phoneNumber:'+251911000000',city:'Addis Ababa'})
    if(url.pathname==='/api/v1/creator/earnings/summary')return json({currencyCode:'ETB',pendingBalance:0,availableBalance:0,heldBalance:0,scheduledBalance:0,currentPayoutAmount:0,currentPeriodConfirmedSales:0,confirmedSalesCount:0,upcomingPayoutAmount:0})
    if(url.pathname==='/api/v1/creator/payouts')return json([])
    if(url.pathname==='/api/v1/creator/partnerships')return json(creatorRows)
    return json([])
  })
}

for(const viewport of viewports){
  test(`Business request labels remain visible and notification opens Video Approvals at ${viewport.width}px`,async({page,browserName})=>{
    await page.setViewportSize(viewport)
    await install(page,'MerchantAdmin')
    await page.goto('/',{waitUntil:'domcontentloaded'})
    await page.getByRole('button',{name:'Requests',exact:true}).click()
    const creator=page.getByRole('tab',{name:'Creator Requests'}),video=page.getByRole('tab',{name:'Video Approvals'})
    await expect(creator).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(video).toHaveCSS('color','rgb(51, 65, 85)')
    await video.click()
    await expect(video).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(creator).toHaveCSS('color','rgb(51, 65, 85)')
    await page.getByRole('button',{name:'Notifications'}).click()
    await page.getByRole('button',{name:/New promotion video waiting for approval/}).click()
    await expect(video).toHaveAttribute('aria-selected','true')
    expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
    if(screenshotDir)await page.screenshot({path:`${screenshotDir}/business-requests-${browserName}-${viewport.width}.png`,animations:'disabled',fullPage:true})
  })
}

test('Creator Business invitation notification opens Requests and its actionable invitation',async({page,browserName})=>{
  await page.setViewportSize({width:390,height:844})
  await install(page,'Creator')
  await page.goto('/',{waitUntil:'domcontentloaded'})
  await page.getByRole('button',{name:'Notifications'}).click()
  await page.getByRole('button',{name:/New Business invitation/}).click()
  await expect(page.getByRole('heading',{name:'Requests',exact:true})).toBeVisible()
  await expect(page.getByRole('heading',{name:'Business Invitations',exact:true})).toBeVisible()
  const invitation=page.locator('.creator-request-sections article').filter({hasText:'Abc'})
  await expect(invitation.getByRole('button',{name:'Accept'})).toBeVisible()
  await expect(invitation.getByRole('button',{name:'Decline'})).toBeVisible()
  expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
  if(screenshotDir)await page.screenshot({path:`${screenshotDir}/creator-invitation-${browserName}-390.png`,animations:'disabled',fullPage:true})
})
