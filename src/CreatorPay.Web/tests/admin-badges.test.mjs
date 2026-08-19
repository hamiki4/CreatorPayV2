import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const source=readFileSync(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')

test('Admin navigation reuses actionable dashboard counts with zero and 99+ rules',()=>{
  for(const key of ['pendingCreatorApprovals','pendingMerchantApprovals','pendingDeposits','pendingPayouts','openFraudAlerts'])assert.match(source,new RegExp(key))
  assert.match(source,/count>99\?'99\+':count/)
  assert.match(source,/count>0&&<span className="admin-nav-badge"/)
})

test('Admin badges refresh without logout and remain independent of notification read state',()=>{
  assert.match(source,/dashboard\/summary/)
  assert.match(source,/setInterval\(refresh,12000\)/)
  assert.match(source,/addEventListener\('focus',refresh\)/)
  assert.match(source,/visibilitychange'\s*,refresh/)
  assert.match(source,/aria-label=\{aria\}/)
  assert.doesNotMatch(source,/notification.*read.*count/i)
})
