import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const auth=fs.readFileSync(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const creator=fs.readFileSync(new URL('../src/CreatorDashboard.tsx',import.meta.url),'utf8')
const business=fs.readFileSync(new URL('../src/BusinessAdvertising.tsx',import.meta.url),'utf8')
const customer=fs.readFileSync(new URL('../src/CustomerWorkspace.tsx',import.meta.url),'utf8')
const profile=fs.readFileSync(new URL('../src/profileMedia.tsx',import.meta.url),'utf8')
const photoUpload=fs.readFileSync(new URL('../src/photoUpload.ts',import.meta.url),'utf8')

test('creator profile photo controls stay on the authenticated Profile screen',()=>{
  for(const label of ['/api/v1/creators/me/profile-photo','Upload Photo','Change Photo','Remove Photo','ProfileAvatar','creatorPhotoUrl','prepareProfilePhoto','profilePhotoAccept'])assert.ok(creator.includes(label))
  assert.match(creator,/const loadProfile\s*=/)
  assert.match(creator,/const loadSupporting\s*=/)
  assert.doesNotMatch(creator,/Promise\.allSettled\(\[api<Profile>/)
  assert.doesNotMatch(auth,/profile-photo|Upload Photo|Change Photo|Remove Photo/i)
})

test('creator photos are reused in business and customer discovery cards',()=>{
  for(const label of ['profileImageUrl','creatorProfileImageUrl','creatorPhoneNumber','phoneNumber','ProfileAvatar','creator-heading'])assert.ok(business.includes(label)||customer.includes(label))
  assert.match(business,/Find Creators/)
  assert.match(business,/Active Ads/)
  assert.match(business,/Requests/)
  assert.match(customer,/Discover Businesses/)
  assert.match(profile,/export const creatorPhotoUrl/)
  assert.match(creator,/identityMedia=/)
})

test('social media and photo controls remain safe and bounded',()=>{
  assert.doesNotMatch(business,/javascript:|data:/)
  assert.match(business,/target="_blank"/)
  assert.match(business,/rel="noopener noreferrer"/)
  assert.match(business,/Not provided/)
  assert.match(business,/social-media-link/)
  assert.doesNotMatch(business,/↗/)
  assert.doesNotMatch(business,/Social profile available/)
  assert.match(customer,/ProfileAvatar/)
  assert.match(creator,/creator-heading/)
  assert.match(profile,/onError=\{\(\)=>setFailed\(true\)\}/)
  assert.match(profile,/useEffect\(\(\)=>\{setFailed\(false\)\},\[photoUrl\]\)/)
  assert.match(profile,/buildProfilePhotoUrl/)
  assert.match(profile,/normalizeProfilePhotoUrl/)
  assert.match(photoUpload,/profilePhotoMaxUploadBytes/)
  assert.match(photoUpload,/profilePhotoMaxDimension/)
  assert.match(photoUpload,/profilePhotoProcessingMessage/)
  assert.match(photoUpload,/profilePhotoUnsupportedMessage/)
  assert.match(photoUpload,/image\/heic/)
  assert.match(photoUpload,/createImageBitmap/)
})

test('photo upload helper normalizes supported mobile photos before sending them',()=>{
  assert.match(photoUpload,/isSupportedProfilePhoto/)
  assert.match(photoUpload,/new File\(\[blob\].*\.jpg/)
  assert.match(photoUpload,/canvas\.toBlob/)
  assert.match(photoUpload,/imageOrientation/)
  assert.match(photoUpload,/buildProfilePhotoUrl/)
  assert.match(photoUpload,/normalizeProfilePhotoUrl/)
  assert.match(photoUpload,/apiBase/)
  assert.doesNotMatch(photoUpload,/This section is temporarily unavailable\./)
})

test('shared avatar component rewrites relative creator photo urls to the pilot api origin',()=>{
  assert.match(profile,/normalizeProfilePhotoUrl\(photoUrl\)/)
  assert.match(profile,/resolvedPhotoUrl/)
  assert.match(profile,/img src=\{resolvedPhotoUrl\}/)
  assert.match(profile,/loading="eager"/)
  assert.match(profile,/referrerPolicy="no-referrer"/)
  assert.doesNotMatch(profile,/img src=\{photoUrl\}/)
})
