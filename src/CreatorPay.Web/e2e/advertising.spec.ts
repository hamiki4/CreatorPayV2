import {expect,test} from './fixtures'
import {login} from './auth-helpers'

const suffix=(project:string)=>project==='mobile'?'2':'1'

test('Admin payout cycles use readable summaries and compact searches',async({page})=>{
  await login(page,'admin@e2e.invalid')
  await page.getByRole('link',{name:'Payouts',exact:true}).click()
  await expect(page.getByRole('heading',{name:'Payout Cycles'})).toBeVisible()
  const summary=page.locator('.payout-cycle-summary')
  for(const label of ['Cycle Start','Cutoff','Payout Date','Eligible/Scheduled Total','Reserved/In Batch','Paid This Cycle'])await expect(summary.getByText(label,{exact:true})).toBeVisible()
  await expect(summary.getByText(/^\d{2}\/\d{2}\/\d{4}$/).first()).toBeVisible()
  const payoutSearch=page.getByLabel('Search Creator Name or Creator ID').locator('xpath=../..')
  await expect(payoutSearch.getByRole('searchbox')).toBeVisible()
  await expect(payoutSearch.getByRole('button',{name:'Search',exact:true})).toBeVisible()
  await expect(payoutSearch.getByRole('button',{name:'Clear',exact:true})).toBeVisible()
  await page.getByRole('button',{name:'Customers',exact:true}).click()
  await expect(page.getByLabel('Search Customer Name')).toBeVisible()
  await expect(page.getByText('Customer Name',{exact:true})).toBeVisible()
  await expect(page.getByText('Eligible Cashback',{exact:true})).toBeVisible()
  await page.getByRole('button',{name:'Platform Revenue',exact:true}).click()
  await expect(page.getByRole('heading',{name:'Current Platform Revenue Period'})).toBeVisible()
  for(const label of ['Period Start','Period End','Transactions','Platform Revenue'])await expect(page.locator('.revenue-period-summary').getByText(label,{exact:true})).toBeVisible()
  await expect(page.getByText('Platform Revenue History',{exact:true})).toBeVisible()
  await expect(page.getByText('This section is temporarily unavailable.')).toHaveCount(0)
  await page.getByRole('link',{name:'Wallets',exact:true}).click()
  await expect(page.getByRole('heading',{name:'Wallets'})).toBeVisible()
  await expect(page.locator('main')).not.toContainText(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/)
})

test('Platform Admin approves a pending Creator',async({page},testInfo)=>{
  const n=suffix(testInfo.project.name),name=`${n==='1'?'Desktop':'Mobile'} Pending Creator`
  await login(page,'admin@e2e.invalid')
  await page.locator('nav[aria-label="Admin navigation"] a[href="/admin/creator-review"]').click()
  await expect(page.getByRole('heading',{name:'Creator Review'})).toBeVisible()
  const card=page.locator('article').filter({hasText:name})
  await expect(card.getByText('Pending Approval')).toBeVisible()
  await card.getByRole('button',{name:'Review'}).click()
  await expect(page.getByRole('heading',{name})).toBeVisible()
  await page.getByRole('button',{name:'Approve',exact:true}).click()
  await expect(page.getByRole('status')).toContainText('Creator decision saved and audited.')
  await expect(page.locator('article').filter({hasText:name})).toHaveCount(0)
  await page.getByRole('button',{name:'Sign out'}).click()
  await login(page,`creator-pending-${n}@e2e.invalid`)
  await expect(page.getByRole('heading',{name:'Creator'})).toBeVisible()
  await expect(page.getByText('Active',{exact:true}).first()).toBeVisible()
})

