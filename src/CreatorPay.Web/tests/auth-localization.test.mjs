import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import {spawnSync} from 'node:child_process'
import {fileURLToPath} from 'node:url'
import test from 'node:test'
const source=await readFile(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const viteConfiguration=await readFile(new URL('../vite.config.ts',import.meta.url),'utf8')
const mainSource=await readFile(new URL('../src/main.tsx',import.meta.url),'utf8')
const sessionSource=await readFile(new URL('../src/authSession.ts',import.meta.url),'utf8')
const onboardingSource=await readFile(new URL('../src/OnboardingStatus.tsx',import.meta.url),'utf8')
const notificationSource=await readFile(new URL('../src/NotificationWorkspace.tsx',import.meta.url),'utf8')
const campaignSource=await readFile(new URL('../src/CampaignWorkspace.tsx',import.meta.url),'utf8')
const merchantSource=await readFile(new URL('../src/MerchantWorkspace.tsx',import.meta.url),'utf8')
const serviceWorkerUrl=new URL('../public/sw.js',import.meta.url)

test('all registration requests use the configured CreatorPay API base',()=>{
  assert.match(source,/const base=\(import\.meta\.env\.VITE_API_URL/)
  for(const endpoint of ['/api/v1/customers/register','/api/v1/creators/register','/api/v1/merchants/register'])assert.ok(source.includes(`post('${endpoint}'`),`missing configured request: ${endpoint}`)
  assert.match(viteConfiguration,/mode==='development'\?'http:\/\/localhost:5225':''/)
})

test('renders the bilingual language label in the selected language order',()=>{
  assert.match(source,/locale==='am'\?'ቋንቋ \/ Language':'Language \/ ቋንቋ'/)
})

test('uses clear English and Amharic public role terminology',()=>{
  for(const value of ['Shopper Registration','Content Creator Registration','Business Owner Registration','Create shopper account','Create content creator account','Create business owner account',"Father's Name",'Confirm Password'])assert.ok(source.includes(value),`missing English label: ${value}`)
  for(const value of ['የገበያተኛ ምዝገባ','የይዘት ፈጣሪ ምዝገባ','የንግድ ባለቤት ምዝገባ','የአባት ስም','የይለፍ ቃል ያረጋግጡ'])assert.ok(source.includes(value),`missing Amharic label: ${value}`)
})

test('validates confirmation and omits it from creator and business owner requests',()=>{
  assert.match(source,/function matches\(password:string,confirmation:string\)/)
  assert.match(source,/Passwords do not match\./)
  assert.match(source,/const\{confirmation,platform,profileUrl,followerCount,\.\.\.profile\}=creator/)
  assert.match(source,/const\{confirmation,\.\.\.request\}=merchant/)
})

test('applies all five live public password rules before requests',()=>{
  for(const rule of ['value.length>=8','/[A-Z]/.test(value)','/[a-z]/.test(value)','/\\d/.test(value)','/[^A-Za-z0-9]/.test(value)'])assert.ok(source.includes(rule),`missing password rule: ${rule}`)
  assert.match(source,/publicPasswordRules\.every\(rule=>rule\(password\)\)/)
  assert.match(source,/minLength=\{8\} maxLength=\{128\}/)
  assert.match(source,/aria-live="polite"/)
  assert.match(source,/results\[index\]\?'✓':'○'/)
})

test('includes English and Amharic checklist and match text',()=>{
  for(const value of ['Password requirements:','At least 8 characters','One uppercase letter','One lowercase letter','One number','One special character','Passwords match','Passwords do not match','የይለፍ ቃል መስፈርቶች፦','ቢያንስ 8 ቁምፊዎች','የይለፍ ቃሎቹ ይዛመዳሉ'])assert.ok(source.includes(value),`missing password copy: ${value}`)
})

test('tab switching resets forms and messages while language switching preserves them',()=>{
  assert.match(source,/function setMode\(next:Mode\).*setCustomer\(freshCustomer\(\)\).*setMessage\(''\)/s)
  assert.match(source,/setLocale\(v\)/)
  assert.doesNotMatch(source,/location\.reload/)
})

test('reduced registration forms retain required request defaults',()=>{
  assert.match(source,/biography:'',contentCategories:''/)
  assert.match(source,/region:'Not provided'/)
  for(const removedLabel of ['Zone or neighborhood','Short biography','Content categories','Social handle','Manual payout channel','Payout account identifier'])assert.ok(!source.includes(removedLabel),`removed field remains: ${removedLabel}`)
})

test('shows safe 400 and 409 details without a support id',()=>{
  assert.match(source,/typeof v\.detail==='string'/)
  assert.match(source,/if\(r\.status<500\)/)
  assert.match(source,/setMessage\(detail\?\?t\.failed\)/)
})

test('shows a generic 500 error with a correlation id',()=>{
  assert.match(source,/x-correlation-id/)
  assert.match(source,/typeof v\.correlationId==='string'/)
  assert.match(source,/setMessage\(`\$\{t\.failed\}\$\{correlation/)
})

test('validates and normalizes Ethiopian mobile numbers before every registration request',()=>{
  assert.match(source,/\^0\[79\]\\d\{8\}\$/)
  assert.match(source,/\^\\\+251\[79\]\\d\{8\}\$/)
  assert.match(source,/value\.replace\(\/\[\\s-\]\/g,''\)/)
  assert.match(source,/return `\+251\$\{compact\.slice\(1\)\}`/)
  assert.equal((source.match(/validPhone\(/g)??[]).length,4)
  for(const role of ['customer','creator','merchant'])assert.ok(source.includes(`validPhone(${role}.phoneNumber)`),`missing ${role} preflight validation`)
})

test('contains the exact English and natural Amharic Ethiopian phone messages',()=>{
  assert.ok(source.includes('Enter a valid Ethiopian mobile number, for example 0911234567, 0712345678, or +251911234567.'))
  assert.ok(source.includes('ትክክለኛ የኢትዮጵያ ሞባይል ቁጥር ያስገቡ፣ ለምሳሌ 0911234567፣ 0712345678 ወይም +251911234567።'))
})

test('renders one localized welcome heading followed by the tagline',()=>{
  assert.match(source,/<h1>\{t\.welcome\}<\/h1><p>\{brand\.tagline\}<\/p>/)
  assert.doesNotMatch(source,/<h2>/)
})

test('routes every authenticated role to only its assigned workspace',()=>{
  for(const [role,path] of Object.entries({Customer:'/shopper',Creator:'/creator',MerchantAdmin:'/business',Supervisor:'/supervisor',Cashier:'/cashier',PlatformAdmin:'/admin'}))assert.ok(sessionSource.includes(`${role}:'${path}'`),`missing ${role} route`)
  assert.match(source,/location\.assign\(workspaceRoute\(x\.user\.role\)\)/)
  assert.match(mainSource,/isWorkspacePathAllowed\(user\.role,location\.pathname\)/)
  assert.match(mainSource,/location\.replace\(workspaceRoute\(user\.role\)\)/)
})

test('refreshes expired access tokens once and clears auth state on logout',()=>{
  assert.match(mainSource,/\/api\/v1\/auth\/refresh/)
  assert.match(mainSource,/response\.status===401&&retry&&await refreshAccess\(\)/)
  assert.match(mainSource,/\/api\/v1\/auth\/logout/)
  for(const key of ['creatorpay_access_token','creatorpay_refresh_token'])assert.ok(sessionSource.includes(`removeItem('${key}')`))
})

test('does not expose public Platform Admin registration',()=>{
  assert.doesNotMatch(source,/Platform Admin Registration/)
  assert.doesNotMatch(source,/platform-admin.*register/i)
})

test('pending accounts see onboarding status instead of active operations',()=>{
  assert.match(mainSource,/active=user\.status==='Active'/)
  assert.match(mainSource,/!active\?<OnboardingStatus/)
  for(const path of ['/api/v1/auth/me','/api/v1/creators/me','/api/v1/merchants/me'])assert.ok(onboardingSource.includes(path),`missing onboarding endpoint ${path}`)
  assert.match(onboardingSource,/Email \{data\.isEmailVerified\?'verified':'not verified'\} · Phone/)
})

test('notification requests use the configured API base for every operation',()=>{
  assert.match(notificationSource,/const apiBase=\(import\.meta\.env\.VITE_API_URL/)
  assert.match(notificationSource,/fetch\(`\$\{apiBase\}\$\{path\}`/)
  for(const path of ['/api/v1/notifications?page=1&pageSize=50','/api/v1/notifications/unread-count','/api/v1/notifications/read-all','/api/v1/notifications/${x.notificationId}/read'])assert.ok(notificationSource.includes(path),`missing notification path ${path}`)
  assert.doesNotMatch(notificationSource,/NotificationPreferences|notification-preferences|pushEnabled/)
})

test('notification API rejects non-JSON responses with a safe message',()=>{
  assert.match(notificationSource,/response\.headers\.get\('content-type'\)/)
  assert.match(notificationSource,/if\(!contentType\.includes\('json'\)\)throw new Error\(invalidResponse\)/)
  assert.ok(notificationSource.includes('The notification service returned an invalid response. Please try again.'))
  assert.doesNotMatch(notificationSource,/await response\.json\(\)(?!\})/)
})

test('service worker is valid JavaScript and is disabled during Vite development',()=>{
  const result=spawnSync(process.execPath,['--check',fileURLToPath(serviceWorkerUrl)],{encoding:'utf8'})
  assert.equal(result.status,0,result.stderr)
  assert.match(mainSource,/if\(import\.meta\.env\.DEV\)/)
  assert.match(mainSource,/registration=>registration\.unregister\(\)/)
})

test('business type dropdown is bilingual and complete',()=>{
  for(const value of ['Restaurant / Café','Grocery / Mini-market','Clothing / Boutique','Beauty / Salon','Furniture','Electronics','Hotel / Travel','Professional Services','Other'])assert.ok(source.includes(value),`missing business type ${value}`)
  for(const value of ['ምግብ ቤት / ካፌ','ግሮሰሪ / ሚኒ ማርኬት','ውበት / ሳሎን','የቤት ዕቃ','ሙያዊ አገልግሎቶች'])assert.ok(source.includes(value),`missing Amharic business type ${value}`)
  assert.match(source,/<select required value=\{merchant\.businessType\}/)
  assert.match(merchantSource,/<select required value=\{businessType\}/)
  assert.ok(merchantSource.includes("request<Profile>('/api/v1/merchants/me','PUT'"))
  assert.ok(merchantSource.includes('(legacy value)'))
})

test('offer reuse suggestions are selectable and sent as the final rule',()=>{
  assert.match(campaignSource,/Restaurant \/ Café'.*Grocery \/ Mini-market'.*OncePerDay/)
  assert.match(campaignSource,/Beauty \/ Salon'.*OncePerWeek/)
  assert.match(campaignSource,/Furniture'.*OncePerOffer/)
  for(const value of ['OncePerDay','OncePerWeek','OncePerMonth','OncePerOffer','Unlimited'])assert.ok(campaignSource.includes(value),`missing reuse rule ${value}`)
  assert.match(campaignSource,/eligibleLocationIds:null,reuseRule/)
  assert.ok(campaignSource.includes('How often can the same Shopper use this Offer?'))
  assert.ok(campaignSource.includes('Suggested for your business type'))
})
