import test from 'node:test'
import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import {repoPath} from './repoPath.mjs'

const read=name=>readFileSync(new URL(`../src/${name}`,import.meta.url),'utf8')
const main=read('main.tsx')
const onboarding=read('ExternalOnboardingWorkspace.tsx')
const session=read('externalSession.ts')
const chrome=read('AccountChrome.tsx')
const admin=read('AdminPortal.tsx')
const pin=read('PinExperience.tsx')
const routing=read('authSession.ts')
const endpoints=readFileSync(repoPath(import.meta.url,'src/CreatorPay.Api/Integration/ExternalProductEndpoints.cs'),'utf8')
const apiHost=readFileSync(repoPath(import.meta.url,'src/CreatorPay.Api/Program.cs'),'utf8')

test('V3-managed routes preserve the existing product workspaces without a second login',()=>{
  for(const route of ['/onboarding/customer','/onboarding/creator','/onboarding/business']) assert.ok(main.includes(route),route)
  for(const route of ['/shopper','/creator','/business','/admin']) assert.ok(main.includes(route)||routing.includes(route),route)
  assert.match(main,/external\.integrationEnabled&&!external\.session/)
  assert.match(main,/location\.replace\(external\.authenticationUrl/)
  assert.match(session,/credentials: "include"/)
})

test('external onboarding keeps profile fields while excluding V2 authentication credentials',()=>{
  for(const label of ['Preferred name','Legal First Name',"Father's / Last Name",'Public Display Name','Primary City','Primary Social Platform','Social Profile URL','Estimated Follower Count','Trading Name','Business Type','Primary Contact Name','Business Address']) assert.ok(onboarding.includes(label),label)
  assert.doesNotMatch(onboarding,/Password|Confirm password|Create 5-digit PIN|Firebase|verification code|Public ID|User ID|Profile ID/)
  assert.match(onboarding,/do not create another login/)
})

test('V3External navigation exposes profile switching and coordinated logout',()=>{
  assert.match(chrome,/Switch profile/)
  assert.ok(chrome.indexOf('Switch profile')<chrome.indexOf('Sign out'))
  assert.match(admin,/Switch profile/)
  assert.match(admin,/signOutExternalSession/)
  assert.match(session,/switch-profile/)
  assert.match(session,/logout/)
})

test('legacy V2 PIN and Admin password tools are bypassed for external sessions',()=>{
  assert.match(pin,/isExternalSession\(\)/)
  assert.match(admin,/externalAuthPage/)
  assert.match(admin,/password-reset-requests/)
  assert.match(admin,/admin-accounts/)
})

test('external cookie mutations require exact product origin and an explicit browser request marker',()=>{
  assert.match(endpoints,/Fixed\(state, cookieState\)/)
  assert.match(endpoints,/SameSite = SameSiteMode\.Strict/)
  assert.match(endpoints,/HttpOnly = true/)
  assert.match(endpoints,/X-Weymela-Product-Request/)
  assert.match(session,/X-Weymela-Product-Request/)
  assert.match(onboarding,/api<\{ destination: string \}>/)
  assert.match(apiHost,/externalCookie && !callback/)
  assert.match(apiHost,/SameOrigin\(context\.Request\.Headers\.Origin/)
})
