import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import test from 'node:test'

const read = (path) => readFile(new URL(`../${path}`, import.meta.url), 'utf8')
const [styles,businessAds,businessDashboard,creatorAds,creatorDashboard,customer,cashier,help,admin,partnerships,earnings]=await Promise.all([
  read('src/styles.css'),read('src/BusinessAdvertising.tsx'),read('src/BusinessDashboard.tsx'),read('src/CreatorAdvertising.tsx'),read('src/CreatorDashboard.tsx'),read('src/CustomerWorkspace.tsx'),read('src/CashierCheckoutWorkspace.tsx'),read('src/PublicPages.tsx'),read('src/AdminPortal.tsx'),read('../CreatorPay.Api/Partnerships/PartnershipEndpoints.cs'),read('../CreatorPay.Infrastructure/Earnings/CreatorEarningsService.cs'),
])

test('Business request tabs have explicit readable selected and unselected colors',()=>{
  assert.match(styles,/business-request-tabs button\[aria-selected="true"\][\s\S]*background:#2563eb !important;[\s\S]*color:#fff !important/)
  assert.match(styles,/business-request-tabs button\[aria-selected="false"\][\s\S]*color:#334155 !important/)
  assert.match(businessAds,/aria-selected=\{section === "creator"\}/)
  assert.match(businessAds,/aria-selected=\{section === "video"\}/)
})

test('normal account UI avoids opaque Public ID and unnecessary Creator ID terminology',()=>{
  for(const source of [businessAds,businessDashboard,creatorAds,creatorDashboard,customer,cashier,help])assert.doesNotMatch(source,/['"`]([^'"`]*\bpublic ID\b[^'"`]*)['"`]/i)
  assert.doesNotMatch(businessDashboard,/Creator ID/)
  assert.doesNotMatch(creatorDashboard,/Creator ID|creatorCode/)
  assert.doesNotMatch(customer,/Creator ID/)
  assert.match(cashier,/Creator ID/)
  assert.doesNotMatch(admin,/Public ID or correlation ID/)
  assert.match(admin,/Search accounts, Businesses, Creators, or reference/)
})

test('supported lifecycle notifications route to actionable role-safe destinations',()=>{
  assert.match(partnerships,/New Business invitation[\s\S]*\/\?view=requests&section=invitations/)
  assert.match(partnerships,/New Creator request[\s\S]*\/\?view=requests&section=creator/)
  assert.match(partnerships,/New promotion video waiting for approval\.[\s\S]*\/\?view=requests&section=video/)
  assert.match(partnerships,/PromotionVideoApproved[\s\S]*\/\?view=ads/)
  assert.match(partnerships,/PromotionVideoRejected[\s\S]*\/\?view=ads/)
  assert.match(earnings,/"TargetPath", "\/\?view=payout"/)
  assert.match(creatorAds,/My Requests/)
  assert.match(creatorAds,/Business Invitations/)
  assert.match(admin,/authorizedNotificationTarget/)
  assert.match(admin,/route\.roles\.includes\(role\)/)
})
