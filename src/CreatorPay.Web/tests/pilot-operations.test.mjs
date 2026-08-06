import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

test('pilot operations package defaults public and external money features off',async()=>{
  const config=await readFile(new URL('../../CreatorPay.Api/appsettings.Pilot.json',import.meta.url),'utf8')
  const pilot=JSON.parse(config)
  assert.equal(pilot.Pilot.Enabled,true)
  for(const flag of ['ExternalSms','ExternalEmail','ExternalPaymentProvider','AutomaticPayouts','PublicRegistration','PublicDiscovery']) assert.equal(pilot.FeatureFlags[flag],false)
  assert.equal(pilot.FeatureFlags.PlatformAdminOperations,true)
})
