import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const source=readFileSync(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')

test('Admin navigation reuses actionable dashboard counts with zero and 99+ rules',()=>{
  for(const key of ['pendingCreatorApprovals','pendingMerchantApprovals','pendingDeposits','pendingPayouts','openFraudAlerts','openSupportRequests'])assert.match(source,new RegExp(key))
  assert.match(source,/count > 99/)
  assert.match(source,/99\+/)
  assert.match(source,/admin-nav-badge/)
  assert.match(source,/Admin Accounts/)
  assert.doesNotMatch(source,/admin-accounts", label: "Admin Accounts", roles: \["PlatformAdmin"\], countKey: "openSupportRequests"/)
  assert.match(source,/password-reset-requests", label: "Password Reset Requests", roles: adminRoles, countKey: "openSupportRequests"/)
})

test('Admin badges refresh without logout and keep notification counts separate from support badges',()=>{
  assert.match(source,/dashboard\/summary/)
  assert.match(source,/setInterval\(refresh,\s*60_000\)/)
  assert.match(source,/addEventListener\("focus",\s*refresh\)/)
  assert.match(source,/visibilitychange",\s*refresh/)
  assert.match(source,/aria-label=\{aria\}/)
  assert.match(source,/notification-count/)
  assert.match(source,/aria-expanded=\{open\}/)
  assert.match(source,/Mark all as read/)
  assert.match(source,/notificationTarget/)
  assert.match(source,/SupportRequestReceived/)
  assert.match(source,/Platform Admin|Operations Admin/)
})
