import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const read=(path)=>readFileSync(new URL(`../src/${path}`,import.meta.url),'utf8')
const accountChrome=read('AccountChrome.tsx')
const customer=read('CustomerWorkspace.tsx')
const creator=read('CreatorDashboard.tsx')
const business=read('MerchantWorkspace.tsx')
const cashier=read('CashierCheckoutWorkspace.tsx')
const onboarding=read('OnboardingStatus.tsx')
const styles=read('styles.css')

test('effective account status badge shows active and inactive with the approved dot styling',()=>{
  assert.match(accountChrome,/AccountStatusBadge/)
  assert.match(accountChrome,/●/)
  assert.match(accountChrome,/account-status--\$\{active\?'active':'inactive'\}/)
  assert.match(styles,/\.account-status--active\{color:#176b46\}/)
  assert.match(styles,/\.account-status--inactive\{color:#a12f25\}/)
})

test('customer discovery and role headers use effective status and simplified wording',()=>{
  assert.match(customer,/Discover Promotions/)
  assert.match(customer,/No active promotions found\./)
  assert.match(customer,/Search business or creator/)
  assert.match(customer,/effectiveStatus/)
  assert.match(customer,/AccountStatusBadge status=\{profile\.effectiveStatus\}/)
  assert.match(creator,/AccountStatusBadge status=\{profile\.effectiveStatus\}/)
  assert.match(business,/AccountStatusBadge status=\{profile\.effectiveStatus\}/)
  assert.match(cashier,/AccountStatusBadge status=\{staff\.effectiveStatus\}/)
  assert.match(onboarding,/effectiveStatus \?\?/)
})

test('creator and business profile status no longer depend on raw account status labels',()=>{
  assert.doesNotMatch(creator,/statusLabel\(profile\.status\)/)
  assert.doesNotMatch(business,/statusLabel\(business\?\.status\)/)
  assert.doesNotMatch(customer,/statusLabel\(profile\.status\)/)
  assert.doesNotMatch(cashier,/statusLabel\(staff\.status\)/)
})
