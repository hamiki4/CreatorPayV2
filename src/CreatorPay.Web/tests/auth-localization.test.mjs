import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {spawnSync} from 'node:child_process'
import {fileURLToPath} from 'node:url'
import test from 'node:test'

const auth=await readFile(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const main=await readFile(new URL('../src/main.tsx',import.meta.url),'utf8')
const session=await readFile(new URL('../src/authSession.ts',import.meta.url),'utf8')
const notifications=await readFile(new URL('../src/NotificationWorkspace.tsx',import.meta.url),'utf8')
const deepLink=await readFile(new URL('../src/creatorQrDeepLink.ts',import.meta.url),'utf8')
const sw=new URL('../public/sw.js',import.meta.url)
const compact=value=>value.replace(/\s+/g,'')
const authCompact=compact(auth),mainCompact=compact(main),sessionCompact=compact(session),notificationsCompact=compact(notifications)

test('registration and login use the configured API base',()=>{
  assert.match(authCompact,/constbase=\(import\.meta\.env\.VITE_API_URL/)
  for(const endpoint of ['/api/v1/customers/register','/api/v1/creators/register','/api/v1/merchants/register','/api/v1/auth/login'])assert.ok(auth.includes(endpoint))
})

test('pilot authentication UI is English-only',()=>{
  for(const label of ['Shopper Registration','Content Creator Registration','Business Owner Registration','Confirm Password'])assert.ok(auth.includes(label))
  assert.doesNotMatch(auth,/locale|'am'|አማርኛ|ቋንቋ/)
})

test('registration validates passwords and Ethiopian phones',()=>{
  for(const rule of ['v.length>=8','/[A-Z]/.test(v)','/[a-z]/.test(v)','/\\d/.test(v)','/[^A-Za-z0-9]/.test(v)'])assert.ok(authCompact.includes(rule))
  assert.match(auth,/Passwords do not match\./)
  assert.match(auth,/\^0\[79\]\\d\{8\}\$/)
  assert.match(auth,/\^\\\+251\[79\]\\d\{8\}\$/)
  assert.match(authCompact,/const\{confirmation,platform,profileUrl,followerCount,\.\.\.request\}=creator/)
  assert.match(authCompact,/const\{confirmation,\.\.\.request\}=business/)
})

test('Shopper registration submits synchronized password confirmation exactly once',()=>{
  assert.match(auth,/new FormData\(e\.currentTarget\)/)
  assert.match(auth,/name="shopperPassword"/)
  assert.match(auth,/name="shopperConfirmation"/)
  assert.match(authCompact,/\.\.\.credentials,phoneNumber:p/)
  assert.match(authCompact,/if\(shopperSubmitting\.current\)return/)
  assert.doesNotMatch(auth,/const\{confirmation,\.\.\.request\}=shopper/)
})

test('authenticated roles route only to their assigned dashboard',()=>{
  for(const [role,path] of Object.entries({Customer:'/shopper',Creator:'/creator',MerchantAdmin:'/business',Supervisor:'/supervisor',Cashier:'/cashier',PlatformAdmin:'/admin'}))assert.ok(sessionCompact.includes(`${role}:'${path}'`)||sessionCompact.includes(`${role}:"${path}"`))
  assert.match(auth,/takeCreatorQrPath/)
  assert.match(auth,/location\.assign\(continuation\?\?workspaceRoute\(x\.user\.role\)\)/)
  assert.match(mainCompact,/isWorkspacePathAllowed\(user\.role,location\.pathname\)/)
})

test('signed Creator QR continuation is same-origin, route-limited, and Cashier-only',()=>{
  assert.match(deepLink,/^const publicIdPattern=/m)
  assert.match(deepLink,/tokenPattern/)
  assert.match(deepLink,/url\.origin===location\.origin/)
  assert.match(deepLink,/query\.getAll\('t'\)\.length!==1/)
  assert.match(main,/user\.role==='Cashier'/)
  assert.match(main,/Cashier checkout requires an active Cashier account\./)
})

test('authenticated sessions expire after two minutes of real user inactivity',()=>{
  assert.match(sessionCompact,/INACTIVITY_TIMEOUT_MS=120_000/)
  for(const event of ['pointerdown','pointermove','touchstart','keydown','scroll','click','popstate'])assert.ok(session.includes(`'${event}'`)||session.includes(`"${event}"`))
  assert.match(main,/installInactivityLogout/)
  assert.match(main,/handleUnauthorized\(response\.status\)/)
})

test('notifications use the shared configured base and reject non-JSON safely',()=>{
  assert.match(notificationsCompact,/constapiBase=\(import\.meta\.env\.VITE_API_URL/)
  assert.match(notifications,/content-type/)
  assert.match(notifications,/This section is temporarily unavailable/)
})

test('service worker JavaScript is valid',()=>{
  const result=spawnSync(process.execPath,['--check',fileURLToPath(sw)],{encoding:'utf8'})
  assert.equal(result.status,0,result.stderr)
})
