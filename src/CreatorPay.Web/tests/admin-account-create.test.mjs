import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

const admin=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
const create=await readFile(new URL('../src/AdminAccountCreate.tsx',import.meta.url),'utf8')
const auth=await readFile(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')

test('platform admin accounts page exposes create account workflow',()=>{
  assert.match(admin,/AdminAccountCreate/)
  assert.match(create,/Create Account/)
  for(const role of ['PlatformAdmin','OperationsAdmin','MerchantAdmin \/ Business','Cashier','Creator','Customer']) assert.match(create,new RegExp(role))
  for(const businessType of ['Restaurant / Café','Grocery / Mini-market','Clothing / Boutique','Beauty / Salon','Furniture','Electronics','Hotel / Travel','Professional Services','Other']) assert.match(auth,new RegExp(businessType))
  assert.match(create,/businessTypes\.map/)
  assert.match(create,/Business type[\s\S]*<select/)
  assert.match(create,/title,\s*description,\s*roles,\s*fixedRole,\s*onCreated/)
  assert.match(create,/showAdminFields = role === "PlatformAdmin" \|\| role === "OperationsAdmin"/)
  assert.match(create,/showRoleSelect = !fixedRole && roles.length > 1/)
  assert.match(create,/Full Name[\s\S]*autoComplete="name"/)
  assert.match(create,/PlatformAdmin: "Platform Admin"/)
  assert.match(create,/OperationsAdmin: "Operations Admin"/)
  assert.match(admin,/roles=\{\["PlatformAdmin", "OperationsAdmin"\]\}/)
  assert.match(admin,/createRoles=\{\["PlatformAdmin", "OperationsAdmin"\]\}/)
  assert.match(admin,/showBusinessFilter=\{false\}/)
  assert.match(admin,/showRoleFilter/)
  assert.match(admin,/All Roles/)
  assert.doesNotMatch(create,/Birth Date|Date of Birth|DOB|Verify Email|Verify Phone|Phone Verified|Email Verified/)
  assert.match(create,/Business/)
})

test('normal admin create forms start empty and use examples only as placeholders',()=>{
  for(const field of ['email','phoneNumber','password','confirmation','firstName','lastName','displayName','legalBusinessName','tradingName','businessType','primaryContactName','businessAddress','city','region','country','timeZone','preferredLanguage']) {
    assert.match(create,new RegExp(`${field}: ""`))
  }
  for(const placeholder of ['name@example.com','Addis Ababa','Ethiopia','Africa/Addis_Ababa','en']) assert.match(create,new RegExp(`placeholder="${placeholder}"`))
  assert.match(create,/Select a Business Type/)
  assert.match(create,/autoComplete="new-password"/)
  assert.match(create,/type="email"/)
  assert.match(create,/type="tel"/)
  assert.doesNotMatch(create,/email: "admin@weymela\.com"/)
  assert.doesNotMatch(create,/password: "[^\"]+"/)
})
