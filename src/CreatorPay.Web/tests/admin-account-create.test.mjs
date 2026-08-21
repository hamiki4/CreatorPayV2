import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

const admin=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
const create=await readFile(new URL('../src/AdminAccountCreate.tsx',import.meta.url),'utf8')

test('platform admin accounts page exposes create account workflow',()=>{
  assert.match(admin,/AdminAccountCreate/)
  assert.match(create,/Create Account/)
  for(const role of ['PlatformAdmin','MerchantAdmin \/ Business','Cashier','Creator','Customer']) assert.match(create,new RegExp(role))
  assert.doesNotMatch(create,/Birth Date|Date of Birth|DOB|Verify Email|Verify Phone|Phone Verified|Email Verified/)
  assert.match(create,/Business/)
})
