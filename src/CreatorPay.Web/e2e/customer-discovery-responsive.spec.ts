import {expect,test} from './fixtures'
import {login} from './auth-helpers'

const exactPromotion = 'https://www.tiktok.com/@weymela/video/7412345678901234567'

function promotion(index:number, distanceKm?:number){
  return {
    relationshipId:`70000000-0000-0000-0000-${String(index).padStart(12,'0')}`,
    businessId:`71000000-0000-0000-0000-${String(index).padStart(12,'0')}`,
    publicBusinessId:`BUS-${1000+index}`,
    businessName:`Addis Business ${index}`,
    city:'Addis Ababa',
    creatorId:`72000000-0000-0000-0000-${String(index).padStart(12,'0')}`,
    publicCreatorId:`CRE-${1000+index}`,
    creatorCode:String(3300+index),
    creatorName:`Creator ${index}`,
    status:'Active',
    daysLeft:30-index,
    rewardsAvailable:true,
    businessType:'ProfessionalServices',
    addressLine1:`Bole Road ${index}`,
    region:'Addis Ababa',
    promotionVideoUrl:exactPromotion,
    promotionVideoPlatform:'TikTok',
    promotionVideoStatus:'Live',
    distanceKm,
    businessLatitude:9.03+(index/1000),
    businessLongitude:38.74+(index/1000),
  }
}

async function mockPromotionFeed(page:import('@playwright/test').Page,count:number){
  await page.route('**/api/v1/customer/discovery/advertising**',async route=>{
    const url=new URL(route.request().url())
    const withDistance=url.searchParams.has('latitude')
    const rows=Array.from({length:count},(_,i)=>promotion(i+1,withDistance?(i+1)*1.2:undefined))
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(rows)})
  })
}

for(const count of [1,3,10]){
  test(`Customer Discover supports a compact ${count}-promotion feed`,async({page})=>{
    await mockPromotionFeed(page,count)
    await login(page,'shopper@e2e.invalid')
    await page.getByRole('navigation',{name:'Customer navigation'}).getByRole('button',{name:'Discover',exact:true}).click()
    await expect(page.locator('.customer-promotion-card')).toHaveCount(count)
    await expect(page.locator('.customer-promotion-card').first().getByRole('link',{name:'Watch Promotion'})).toHaveAttribute('href',exactPromotion)
    await expect(page.locator('.customer-promotion-card').first().getByRole('link',{name:'Get Directions'})).toHaveAttribute('href',/google\.com\/maps\/dir/)
    expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
  })
}

test('Customer location permission displays km distances and nearest ordering',async({page,context})=>{
  await context.grantPermissions(['geolocation'])
  await context.setGeolocation({latitude:9.03,longitude:38.74})
  await mockPromotionFeed(page,3)
  await login(page,'shopper@e2e.invalid')
  await page.getByRole('navigation',{name:'Customer navigation'}).getByRole('button',{name:'Discover',exact:true}).click()
  await page.getByRole('combobox',{name:'Sort promotions'}).selectOption('nearest')
  await page.getByRole('button',{name:'Use Location'}).click()
  await expect(page.getByText(/Location is on/)).toBeVisible()
  await expect(page.getByText('1.2 km away')).toBeVisible()
  await expect(page.locator('.customer-promotion-card').first()).toContainText('Addis Business 1')
})

test('Customer denied location remains usable and never exposes browser errors',async({page,context})=>{
  await context.clearPermissions()
  await mockPromotionFeed(page,3)
  await login(page,'shopper@e2e.invalid')
  await page.getByRole('navigation',{name:'Customer navigation'}).getByRole('button',{name:'Discover',exact:true}).click()
  await page.getByRole('combobox',{name:'Sort promotions'}).selectOption('nearest')
  await page.getByRole('button',{name:'Use Location'}).click()
  await expect(page.getByText('Location access is off. Enable location to see promotions near you.')).toBeVisible()
  await expect(page.locator('.customer-promotion-card')).toHaveCount(3)
  await expect(page.locator('body')).not.toContainText('User denied Geolocation')
})
