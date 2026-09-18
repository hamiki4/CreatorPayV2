import test from 'node:test'
import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import {repoPath} from './repoPath.mjs'

const read=name=>readFileSync(new URL(`../src/${name}`,import.meta.url),'utf8')
const main=read('main.tsx')
const onboarding=read('ExternalOnboardingWorkspace.tsx')
const onboardingStatus=read('OnboardingStatus.tsx')
const session=read('externalSession.ts')
const chrome=read('AccountChrome.tsx')
const admin=read('AdminPortal.tsx')
const pin=read('PinExperience.tsx')
const routing=read('authSession.ts')
const creator=read('CreatorDashboard.tsx')
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
  for(const label of ['Preferred name','Legal First Name',"Father's / Last Name",'Public Display Name','Primary City','Social profiles','TikTok','Instagram','YouTube','Facebook','Trading Name','Business Type','Primary Contact Name','Business Address']) assert.ok(onboarding.includes(label),label)
  assert.doesNotMatch(onboarding,/Password|Confirm password|Create 5-digit PIN|Firebase|verification code|Public ID|User ID|Profile ID/)
  assert.match(onboarding,/Account email/)
  assert.match(onboarding,/Account phone/)
  assert.match(onboarding,/readOnly/)
  assert.doesNotMatch(onboarding,/Primary Social Platform|Estimated Follower Count/)
  assert.match(onboarding,/Business Contact Phone/)
  assert.match(onboarding,/Business Contact Email/)
  assert.match(onboarding,/Back to profiles/)
  assert.match(onboarding,/href="\/onboarding"/)
  assert.match(onboarding,/Add social platform/)
  assert.match(creator,/Save social profiles/)
  assert.match(creator,/drafts\.length===1/)
})

test('V3External navigation exposes profile switching and coordinated logout',()=>{
  assert.match(chrome,/Switch profile/)
  assert.ok(chrome.indexOf('Switch profile')<chrome.indexOf('Sign out'))
  assert.match(admin,/Switch profile/)
  assert.match(admin,/signOutExternalSession/)
  assert.match(session,/switch-profile/)
  assert.match(session,/logout/)
  assert.match(session,/location\.replace\(value\.redirectUrl\)/)
  assert.match(session,/fetch\("\/api\/session\/sign-out"/)
  assert.match(session,/location\.replace\("\/sign-in"\)/)
  assert.doesNotMatch(session,/location\.assign\(value\.redirectUrl\)/)
  assert.match(onboarding,/location\.replace\(result\.destination\)/)
})

test('missing external profiles recover to V3 lifecycle routing before product status APIs run',()=>{
  assert.match(session,/externalOnboardingPath/)
  assert.match(session,/"\/onboarding\/creator"/)
  assert.match(session,/"\/onboarding\/business"/)
  assert.match(main,/externalOnboardingPath\(external\.session\)/)
  assert.match(main,/location\.replace\("\/onboarding"\)/)
  assert.match(onboardingStatus,/response\.status === 401 \|\| response\.status === 403/)
  assert.match(onboardingStatus,/getExternalSession\(\)\?\.isOnboarding/)
  assert.match(onboardingStatus,/location\.replace\('\/onboarding'\)/)
  assert.match(onboardingStatus,/CorrectionRequested/)
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
  assert.match(endpoints,/ProductRequest\(context, service\)\) return Results\.Ok\(new \{ state \}\)/)
  assert.match(endpoints,/ProductRequest\(context, service\)\) return Results\.Ok\(new \{ destination \}\)/)
})
