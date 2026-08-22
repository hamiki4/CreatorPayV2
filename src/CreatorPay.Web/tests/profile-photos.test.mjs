import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const auth=fs.readFileSync(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const creator=fs.readFileSync(new URL('../src/CreatorDashboard.tsx',import.meta.url),'utf8')
const business=fs.readFileSync(new URL('../src/BusinessAdvertising.tsx',import.meta.url),'utf8')
const customer=fs.readFileSync(new URL('../src/CustomerWorkspace.tsx',import.meta.url),'utf8')
const profile=fs.readFileSync(new URL('../src/profileMedia.tsx',import.meta.url),'utf8')

test('creator profile photo controls stay on the authenticated Profile screen',()=>{
  for(const label of ['/api/v1/creators/me/profile-photo','Upload Photo','Change Photo','Remove Photo','ProfileAvatar','creatorPhotoUrl'])assert.ok(creator.includes(label))
  assert.doesNotMatch(auth,/profile-photo|Upload Photo|Change Photo|Remove Photo/i)
})

test('creator photos are reused in business and customer discovery cards',()=>{
  for(const label of ['profileImageUrl','creatorProfileImageUrl','ProfileAvatar','creator-heading'])assert.ok(business.includes(label)||customer.includes(label))
  assert.match(business,/Find Creators/)
  assert.match(business,/Active Ads/)
  assert.match(business,/Requests/)
  assert.match(customer,/Discover Businesses/)
  assert.match(profile,/export const creatorPhotoUrl/)
})

test('social media and photo controls remain safe and bounded',()=>{
  assert.doesNotMatch(business,/javascript:|data:/)
  assert.match(business,/target="_blank"/)
  assert.match(business,/rel="noopener noreferrer"/)
  assert.match(business,/Not provided/)
  assert.match(customer,/ProfileAvatar/)
  assert.match(creator,/creator-heading/)
})
