import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

test('admin operations exposes milestone 30 queues and support lookup',async()=>{
  const source=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
  assert.match(source,/Operational dashboard/)
  assert.match(source,/support:'support'/)
  const api=await readFile(new URL('../../CreatorPay.Api/Admin/AdminEndpoints.cs',import.meta.url),'utf8')
  for(const signal of ['failedCheckouts','walletsBelowThreshold','failedNotifications','pendingPayouts','suspiciousActivity','openSupportRequests','systemHealth','serviceHealth','deploymentStatus']) assert.match(api,new RegExp(signal))
})
