import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {repoPath} from './repoPath.mjs'

test('pilot operations package defaults public and external money features off',async()=>{
  const config=await readFile(repoPath(import.meta.url,'src/CreatorPay.Api/appsettings.Pilot.json'),'utf8')
  const pilot=JSON.parse(config)
  assert.equal(pilot.Pilot.Enabled,true)
  for(const flag of ['ExternalSms','ExternalEmail','ExternalPaymentProvider','AutomaticPayouts','PublicRegistration','PublicDiscovery']) assert.equal(pilot.FeatureFlags[flag],false)
  assert.equal(pilot.FeatureFlags.PlatformAdminOperations,true)
})
