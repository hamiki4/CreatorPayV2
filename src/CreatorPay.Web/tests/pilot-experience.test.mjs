import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
const experience=fs.readFileSync(new URL('../src/PilotExperience.tsx',import.meta.url),'utf8')
const main=fs.readFileSync(new URL('../src/main.tsx',import.meta.url),'utf8')
const admin=fs.readFileSync(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
const styles=fs.readFileSync(new URL('../src/styles.css',import.meta.url),'utf8')
const e2e=fs.readFileSync(new URL('../e2e/shopper.spec.ts',import.meta.url),'utf8')
test('first-run tours cover every pilot role in English and Amharic',()=>{for(const role of ['Shopper','Creator','Business Owner','Cashier','Supervisor']){assert.ok(experience.includes(`${role}:`)||experience.includes(`'${role}':`));assert.ok(main.includes(`role="${role}"`)||main.includes(`role={user.role}`)||main.includes(`?'Creator':'Business Owner'`))}assert.match(experience,/Locale='en'\|'am'/);assert.match(experience,/weymela_tour_v32_/)} )
test('shared assistance covers offline, timeout, toast, contextual help, and anonymous analytics',()=>{for(const item of ['navigator.onLine','creatorpay_refresh_token','weymela:toast','context_help_opened','weymela:ux'])assert.ok(experience.includes(item));assert.doesNotMatch(experience,/analytics.*(?:email|phone|displayName)/i)})
test('feedback and post-checkout survey use protected pilot APIs',()=>{assert.match(experience,/\/api\/v1\/pilot\/feedback/);assert.match(experience,/\/api\/v1\/pilot\/checkout-survey/);assert.match(experience,/status==='Completed'/);assert.ok(admin.includes("'pilot-feedback':'pilot-feedback'"))})
test('accessibility and responsive safeguards are present',()=>{for(const item of ['aria-modal="true"','aria-live="polite"','aria-pressed','role="alert"'])assert.ok(experience.includes(item));assert.match(styles,/prefers-reduced-motion:reduce/);assert.match(styles,/forced-colors:active/);assert.match(styles,/@media\(max-width:600px\)/)})
test('Shopper E2E uses the production tour dismissal and persistence state',()=>{assert.match(e2e,/getByRole\('dialog',\{name:'Welcome to Weymela'\}\)/);assert.match(e2e,/getByRole\('button',\{name:'Not now'\}\)\.click/);assert.match(e2e,/weymela_tour_v32_Shopper/);assert.match(e2e,/localStorage\.getItem/);assert.match(e2e,/page\.reload/);assert.match(e2e,/Purchase Confirmations/)})
