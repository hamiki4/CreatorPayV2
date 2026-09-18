import {expect,test} from './fixtures'
import type {Page,Route} from '@playwright/test'

type ProductRole='MerchantAdmin'|'Creator'|'PlatformAdmin'|'OperationsAdmin'

const emptyEarnings={
  availableEarnings:0,minimumToCashOut:3000,amountNeeded:3000,eligibleAmount:0,
  viewEarnings:0,saleEarnings:0,ugcEarnings:0,history:[],payoutHistory:[],
}

function response(path:string,role:ProductRole){
  if(path==='/api/v1/integration/v3/session')return {
    role,status:'Active',isOnboarding:false,
    destination:role==='MerchantAdmin'?'/business':role==='Creator'?'/creator':'/admin',
    displayName:'Rendered Test',accountEmail:'rendered@example.invalid',accountPhone:'+251900000000',
  }
  if(path==='/api/v1/merchants/me')return {tradingName:'Blue Nile Coffee',effectiveStatus:'Active'}
  if(path==='/api/v1/creators/me')return {
    displayName:'Mimi Creator',publicCreatorId:'rendered-test',effectiveStatus:'Active',
    email:'rendered@example.invalid',phoneNumber:'+251900000000',city:'Addis Ababa',
    socialProfiles:[{platform:'TikTok',profileUrl:'https://www.tiktok.com/@mimi',followerCount:12000,verificationStatus:'Verified'}],
  }
  if(path==='/api/business/home')return {wallet:{available:10000,reserved:1650,totalBalance:11650},activeCampaigns:1,creatorRequests:2,confirmedSales:3}
  if(path==='/api/business/promotions'||path==='/api/business/ugc'||path==='/api/business/deposit-requests')return []
  if(path==='/api/business/ugc-pricing')return {minimumCreatorPayment:200,platformFeePercent:10,minimumUgcBudget:null,financialConfigurationVersion:2}
  if(path==='/api/business/wallet')return {available:10000,reserved:1650,totalBalance:11650,version:1,history:[]}
  if(path==='/api/creator/home')return {requests:1,activeCampaigns:1,earnings:emptyEarnings}
  if(path==='/api/creator/earnings')return emptyEarnings
  if(path==='/api/creator/promotions/discover')return [{
    id:'promotion-1',businessId:'business-1',business:{displayName:'Blue Nile Coffee'},title:'Coffee launch',
    description:'Create a short launch video.',type:'ViewPlusCommission',startUtc:'2026-09-18T00:00:00Z',
    endUtc:'2026-10-18T00:00:00Z',budget:3000,approvedCreators:0,creatorCapacity:2,
    platforms:[{platform:'TikTok',approved:0,capacity:2,available:2}],
    eligibleSocialProfiles:[{id:'social-1',platform:'TikTok',profileUrl:'https://www.tiktok.com/@mimi',selfReportedAudience:12000,verificationStatus:'Verified'}],
  }]
  if(path==='/api/creator/requests'||path==='/api/creator/campaigns'||path==='/api/creator/ugc/assignments')return []
  if(path==='/api/creator/ugc')return [{
    id:'ugc-1',businessId:'business-1',business:'Blue Nile Coffee',title:'Product photos',contentType:'Photos',
    status:'Open',creatorPayment:500,creatorsNeeded:3,approvedCreators:0,requiredFunding:1650,
    reservedFunding:1650,usedFunding:0,dueDateUtc:'2026-10-18T00:00:00Z',version:1,
    platformRequirements:[{platform:'Instagram',format:'Instagram Reels'}],
  }]
  if(path==='/api/notifications')return {items:[],unreadCount:0}
  if(path==='/api/admin/promotions'||path==='/api/admin/ugc'||path==='/api/admin/notifications')return []
  if(path==='/api/v1/admin/dashboard/summary')return {}
  if(path==='/api/admin/home')return {pendingCreatorApprovals:0,pendingBusinessApprovals:0,pendingDeposits:0,pendingPayouts:0,activeCampaigns:0,openUgc:0,failedOutbox:0}
  if(path==='/api/admin/operations')return {}
  if(path.startsWith('/api/admin/')||path.startsWith('/api/v1/admin/'))return []
  return []
}

