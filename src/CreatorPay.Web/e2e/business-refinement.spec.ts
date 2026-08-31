import {expect,test} from './fixtures'
import {login} from './auth-helpers'

const viewports=[
  {name:'375',width:375,height:812},
  {name:'390',width:390,height:844},
  {name:'393',width:393,height:852},
  {name:'430',width:430,height:932},
  {name:'desktop',width:1280,height:900},
]

async function noOverflow(page:import('@playwright/test').Page){
  expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
}

for(const viewport of viewports){
  test(`Business reference dashboard and routes at ${viewport.name}`,async({page})=>{
    test.setTimeout(90_000)
    await page.setViewportSize({width:viewport.width,height:viewport.height})
    await login(page,'business-1@e2e.invalid')

    await expect(page.getByRole('heading',{name:'Business',exact:true})).toBeVisible()
    await expect(page.getByRole('heading',{name:'Dashboard',exact:true})).toBeVisible()
    const cards=page.locator('.business-dashboard-cards > button')
    await expect(cards).toHaveCount(5)
    await expect(cards.locator('.business-dashboard-label')).toHaveText(['Active Ads','Creator Requests','Video Approvals','Confirmed Sales','Wallet Balance'])
    await expect(cards.filter({hasText:'Active Ads'}).locator('strong')).toHaveText(/^\d+$/)
    await expect(cards.filter({hasText:'Creator Requests'}).locator('strong')).toHaveText(/^\d+$/)
    await expect(cards.filter({hasText:'Video Approvals'}).locator('strong')).toHaveText(/^\d+$/)
    await expect(cards.filter({hasText:'Confirmed Sales'}).locator('strong')).toHaveText(/^\d+$/)
    await expect(cards.filter({hasText:'Wallet Balance'}).locator('strong')).toHaveText(/^\d[\d,]*\.\d{2}$/)
    const quick=page.locator('.business-quick-actions button')
    await expect(quick).toHaveCount(3)
    await expect(quick).toHaveText(['Add New Ad','Add Creator','Cashier Management'])
    const topCreators=page.locator('.business-top-creator-card')
    await expect(page.getByRole('heading',{name:'Top Creators',exact:true})).toBeVisible()
    await expect(topCreators).toHaveCount(3)
    const rankedFollowers=await topCreators.evaluateAll(cards=>cards.map(card=>Number((card as HTMLElement).dataset.followers)))
    expect(rankedFollowers).toEqual([...rankedFollowers].sort((a,b)=>b-a))
    await expect(topCreators.getByRole('button',{name:'View Creator'})).toHaveCount(3)
    const nav=page.getByRole('navigation',{name:'Business sections'})
    await expect(nav.getByRole('button')).toHaveText(['Home','Creators','Active Ads','Requests','Checkout','Profile'])
    await expect(nav.getByRole('button',{name:'Home',exact:true})).toHaveCSS('color','rgb(37, 99, 235)')
    await expect(nav.getByRole('button',{name:'Home',exact:true})).toHaveAttribute('aria-current','page')
    const recentActivity=page.locator('.business-recent-activity')
    if(await recentActivity.count())await expect(recentActivity.getByRole('button')).not.toHaveCount(0)

    for(const selector of ['.business-dashboard-cards > button','.business-quick-actions button','.business-top-creator-card > button','.workspace-nav button']){
      const values=await page.locator(selector).evaluateAll(nodes=>nodes.map(node=>{
        const style=getComputedStyle(node)
        return {select:style.userSelect,webkit:(style as CSSStyleDeclaration&{webkitUserSelect:string}).webkitUserSelect}
      }))
      expect(values.every(value=>value.select==='none'||value.webkit==='none')).toBeTruthy()
    }

    await noOverflow(page)
    await page.screenshot({path:`test-results/business-reference-${viewport.name}.png`,animations:'disabled',fullPage:true})

    const settings=page.getByRole('button',{name:'Settings',exact:true})
    await settings.click()
    const menu=page.getByRole('menu')
    const menuBox=await menu.boundingBox()
    expect(menuBox).not.toBeNull()
    expect(menuBox!.x).toBeGreaterThanOrEqual(0)
    expect(menuBox!.x+menuBox!.width).toBeLessThanOrEqual(viewport.width)
    if(viewport.width<=430)expect(viewport.width-(menuBox!.x+menuBox!.width)).toBeLessThanOrEqual(17)
    await settings.click()
    await noOverflow(page)

    const firstCreatorName=await topCreators.first().locator('.business-top-creator-heading strong').innerText()
    await topCreators.first().getByRole('button',{name:'View Creator'}).click()
    await expect(page.getByRole('heading',{name:'Find Creators',exact:true})).toBeVisible()
    await expect(page.getByLabel('Creator Name')).toHaveValue(firstCreatorName)
    await nav.getByRole('button',{name:'Home',exact:true}).click()
    await expect(page.getByRole('heading',{name:'Dashboard',exact:true})).toBeVisible()

    await cards.filter({hasText:'Active Ads'}).click()
    await expect(page.getByRole('heading',{name:'Active Ads',exact:true})).toBeVisible()
    await page.reload()
    await expect(page.getByRole('heading',{name:'Dashboard',exact:true})).toBeVisible()
    await page.locator('.business-dashboard-cards > button').filter({hasText:'Creator Requests'}).click()
    await expect(page.getByRole('heading',{name:'Requests',exact:true})).toBeVisible()
    const creatorRequestsTab=page.getByRole('tab',{name:'Creator Requests'})
    const videoApprovalsTab=page.getByRole('tab',{name:'Video Approvals'})
    await expect(creatorRequestsTab).toHaveAttribute('aria-selected','true')
    await expect(creatorRequestsTab).toHaveCSS('background-color','rgb(37, 99, 235)')
    await expect(creatorRequestsTab).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(videoApprovalsTab).toHaveCSS('color','rgb(51, 65, 85)')
    await page.reload()
    await page.locator('.business-dashboard-cards > button').filter({hasText:'Video Approvals'}).click()
    await expect(page.getByRole('tab',{name:'Video Approvals'})).toHaveAttribute('aria-selected','true')
    await expect(page.getByRole('tab',{name:'Video Approvals'})).toHaveCSS('background-color','rgb(37, 99, 235)')
    await expect(page.getByRole('tab',{name:'Video Approvals'})).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(page.getByRole('tab',{name:'Creator Requests'})).toHaveCSS('color','rgb(51, 65, 85)')
    const submittedVideo=page.getByRole('link',{name:'View Promo Video'}).first()
    if(await submittedVideo.count())expect(await submittedVideo.getAttribute('href')).toMatch(/^https:\/\//)
    await page.reload()
    await page.locator('.business-dashboard-cards > button').filter({hasText:'Confirmed Sales'}).click()
    await expect(page.getByRole('heading',{name:'Confirmed Sales',exact:true})).toBeVisible()
    await expect(page.locator('.business-sales-summary article')).toHaveCount(3)
    await page.reload()
    await page.locator('.business-dashboard-cards > button').filter({hasText:'Wallet Balance'}).click()
    await expect(page.getByRole('heading',{name:'Wallet',exact:true})).toBeVisible()
    await page.reload()
    await page.getByRole('button',{name:'Checkout',exact:true}).click()
    const checkoutTabs=page.getByRole('navigation',{name:'Cashier Dashboard sections'})
    const checkoutTab=checkoutTabs.getByRole('button',{name:'Checkout',exact:true})
    const recentTab=checkoutTabs.getByRole('button',{name:'Recent Transactions',exact:true})
    await expect(checkoutTab).toHaveCSS('background-color','rgb(37, 99, 235)')
    await expect(checkoutTab).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(recentTab).toHaveCSS('background-color','rgb(255, 255, 255)')
    await expect(recentTab).not.toHaveCSS('color','rgb(255, 255, 255)')
    const submitPurchase=page.getByRole('button',{name:'Submit Purchase',exact:true})
    await expect(submitPurchase).toHaveCSS('background-color','rgb(37, 99, 235)')
    await expect(submitPurchase).toHaveCSS('color','rgb(255, 255, 255)')
    await recentTab.click()
    await expect(recentTab).toHaveCSS('background-color','rgb(37, 99, 235)')
    await expect(recentTab).toHaveCSS('color','rgb(255, 255, 255)')
    await expect(checkoutTab).toHaveCSS('background-color','rgb(255, 255, 255)')
    await noOverflow(page)
    await page.reload()
    await page.getByRole('button',{name:'Profile',exact:true}).click()
    await expect(page.getByText('Business Name',{exact:true})).toBeVisible()
    await expect(page.getByText('Business ID',{exact:true})).toBeVisible()
    await noOverflow(page)
  })
}