test('Creator requests a Business and the Business activates it',async({page,request:apiRequest},testInfo)=>{
  test.setTimeout(120_000)
  const n=suffix(testInfo.project.name),label=n==='1'?'Desktop':'Mobile',business=`${label} Workflow Business`,creator=`${label} Request Creator`
  // This workflow uses an unfunded synthetic merchant. Keep its fixture
  // readiness deterministic when an earlier spec changes the platform
  // minimum-wallet setting.
  const adminLogin=await apiRequest.post(`${process.env.E2E_API_URL}/api/v1/auth/login`,{data:{email:process.env.E2E_ADMIN_EMAIL,password:process.env.E2E_ADMIN_PASSWORD}})
  expect(adminLogin.ok()).toBeTruthy()
  const adminBody=await adminLogin.json(),headers={Authorization:`Bearer ${adminBody.accessToken}`}
  const settingsResponse=await apiRequest.get(`${process.env.E2E_API_URL}/api/v1/admin/financial-settings`,{headers})
  expect(settingsResponse.ok()).toBeTruthy()
  const settings=await settingsResponse.json()
  const resetMinimum=await apiRequest.put(`${process.env.E2E_API_URL}/api/v1/admin/financial-settings`,{headers,data:{...settings,minimumBusinessWalletBalance:0,payoutScheduleEffectiveFromUtc:new Date(Date.now()+60000).toISOString()}})
  expect(resetMinimum.ok(), await resetMinimum.text()).toBeTruthy()
  // Prove fixture readiness through the same authenticated discovery API that
  // the UI consumes before asserting a rendered card. This avoids racing the
  // settings/seed read model and gives a useful failure if the API is not
  // ready yet.
  const creatorLogin=await apiRequest.post(`${process.env.E2E_API_URL}/api/v1/auth/login`,{data:{email:`creator-request-${n}@e2e.invalid`,password:process.env.E2E_SHOPPER_PASSWORD}})
  expect(creatorLogin.ok(), await creatorLogin.text()).toBeTruthy()
  const creatorAuth=await creatorLogin.json(),creatorHeaders={Authorization:`Bearer ${creatorAuth.accessToken}`}
  let discovery:any[]=[]
  for(let attempt=0;attempt<40;attempt++){
    const response=await apiRequest.get(`${process.env.E2E_API_URL}/api/v1/creator/merchants/search?q=${encodeURIComponent(business)}`,{headers:creatorHeaders})
    expect(response.ok(), await response.text()).toBeTruthy()
    discovery=await response.json()
    if(discovery.some(x=>x.tradingName===business))break
    await new Promise(resolve=>setTimeout(resolve,250))
  }
  expect(discovery.some(x=>x.tradingName===business)).toBeTruthy()
  await login(page,`creator-request-${n}@e2e.invalid`)
  await expect(page.getByRole('heading',{name:'Creator'})).toBeVisible()
  await page.getByRole('button',{name:'Settings'}).click();await expect(page.getByRole('menu').getByRole('menuitem',{name:'Help',exact:true})).toBeVisible();await page.getByRole('button',{name:'Settings'}).click()
  await expect(page.getByRole('button',{name:'Send feedback'})).toHaveCount(0)
  await page.getByRole('button',{name:'Find Businesses'}).first().click()
  const automaticBusinessCard=page.locator('article').filter({hasText:business})
  await expect(automaticBusinessCard).toBeVisible({timeout:30000})
  await expect(automaticBusinessCard.getByRole('button',{name:'Request to Advertise'})).toBeEnabled()
  await page.getByPlaceholder('Search business or city').fill(business)
  const businessCard=page.locator('article').filter({hasText:business})
  await expect(businessCard).toBeVisible()
  await businessCard.getByRole('button',{name:'Request to Advertise'}).click()
  await expect(page.getByRole('status')).toContainText(`Advertising request sent to ${business}.`)
  await expect(businessCard.getByText('Request Pending',{exact:true})).toBeVisible()
  await expect(businessCard.getByRole('button',{name:'Request to Advertise'})).toHaveCount(0)

  await page.evaluate(()=>localStorage.clear())
  await login(page,`business-${n}@e2e.invalid`)
  await expect(page.getByRole('heading',{name:'Business'})).toBeVisible()
  await page.getByRole('button',{name:'Settings'}).click();await expect(page.getByRole('menu').getByRole('menuitem',{name:'Help',exact:true})).toBeVisible();await page.getByRole('button',{name:'Settings'}).click()
  await expect(page.getByRole('button',{name:'Send feedback'})).toHaveCount(0)
  await page.getByRole('navigation',{name:'Business sections'}).getByRole('button',{name:'Requests',exact:true}).click()
  const requestCard=page.locator('article').filter({hasText:creator})
  await expect(requestCard).toContainText('Pending')
  const approvalResponse=page.waitForResponse(response=>response.request().method()==='POST'&&new URL(response.url()).pathname.startsWith('/api/v1/merchant/partnerships/')&&new URL(response.url()).pathname.endsWith('/approve'),{timeout:30000})
  await requestCard.getByRole('button',{name:'Accept'}).click()
  const approval=await approvalResponse
  expect(approval.ok(),await approval.text()).toBeTruthy()
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  const approvedCreator=page.locator('article').filter({hasText:creator})
  await expect(approvedCreator).toContainText('Active')

  await page.evaluate(()=>localStorage.clear())
  await login(page,`creator-request-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Find Businesses'}).first().click()
  await expect(page.locator('article').filter({hasText:business})).toHaveCount(0)
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  // The Active Ads view intentionally contains an active-relationship grid
  // and a separate sales/earnings report table. Scope to the first active row.
  const creatorActiveRow=page.locator('.creator-ads-row.relationship-active').filter({hasText:business})
  await expect(creatorActiveRow).toHaveCount(1,{timeout:30000})
  await expect(creatorActiveRow).toContainText('Active')
  await expect(creatorActiveRow).toContainText('Add Promo Video')
  await page.getByRole('button',{name:'Settings'}).click()
  const profileResponse=page.waitForResponse(response=>response.request().method()==='GET'&&new URL(response.url()).pathname==='/api/v1/creators/me',{timeout:30000})
  await page.getByRole('menu').getByRole('menuitem',{name:'Profile',exact:true}).click()
  await profileResponse
  await expect(page.getByRole('heading',{name:'Profile'})).toBeVisible({timeout:30000})
  await expect(page.locator('.creator-id-profile')).toContainText(`Creator ID`)
  await expect(page.locator('.creator-id-profile').getByRole('strong')).toHaveText(`510${n}`)
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  await expect(page.getByRole('button',{name:'Deactivate Ad'})).toHaveCount(0)

  await page.evaluate(()=>localStorage.clear())
  await login(page,`business-${n}@e2e.invalid`)
  await expect(page.getByRole('button',{name:'Active Ads',exact:true})).toBeVisible({timeout:30000})
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  const activeAdCard=page.locator('article').filter({hasText:creator})
  await expect(activeAdCard).toBeVisible({timeout:30000})
  await expect(activeAdCard.getByRole('button',{name:'Deactivate Ad'})).toBeVisible({timeout:30000})
  page.once('dialog',dialog=>dialog.accept())
  await activeAdCard.getByRole('button',{name:'Deactivate Ad'}).click()
  // Deactivation refreshes the Active Ads query and remounts the section, so
  // the transient success message is not a stable synchronization point.
  await expect(page.getByRole('button',{name:'Deactivate Ad'})).toHaveCount(0,{timeout:30000})
  await page.getByRole('button',{name:'Find Creators'}).click()
  await page.getByPlaceholder('Search Creator').fill(creator)
  const deactivatedSearchCard=page.locator('article').filter({hasText:creator})
  await expect(deactivatedSearchCard).toBeVisible({timeout:30000})
  await expect(deactivatedSearchCard).toContainText('Deactivated')
  await expect(deactivatedSearchCard.getByRole('button',{name:'Reactivate'})).toBeVisible()

  await page.evaluate(()=>localStorage.clear())
  await login(page,`creator-request-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  await expect(page.locator('.creator-ads-row.relationship-active').filter({hasText:business})).toHaveCount(0,{timeout:15000})

  await page.evaluate(()=>localStorage.clear())
  await login(page,`business-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Find Creators'}).click()
  const deactivatedCard=page.locator('article').filter({hasText:creator});
  const reactivate=deactivatedCard.getByRole('button',{name:'Reactivate'});
  await expect(reactivate).toBeVisible();await reactivate.scrollIntoViewIfNeeded();await reactivate.click()
  await expect(page.locator('article').filter({hasText:creator})).toContainText(/Currently Advertising|Active/)
})

test('Business invites a Creator and remains authoritative for activation',async({page},testInfo)=>{
  const n=suffix(testInfo.project.name),label=n==='1'?'Desktop':'Mobile',business=`${label} Workflow Business`,creator=`${label} Invite Creator`
  await login(page,`business-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Find Creators'}).click()
  const automaticCreatorCard=page.locator('article').filter({hasText:creator})
  await expect(automaticCreatorCard).toBeVisible()
  await expect(automaticCreatorCard.getByRole('button',{name:'Invite to Advertise'})).toBeVisible()
  await page.getByPlaceholder('Search Creator').fill(creator)
  const creatorCard=page.locator('article').filter({hasText:creator})
  await expect(creatorCard).toBeVisible()
  await creatorCard.getByRole('button',{name:'Invite to Advertise'}).click()
  await expect(page.getByText(`Invitation sent to ${creator}.`)).toBeVisible()
  await expect(creatorCard.getByText('Invitation Pending',{exact:true})).toBeVisible()
  await expect(creatorCard.getByRole('button',{name:'Invite to Advertise'})).toHaveCount(0)
  await page.getByRole('navigation',{name:'Business sections'}).getByRole('button',{name:'Requests',exact:true}).click()
  await expect(page.locator('article').filter({hasText:creator})).toContainText('Pending')

  await page.evaluate(()=>localStorage.clear())
  await login(page,`creator-invite-${n}@e2e.invalid`)
  await page.getByRole('navigation',{name:'Creator sections'}).getByRole('button',{name:'Requests',exact:true}).click()
  const invitation=page.locator('.request-groups article').filter({hasText:business})
  await expect(invitation).toContainText('Pending')
  await invitation.getByRole('button',{name:'Accept'}).click()
  await expect(page.getByRole('status')).toContainText(`Invitation from ${business} accepted.`)
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  await expect(page.locator('.creator-ads-row.relationship-active').filter({hasText:business}).first()).toContainText('Active')
  await expect(page.getByRole('button',{name:'Activate Ad'})).toHaveCount(0)

  await page.evaluate(()=>localStorage.clear())
  await login(page,`business-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Active Ads',exact:true}).click()
  const invitedCreator=page.locator('article').filter({hasText:creator})
  await expect(invitedCreator).toContainText('Active')
  page.once('dialog',dialog=>dialog.accept())
  await invitedCreator.getByRole('button',{name:'Deactivate Ad'}).first().click()
  await page.getByRole('button',{name:'Find Creators'}).click()
  await page.getByPlaceholder('Search Creator').fill(creator)
  await expect(page.getByText('Deactivated',{exact:true})).toBeVisible()
  await page.getByRole('button',{name:'Reactivate'}).first().click()
  await expect(page.getByText('Active',{exact:true})).toBeVisible()
})

test('Platform Admin loads, validates, and saves Financial Settings',async({page})=>{
  await login(page,'admin@e2e.invalid')
  await page.getByRole('link',{name:'Commission',exact:true}).click()
  await expect(page.getByRole('heading',{name:'Financial Settings'})).toBeVisible()
  await expect(page.getByLabel('Business Commission')).toHaveValue('10')
  await expect(page.getByText('Minimum Business Wallet Balance by Business Type')).toBeVisible()
  await expect(page.locator('.financial-settings-table tbody tr').first().getByRole('spinbutton')).toHaveValue(/^(0|1000|1200)$/)
  await page.getByRole('spinbutton',{name:'Creator %'}).fill('50')
  await page.getByRole('button',{name:'Save Settings'}).click()
  await expect(page.getByRole('status')).toContainText('must total 100%')
  await page.getByRole('spinbutton',{name:'Creator %'}).fill('40')
  page.once('dialog',dialog=>dialog.accept())
  await page.getByRole('button',{name:'Save Settings'}).click()
  await expect(page.getByRole('status')).toContainText('Financial settings saved.')
  await expect(page.getByText('Loading…')).toHaveCount(0)
})

test('Business submits separate payment proofs and Admin approves or rejects each independently',async({page},testInfo)=>{
  const n=suffix(testInfo.project.name)
  await login(page,`business-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Wallet',exact:true}).click()
  await expect(page.getByText('Available balance',{exact:true})).toBeVisible()
  for(const amount of ['250','125']){
    await page.getByLabel('Amount').fill(amount)
    await page.getByLabel('Upload Proof of Payment').setInputFiles({name:`payment-${amount}.png`,mimeType:'image/png',buffer:Buffer.from([137,80,78,71,13,10,26,10,0,0,0,0])})
    await page.getByRole('button',{name:'Submit Deposit'}).click()
    await expect(page.getByRole('status')).toContainText('Pending Review')
  }
  await expect(page.locator('.compact-deposits article')).toHaveCount(2)
  await expect(page.locator('.compact-deposits article').filter({hasText:'250'})).toContainText('Pending Review')
  await expect(page.locator('.compact-deposits article').filter({hasText:'125'})).toContainText('Pending Review')

  await page.evaluate(()=>localStorage.clear())
  await login(page,'admin@e2e.invalid')
  await page.locator('nav[aria-label="Admin navigation"] a[href="/admin/deposits"]').click()
  const businessName=`${n==='1'?'Desktop':'Mobile'} Workflow Business`
  const approved=page.locator('article').filter({hasText:businessName}).filter({hasText:'250'})
  const rejected=page.locator('article').filter({hasText:businessName}).filter({hasText:'125'})
  await expect(approved).toContainText('Pending Review')
  await expect(rejected).toContainText('Pending Review')
  await approved.getByRole('button',{name:'Approve'}).click()
  await expect(page.getByText('Deposit approved and wallet credited.')).toBeVisible()
  await expect(approved).toContainText('Approved')
  await expect(rejected).toContainText('Pending Review')
  page.once('dialog',dialog=>dialog.accept('Proof rejected in browser test'))
  await rejected.getByRole('button',{name:'Reject'}).click()
  await expect(page.getByText('Deposit rejected.')).toBeVisible()
  await expect(rejected).toContainText('Rejected')
  await page.getByRole('button',{name:'Sign out'}).click()
  await login(page,`business-${n}@e2e.invalid`)
  await page.getByRole('button',{name:'Wallet',exact:true}).click()
  await expect(page.getByText(/250\.00/).first()).toBeVisible()
})

test('Business creates a Cashier without requiring a location',async({page},testInfo)=>{
  const n=suffix(testInfo.project.name),cashierName=`${n==='1'?'Desktop':'Mobile'} Pilot Cashier`
  const password='Cashier-temp@123'
  await login(page,'owner@e2e.invalid')
  await page.getByRole('button',{name:'Settings'}).click()
  await page.getByRole('menu').getByRole('menuitem',{name:'Cashier Management',exact:true}).click()
  await page.getByRole('button',{name:'Create Cashier'}).click()
  await page.getByLabel('Cashier Name').fill(cashierName)
  await page.getByLabel('Phone Number').fill(`091177700${n}`)
  await page.getByRole('textbox',{name:'Temporary Password',exact:true}).fill(password)
  await page.getByRole('textbox',{name:'Confirm Temporary Password',exact:true}).fill(password)
  await page.getByRole('button',{name:'Create Cashier',exact:true}).click()
  await expect(page.getByText('Cashier account created.')).toBeVisible()
  const row=page.locator('article').filter({hasText:cashierName})
  await expect(row).toBeVisible()
  await expect(row).toContainText('Not assigned')
})
