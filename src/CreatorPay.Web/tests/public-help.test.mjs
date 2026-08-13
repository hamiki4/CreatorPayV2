import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
const page=fs.readFileSync(new URL('../src/PublicPages.tsx',import.meta.url),'utf8')
const main=fs.readFileSync(new URL('../src/main.tsx',import.meta.url),'utf8')

test('public help, legal, and support routes remain unauthenticated',()=>{const compact=main.replace(/\s+/g,'');for(const path of ['/help','/contact','/terms','/privacy'])assert.ok(compact.includes(path));const root=compact.slice(compact.indexOf('functionRoot()'));assert.ok(root.indexOf('/help')<root.indexOf('constuser=claims()'))})
test('help content is English-only and uses Business terminology',()=>{assert.match(page,/What is Weymela/);assert.match(page,/Find Businesses/);assert.doesNotMatch(page,/Merchant Partnership|[\u1200-\u137f]/)})
test('support form retains consent, references, and safe failures',()=>{for(const field of ['name','contact','userType','subject','message','consentAcknowledged','referenceNumber'])assert.ok(page.includes(field));assert.match(page,/temporarily unavailable/i)})
