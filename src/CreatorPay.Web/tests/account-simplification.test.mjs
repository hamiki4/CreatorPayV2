import test from 'node:test'
import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
const read=name=>readFileSync(new URL(`../src/${name}`,import.meta.url),'utf8')
const chrome=read('AccountChrome.tsx'),creator=read('CreatorDashboard.tsx'),business=read('BusinessDashboard.tsx'),confirmedSales=read('ConfirmedSalesWorkspace.tsx'),shopper=read('CustomerWorkspace.tsx'),auth=read('AuthWorkspace.tsx'),pin=read('PinExperience.tsx'),api=read('apiClient.ts'),lifecycle=read('mobileLifecycle.ts'),styles=read('styles.css'),actionable=read('actionableRefresh.ts'),adminPortal=read('AdminPortal.tsx')

test('all customer roles use compact accessible account chrome',()=>{for(const source of [creator,business,shopper])assert.match(source,/AccountChrome/);for(const label of ['aria-label="Notifications"','aria-label="Settings"','Profile','Help','Sign out'])assert.match(chrome,new RegExp(label));assert.match(chrome,/unread>0&&<span className="notification-count"/);assert.match(chrome,/className="signout-action"/);assert.match(styles,/min-width:2\.85rem/)})
test('settings menu closes from every shared authenticated account shell path',()=>{assert.match(chrome,/settingsRef/);assert.match(chrome,/pointerdown/);assert.match(chrome,/setSettingsOpen\(false\);onProfile\(\)/);assert.match(chrome,/setSettingsOpen\(false\);void enablePushNotifications\(/);assert.match(chrome,/setSettingsOpen\(false\);onHelp\(\)/);assert.match(chrome,/setSettingsOpen\(false\);onSignOut\(\)/);assert.match(chrome,/setSettingsOpen\(!settingsOpen\);setNotificationsOpen\(false\)/)})
test('notification read state and background refresh use the persisted API',()=>{for(const route of ['/api/v1/notifications?page=1&pageSize=30','/api/v1/notifications/unread-count','/read-all'])assert.ok(chrome.includes(route));assert.match(chrome,/setInterval\(\(\)=>\{[^}]+\},12_000\)/);for(const event of ['online','visibilitychange','weymela:app-resume'])assert.ok(chrome.includes(event)||lifecycle.includes(event));assert.match(chrome,/Mark all as read/)})
test('subsequent actionable notifications invalidate every mounted role workspace',()=>{assert.match(actionable,/weymela:actionable-refresh/);for(const source of [shopper,creator,business])assert.match(source,/onActionableRefresh/);assert.match(chrome,/requestActionableRefresh\(\)/);assert.match(chrome,/requestActionableRefresh\(\);const target/);assert.match(chrome,/requestActionableRefresh\(\);if\(!notificationsOpen\)/)})
test('role navigation and customer financial summaries are intentionally minimal',()=>{assert.match(creator,/Find Businesses/);assert.match(creator,/Active Ads/);assert.match(creator,/Requests/);assert.match(creator,/Confirmed Sales/);assert.match(creator,/Payout/);assert.doesNotMatch(creator,/\['overview'|\['creator-id'|\['profile'/);assert.match(creator,/Creator ID/);assert.doesNotMatch(creator,/Reserved Payout|Last Payout Date/);assert.match(business,/Find Creators/);assert.match(business,/Active Ads/);assert.match(business,/Requests/);assert.match(business,/Confirmed Sales/);assert.match(business,/Checkout/);assert.match(business,/Wallet/);assert.doesNotMatch(business,/\['overview'|\['profile'/);assert.match(confirmedSales,/Cashier/);assert.doesNotMatch(confirmedSales,/Processed By|Actor Name|<th>Actor<\/th>/);assert.match(chrome,/Cashier Management/);assert.match(business,/onManagement=\{\(\)=>setTab\('cashiers'\)\}/);assert.match(shopper,/Discover Businesses/);assert.match(shopper,/Cashback/);const discovery=shopper.slice(shopper.indexOf("view==='discover'"),shopper.indexOf("view==='confirmations'")),summary=shopper.slice(shopper.indexOf('function Summary'),shopper.indexOf('function ShopperProfileCard'));assert.doesNotMatch(discovery,/data-label="Status"|data-label="Days Left"/);assert.doesNotMatch(summary,/Reserved Payout|Last Payout Date|Confirmed Purchases/)})
test('registration presentation no longer collects birth date or legal business name',()=>{const registration=auth.slice(auth.indexOf("mode==='customer'"));assert.doesNotMatch(registration,/birthFields\(shopper|birthFields\(creator|birthFields\(business|Legal Business Name/);assert.match(auth,/Sign Up to Weymela/);assert.match(pin,/Create 5-digit PIN/);assert.doesNotMatch(pin,/Create your 5-digit PIN/)})
test('shared API client refreshes once before ending an expired session',()=>{assert.match(api,/response\.status===401&&retry&&await refreshAccessToken\(\)/);assert.match(shopper,/api as request/);assert.doesNotMatch(shopper,/handleUnauthorized/)})
test('admin account deletion keeps the delete action and success copy wired in the frontend',()=>{
  assert.match(adminPortal,/action\(x\.id, "delete"\)/)
  assert.match(adminPortal,/Account deleted and anonymized\./)
  assert.match(adminPortal,/Unable to change this account\./)
  assert.match(adminPortal,/delete/);
})

test('admin cleanup keeps password reset tools and business type correction wired in the frontend',()=>{
  assert.match(adminPortal,/password-reset-requests/)
  assert.match(adminPortal,/Delete Request/)
  assert.match(adminPortal,/Edit Business Type/)
  assert.match(adminPortal,/allowBusinessTypeEdit=\{role === "PlatformAdmin"\}/)
  assert.doesNotMatch(adminPortal,/showPasswordResets/)
  assert.doesNotMatch(adminPortal,/showPasswordResets=\{true\}/)
  assert.match(adminPortal,/columns=\{\["name", "email", "phone", "role", "status", "isLocked", "lastLoginAtUtc"\]\}/)
  assert.match(adminPortal,/columns=\{\["name", "email", "phone", "businessName", "publicBusinessId", "merchantBusinessType", "isLocked", "lastLoginAtUtc"\]\}/)
  assert.doesNotMatch(adminPortal,/columns=\{\["name", "email", "phone", "businessName", "publicBusinessId", "merchantBusinessType", "status", "isLocked", "lastLoginAtUtc"\]\}/)
  assert.match(adminPortal,/columns=\{\["name", "publicCreatorId", "email", "phone", "status", "isLocked", "lastLoginAtUtc"\]\}/)
  assert.match(adminPortal,/columns=\{\["name", "publicCustomerId", "email", "phone", "status", "isLocked", "lastLoginAtUtc"\]\}/)
  assert.doesNotMatch(adminPortal,/walletBalance/)
  assert.doesNotMatch(adminPortal,/fundingStatus/)
  assert.doesNotMatch(adminPortal,/isEmailVerified/)
  assert.doesNotMatch(adminPortal,/isPhoneVerified/)
})
