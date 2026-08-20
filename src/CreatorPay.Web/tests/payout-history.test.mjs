import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'
import {repoPath} from './repoPath.mjs'

const api=readFileSync(repoPath(import.meta.url,'src/CreatorPay.Api/Earnings/EarningsEndpoints.cs'),'utf8')
const ui=readFileSync(new URL('../src/EarningsWorkspace.tsx',import.meta.url),'utf8')

test('PlatformAdmin payout history uses persisted creator and shopper records with filters and paging',()=>{
  assert.match(api,/payout-history\/creators/)
  assert.match(api,/payout-history\/shoppers/)
  assert.match(api,/CreatorPayouts/)
  assert.match(api,/CustomerPayoutRequests/)
  assert.match(api,/Skip\(\(page - 1\) \* pageSize\)\.Take\(pageSize\)/)
  assert.match(api,/totalPaid/)
  assert.match(ui,/payout-history\//)
  assert.match(ui,/Cycle Start/)
  assert.match(ui,/Paid Amount/)
  assert.match(ui,/totalPaidAmount/)
  assert.match(ui,/type="date"/)
})

test('history filters do not replace the current-cycle report',()=>{
  assert.match(ui,/Current \{tab === "creators" \? "Creator" : "Shopper"\} Cycle/)
  assert.match(ui,/PayoutHistoryTable/)
})
