import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
const page=fs.readFileSync(new URL('../src/PublicPages.tsx',import.meta.url),'utf8')
const main=fs.readFileSync(new URL('../src/main.tsx',import.meta.url),'utf8')
const brand=fs.readFileSync(new URL('../src/brand.ts',import.meta.url),'utf8')

test('public help, legal, and support routes remain unauthenticated',()=>{const compact=main.replace(/\s+/g,'');for(const path of ['/help','/contact','/terms','/privacy'])assert.ok(compact.includes(path));const root=compact.slice(compact.indexOf('functionRoot()'));assert.ok(root.indexOf('/help')<root.indexOf('constuser=claims()'))})
test('public account deletion route and navigation are present',()=>{
  const compact=main.replace(/\s+/g,'')
  assert.ok(compact.includes('/delete-account'))
  assert.match(page,/Help Center[\s\S]*Contact Support[\s\S]*Terms[\s\S]*Privacy[\s\S]*Delete Account/)
  assert.doesNotMatch(page,/Sign In/)
  assert.doesNotMatch(page,/Search help|type="search"/i)
  for(const category of ['All','General','Content Creators','Businesses','Customers','Cashiers and Supervisors','Platform Admin','Operations Admin','Accounts']) assert.match(page,new RegExp(category))
  assert.match(page,/Delete Your Weymela Account/)
  assert.match(page,/brand\.supportEmail/)
  assert.match(brand,/support@weymela\.com/)
  assert.doesNotMatch(brand,/support@example\.com/)
})
test('help content matches Business-led Promotions, UGC, and governed roles',()=>{
  for(const text of ['How do I join a Promotion?','How does UGC work?','Create and fund a Promotion or UGC opportunity','Payment is not released merely because a link was uploaded','Platform Admin-only','one-time Offer QR'])assert.ok(page.includes(text),text)
  assert.match(page,/approval consumes only its configured capacity/)
  assert.match(page,/current Business interface does not expose a self-service early-end action/)
  assert.match(page,/current Creator interface does not provide a self-service early-end action/)
  assert.doesNotMatch(page,/Merchant Partnership|four-digit Creator ID|[\u1200-\u137f]/i)
})
test('support form retains consent, references, and safe failures',()=>{for(const field of ['name','contact','userType','subject','message','consentAcknowledged','referenceNumber'])assert.ok(page.includes(field));assert.match(page,/temporarily unavailable/i)})
