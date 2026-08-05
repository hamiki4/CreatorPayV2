import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import test from 'node:test'
const source=await readFile(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const viteConfiguration=await readFile(new URL('../vite.config.ts',import.meta.url),'utf8')

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
