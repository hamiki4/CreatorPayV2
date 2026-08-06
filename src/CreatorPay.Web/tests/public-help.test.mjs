import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
const page=fs.readFileSync(new URL('../src/PublicPages.tsx',import.meta.url),'utf8')
const main=fs.readFileSync(new URL('../src/main.tsx',import.meta.url),'utf8')
const auth=fs.readFileSync(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
test('public help, legal, and support routes are unauthenticated',()=>{for(const path of ["'/help'","'/contact'","'/terms'","'/privacy'"])assert.ok(main.includes(path));const root=main.slice(main.indexOf('function Root()'));assert.ok(root.indexOf("'/help'")<root.indexOf('const user=claims()'))})
test('help has English and Amharic content and filtering',()=>{assert.match(page,/What is Weymela/);assert.match(page,/ወይሜላ ምንድን ነው/);assert.match(page,/category-filter/);assert.match(page,/type="search"/);assert.doesNotMatch(page,/\b(?:4%|3%|commission split)\b/i)})
test('registration exposes legal links and role help mappings exist',()=>{assert.match(auth,/href="\/terms"/);assert.match(auth,/href="\/privacy"/);for(const category of ['Shoppers','Content Creators','Businesses','Cashiers and Supervisors'])assert.ok(main.includes(category))})
test('support form includes required consent and safe API handling',()=>{for(const field of ['name','contact','userType','subject','message','preferredLanguage','consentAcknowledged'])assert.ok(page.includes(field));assert.match(page,/status===429/);assert.match(page,/referenceNumber/)})