async function mockProduct(page:Page,role:ProductRole){
  await page.route('**/api/**',async(route:Route)=>{
    const path=new URL(route.request().url()).pathname
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(response(path,role))})
  })
}

async function noHorizontalOverflow(page:Page){
  await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
}

test('Business renders Promotion and funded UGC creation without hidden required actions',async({page})=>{
  await mockProduct(page,'MerchantAdmin')
  await page.goto('/business')
  await expect(page.getByRole('heading',{name:'Home'})).toBeVisible()
  await page.getByRole('navigation',{name:'Business sections'}).getByRole('button',{name:'Promotions'}).click()
  await expect(page.getByRole('heading',{name:'Promotions',exact:true})).toBeVisible()
  await page.getByRole('button',{name:'Create Promotion'}).click()
  await expect(page.getByLabel('Type')).toContainText('View Only')
  await expect(page.getByLabel('Type')).toContainText('View & Sale')
  await expect(page.getByRole('button',{name:'Publish Promotion'})).toBeVisible()
  await page.getByRole('navigation',{name:'Business sections'}).getByRole('button',{name:'UGC'}).click()
  await page.getByRole('button',{name:'Create UGC'}).click()
  await expect(page.getByText('Current minimum 200 · Platform fee 10%')).toBeVisible()
  await expect(page.getByRole('button',{name:'Publish UGC'})).toBeVisible()
  await expect(page.getByRole('button',{name:'Save draft'})).toBeVisible()
  await noHorizontalOverflow(page)
})

test('Creator discovers a specific Promotion/platform and dedicated UGC opportunity',async({page})=>{
  await mockProduct(page,'Creator')
  await page.goto('/creator')
  await page.getByRole('navigation',{name:'Creator sections'}).getByRole('button',{name:'Discover'}).click()
  await expect(page.getByRole('heading',{name:'Discover Promotions'})).toBeVisible()
  await expect(page.getByText('View & Sale')).toBeVisible()
  await page.getByRole('button',{name:'Request to Join'}).click()
  const dialog=page.getByRole('dialog',{name:'Choose platform'})
  await expect(dialog.getByText('TikTok')).toBeVisible()
  await expect(dialog.getByRole('button',{name:'Submit request'})).toBeVisible()
  await dialog.getByRole('button',{name:'Cancel'}).click()
  await page.getByRole('navigation',{name:'Creator sections'}).getByRole('button',{name:'UGC'}).click()
  await expect(page.getByRole('heading',{name:'UGC'})).toBeVisible()
  await expect(page.getByText('Product photos')).toBeVisible()
  await expect(page.getByRole('button',{name:'Request to Join'})).toBeVisible()
  await noHorizontalOverflow(page)
})

test('Platform Admin renders governed Promotion and UGC oversight',async({page})=>{
  await mockProduct(page,'PlatformAdmin')
  await page.goto('/admin/promotions')
  await expect(page.getByRole('heading',{name:'Promotions'})).toBeVisible()
  await expect(page.getByRole('link',{name:/Financial Settings/})).toBeVisible()
  await expect(page.getByRole('link',{name:/Admin Accounts/})).toBeVisible()
  const openMenu=page.getByRole('button',{name:'Open admin menu'})
  if(await openMenu.isVisible())await openMenu.click()
  await page.getByRole('link',{name:/UGC/}).click()
  await expect(page.getByRole('heading',{name:'UGC'})).toBeVisible()
  await noHorizontalOverflow(page)
})

test('Operations Admin cannot navigate to Platform Admin financial authority',async({page})=>{
  await mockProduct(page,'OperationsAdmin')
  await page.goto('/admin/creator-review')
  await expect(page.getByRole('link',{name:/Financial Settings/})).toHaveCount(0)
  await expect(page.getByRole('link',{name:/Admin Accounts/})).toHaveCount(0)
  await expect(page.getByRole('link',{name:/Promotions/})).toBeVisible()
  await expect(page.getByRole('link',{name:/UGC/})).toBeVisible()
  await noHorizontalOverflow(page)
})
