import {expect,test} from './fixtures'
import type {Page} from '@playwright/test'
import {login} from './auth-helpers'

const screenshotDir=process.env.E2E_SCREENSHOT_DIR
const creatorName=process.env.E2E_CREATOR_NAME!
const viewports=[
  {name:'375',width:375,height:812},
  {name:'390',width:390,height:844},
  {name:'393',width:393,height:852},
  {name:'430',width:430,height:932},
  {name:'desktop',width:1280,height:900},
]

async function noOverflow(page:Page){
  expect(await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(1)
}

for(const viewport of viewports){
  test(`final Cashier, Creator, and Customer polish fits ${viewport.name}`,async({page})=>{
    test.setTimeout(120_000)
    await page.setViewportSize({width:viewport.width,height:viewport.height})

    await login(page,'cashier@e2e.invalid')
    await expect(page.getByRole('heading',{name:'Cashier',exact:true})).toBeVisible()
    await expect(page.getByRole('heading',{name:'Cashier Dashboard'})).toHaveCount(0)
    const cashierNav=page.getByRole('navigation',{name:'Cashier Dashboard sections'})
    await expect(cashierNav.getByRole('button')).toHaveText(['New Purchase','Recent Transactions','Profile'])
    await page.getByLabel('Settings').click()
    const cashierSettings=page.getByRole('menu')
    await expect(cashierSettings.getByRole('menuitem',{name:'Help'})).toBeVisible()
    await expect(cashierSettings.getByRole('menuitem',{name:'Sign out'})).toBeVisible()
    const settingsBox=await cashierSettings.boundingBox()
    expect(settingsBox).not.toBeNull()
    expect(settingsBox!.x).toBeGreaterThanOrEqual(0)
    expect(settingsBox!.x+settingsBox!.width).toBeLessThanOrEqual(viewport.width)
    await page.getByLabel('Settings').click()
    await cashierNav.getByRole('button',{name:'Recent Transactions'}).click()
    const transaction=page.locator('.cashier-transaction-row:not(.headings)').first()
    await expect(transaction).toContainText(creatorName)
    await expect(transaction.locator('[data-label="Creator ID"]')).toHaveText(/^[1-9][0-9]{3}$/)
    await expect(transaction.locator('[data-label="Reference"]')).toHaveText(/^#[A-Z0-9]{4}$/)
    await expect(transaction).not.toContainText(/CP-|[0-9a-f]{8}-[0-9a-f]{4}/i)
    await noOverflow(page)
    if(screenshotDir)await page.screenshot({path:`${screenshotDir}/cashier-recent-${viewport.name}.png`,fullPage:true,animations:'disabled'})

    await login(page,'creator@e2e.invalid')
    const title=page.locator('.role-creator .account-header h1')
    const avatar=page.locator('.role-creator .account-header-avatar')
    const titleBox=await title.boundingBox(),avatarBox=await avatar.boundingBox()
    expect(titleBox).not.toBeNull();expect(avatarBox).not.toBeNull()
    if(viewport.width<=430){
      expect(avatarBox!.x-(titleBox!.x+titleBox!.width)).toBeGreaterThanOrEqual(8)
      expect(avatarBox!.x-(titleBox!.x+titleBox!.width)).toBeLessThanOrEqual(10)
      expect(avatarBox!.width).toBeGreaterThanOrEqual(44)
      expect(avatarBox!.width).toBeLessThanOrEqual(48)
    }
    await page.getByRole('button',{name:'Active Ads',exact:true}).click()
    const liveRow=page.locator('.creator-ads-row.relationship-active').filter({hasText:'Active E2E Business'}).first()
    const videoLink=liveRow.getByRole('link',{name:'View TikTok Video'})
    await expect(videoLink).toHaveAttribute('href','https://www.tiktok.com/@weymela-e2e/video/1234567890123456789')
    const linkBox=await videoLink.boundingBox(),cellBox=await liveRow.locator('.creator-ads-promo').boundingBox()
    expect(linkBox).not.toBeNull();expect(cellBox).not.toBeNull()
    expect(linkBox!.width).toBeLessThan(cellBox!.width)
    await noOverflow(page)
    if(screenshotDir)await page.screenshot({path:`${screenshotDir}/creator-active-${viewport.name}.png`,fullPage:true,animations:'disabled'})

    await login(page,'shopper@e2e.invalid')
    await page.getByRole('navigation',{name:'Customer navigation'}).getByRole('button',{name:'Discover',exact:true}).click()
    const cards=page.locator('.customer-promotion-card')
    await expect(cards.first()).toBeVisible({timeout:10_000})
    await expect(cards.first().locator('.customer-creator-id b')).toHaveText(/^[1-9][0-9]{3}$/)
    expect((await cards.allTextContents()).join(' ')).not.toMatch(/not provided/i)
    await noOverflow(page)
    if(screenshotDir)await page.screenshot({path:`${screenshotDir}/customer-discover-${viewport.name}.png`,fullPage:true,animations:'disabled'})
  })
}
