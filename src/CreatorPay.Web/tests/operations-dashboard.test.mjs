import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

test('Admin navigation reflects Promotions, UGC, governed roles, and Financial Settings',async()=>{
  const source=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
  for(const item of ['Dashboard','Reports','Creator Review','Business Review','Promotions','UGC','Business Accounts','Creator Accounts','Customer Accounts','Cashier Accounts','Admin Accounts','Financial Settings','Deposits / Funding','Wallets','Payouts','Platform Revenue','Fraud','Notifications','Audit / System']) assert.match(source,new RegExp(item))
  for(const route of ['creator-review','business-review','promotions','ugc','admin-accounts','financial-settings','deposits','wallets','payouts','platform-revenue','notifications','audit']) assert.match(source,new RegExp(route))
  assert.match(source,/const operationsBlockedPages: PageId\[] = \["dashboard", "reports", "admin-accounts", "financial-settings", "platform-revenue", "audit", "system"\]/)
  assert.match(source,/id: "financial-settings", label: "Financial Settings", roles: \["PlatformAdmin"\]/)
  assert.match(source,/id: "admin-accounts", label: "Admin Accounts", roles: \["PlatformAdmin"\]/)
  assert.match(source,/const visibleNav = platformNav\.filter/)
  assert.match(source,/const adminName = getExternalSession\(\)\?\.displayName/)
  assert.match(source,/const navRoleLabel = role === "PlatformAdmin" \? "Platform" : "Operations"/)
  assert.doesNotMatch(source,/id: "commission"|label: "Commission"/)
  assert.doesNotMatch(source,/Public ID or correlation ID/)
  assert.match(source,/Search accounts, Businesses, Creators, or reference/)
})

test('Admin account lists do not expose public identifiers or create V2-authenticated external identities',async()=>{
  const source=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
  for(const columns of [
    'columns={["name", "email", "phone", "businessName", "merchantBusinessType", "isLocked", "lastLoginAtUtc"]}',
    'columns={["name", "email", "phone", "status", "isLocked", "lastLoginAtUtc"]}',
    'columns={["name", "email", "phone", "businessName", "assignedLocation", "status", "isLocked", "lastLoginAtUtc"]}',
  ]) assert.ok(source.includes(columns),columns)
  assert.doesNotMatch(source,/columns=\{\[[^\]]*public(?:Business|Creator|Customer)Id/)
  assert.match(source,/allowRemove=\{false\}/)
  assert.match(source,/allowBusinessTypeEdit=\{role === "PlatformAdmin"\}/)
  assert.match(source,/!isExternalSession\(\) \|\| item\.id !== "password-reset-requests"/)
  assert.match(source,/<V3AdminAccounts \/>/)
})

test('Admin review surfaces group verified account, profile, and multi-social information',async()=>{
  const source=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
  for(const label of ['Creator profile','Account verification','Social profiles','Self-reported','Business profile','Contact and account verification','Approve','Request correction','Reject']) assert.match(source,new RegExp(label))
  assert.match(source,/Edit Business Type/)
  assert.match(source,/Delete Request/)
})
