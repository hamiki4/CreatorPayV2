import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import vm from 'node:vm'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const ts = require('typescript')
const React = require('react')
const ReactDOMServer = require('react-dom/server')

const auth=fs.readFileSync(new URL('../src/AuthWorkspace.tsx',import.meta.url),'utf8')
const creator=fs.readFileSync(new URL('../src/CreatorDashboard.tsx',import.meta.url),'utf8')
const chrome=fs.readFileSync(new URL('../src/AccountChrome.tsx',import.meta.url),'utf8')
const business=fs.readFileSync(new URL('../src/BusinessAdvertising.tsx',import.meta.url),'utf8')
const customer=fs.readFileSync(new URL('../src/CustomerWorkspace.tsx',import.meta.url),'utf8')
const profile=fs.readFileSync(new URL('../src/profileMedia.tsx',import.meta.url),'utf8')
const photoUpload=fs.readFileSync(new URL('../src/photoUpload.ts',import.meta.url),'utf8')
const styles=fs.readFileSync(new URL('../src/styles.css',import.meta.url),'utf8')
const nginx=fs.readFileSync(new URL('../nginx.conf',import.meta.url),'utf8')
const { renderNginxConfig, resolveApiOrigin } = await import(new URL('../scripts/render-nginx-config.mjs', import.meta.url))

function compileModule(fileUrl, replacements = {}, overrides = {}) {
  let source = fs.readFileSync(fileUrl, 'utf8')
  for (const [pattern, replacement] of Object.entries(replacements)) {
    const matcher = pattern.startsWith('/') && pattern.endsWith('/')
      ? new RegExp(pattern.slice(1, -1), 'm')
      : pattern
    source = source.replace(matcher, replacement)
  }
  const output = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
    fileName: new URL(fileUrl).pathname,
  }).outputText
  const module = { exports: {} }
  const sandbox = {
    module,
    exports: module.exports,
    require: (id) => (id in overrides ? overrides[id] : require(id)),
    __filename: new URL(fileUrl).pathname,
    __dirname: new URL('.', fileUrl).pathname,
    process,
    console,
    setTimeout,
    clearTimeout,
  }
  vm.runInNewContext(output, sandbox, { filename: new URL(fileUrl).pathname })
  return module.exports
}

test('creator profile photo controls stay on the authenticated Profile screen',()=>{
  for(const label of ['/api/v1/creators/me/profile-photo','Upload Photo','Change Photo','Remove Photo','ProfileAvatar','creatorPhotoUrl','prepareProfilePhoto','profilePhotoAccept'])assert.ok(creator.includes(label))
  assert.match(creator,/const loadProfile\s*=/)
  assert.match(creator,/const loadSupporting\s*=/)
  assert.match(creator,/creator-photo-actions/)
  assert.match(creator,/photoUrl=\{photo\}/)
  assert.match(creator,/ProfileAvatar name=\{profile\.displayName\} photoUrl=\{photo\}/)
  assert.doesNotMatch(creator,/Promise\.allSettled\(\[api<Profile>/)
  assert.doesNotMatch(auth,/profile-photo|Upload Photo|Change Photo|Remove Photo/i)
})

test('creator photos are reused in business and customer discovery cards',()=>{
  for(const label of ['profileImageUrl','creatorProfileImageUrl','creatorPhoneNumber','phoneNumber','ProfileAvatar','CreatorIdentity','creator-heading','creator-identity-text'])assert.ok(business.includes(label)||customer.includes(label))
  assert.match(business,/Find Creators/)
  assert.match(business,/Active Ads/)
  assert.match(business,/Requests/)
  assert.match(customer,/Discover Businesses/)
  assert.match(profile,/export const creatorPhotoUrl/)
  assert.match(creator,/AccountChrome[^]*photoUrl=\{photo\}/)
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
  assert.doesNotMatch(profile,/img src=\{photoUrl\}/)
})

test('creator header avatar reuses the same profile photo source as the profile card',()=>{
  assert.match(creator,/photoUrl=\{photo\}/)
  assert.match(creator,/role="Creator"/)
  assert.match(chrome,/account-identity-avatar/)
  assert.match(chrome,/account-identity--creator/)
  assert.match(chrome,/account-identity-text/)
  assert.match(chrome,/role==='Creator'\?<div className="account-identity account-identity--creator">/)
  assert.match(chrome,/<h1>\{role\}<\/h1><strong>\{name\?\?role\}<\/strong>/)
  assert.match(chrome,/account-status-dot/)
  assert.match(creator,/photoUrl=\{photo\}/)
  assert.match(styles,/\.account-identity--creator\{display:grid;grid-template-columns:auto minmax\(0,1fr\);align-items:center;column-gap:\.8rem;row-gap:\.15rem;flex-wrap:nowrap;margin:0\}/)
  assert.match(styles,/\.account-identity-text\{display:grid;gap:\.08rem;align-content:center;min-width:0\}/)
  assert.match(styles,/\.account-identity-avatar\{width:3rem;height:3rem;min-width:3rem;border:1px solid #c5d5ca;box-shadow:0 1px 1px rgb\(21 60 39\/\.05\)\}/)
  assert.match(styles,/\.account-identity-avatar\{width:2\.9rem;height:2\.9rem;min-width:2\.9rem\}/)
})

test('web csp is rendered from the configured api origin without broadening image sources',()=>{
  assert.match(nginx,/__IMG_SRC__/)
  assert.doesNotMatch(nginx,/api-pilot\.weymela\.com|api\.weymela\.com/)
  assert.equal(resolveApiOrigin('https://api-pilot.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg'),'https://api-pilot.weymela.com')
  assert.equal(resolveApiOrigin('https://api.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg'),'https://api.weymela.com')
  assert.equal(resolveApiOrigin('/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg'),'')
  const pilotRendered = renderNginxConfig(nginx, 'https://api-pilot.weymela.com')
  assert.match(pilotRendered,/img-src 'self' data: https:\/\/api-pilot\.weymela\.com;/)
  const prodRendered = renderNginxConfig(nginx, 'https://api.weymela.com')
  assert.match(prodRendered,/img-src 'self' data: https:\/\/api\.weymela\.com;/)
  const relativeRendered = renderNginxConfig(nginx, '/')
  assert.match(relativeRendered,/img-src 'self' data:;/)
})

test('profile photo helpers render the same current image across relative and absolute urls',()=>{
  const photoModule = compileModule(
    new URL('../src/photoUpload.ts', import.meta.url),
    { '/^const apiBase=.*$/': "const apiBase = 'https://api-pilot.weymela.com'" },
    { './apiClient': { apiBase: 'https://api-pilot.weymela.com' } },
  )
  const profileModule = compileModule(
    new URL('../src/profileMedia.tsx', import.meta.url),
    {},
    { './photoUpload': photoModule },
  )
  const { buildProfilePhotoUrl, normalizeProfilePhotoUrl } = photoModule
  const { ProfileAvatar, initials } = profileModule

  assert.equal(buildProfilePhotoUrl('CR-EFCD7528C632', 'file-one.jpg'), 'https://api-pilot.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg')
  assert.equal(buildProfilePhotoUrl('CR-EFCD7528C632', 'file-two.jpg'), 'https://api-pilot.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-two.jpg')
  assert.equal(normalizeProfilePhotoUrl('/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg'), 'https://api-pilot.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg')
  const absolute = 'https://api-pilot.weymela.com/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg'
  assert.equal(normalizeProfilePhotoUrl(absolute), absolute)
  assert.equal(normalizeProfilePhotoUrl(undefined), undefined)
  assert.equal(initials('Adonay Bekele'), 'AB')
  assert.equal(initials('Adonay'), 'A')

  const relativeMarkup = ReactDOMServer.renderToStaticMarkup(React.createElement(ProfileAvatar, { name: 'Adonay Bekele', photoUrl: '/api/v1/discovery/creators/CR-EFCD7528C632/photo?v=file-one.jpg' }))
  assert.match(relativeMarkup, /src="https:\/\/api-pilot\.weymela\.com\/api\/v1\/discovery\/creators\/CR-EFCD7528C632\/photo\?v=file-one\.jpg"/)

  const absoluteMarkup = ReactDOMServer.renderToStaticMarkup(React.createElement(ProfileAvatar, { name: 'Adonay Bekele', photoUrl: absolute }))
  assert.match(absoluteMarkup, /src="https:\/\/api-pilot\.weymela\.com\/api\/v1\/discovery\/creators\/CR-EFCD7528C632\/photo\?v=file-one\.jpg"/)

  const fallbackMarkup = ReactDOMServer.renderToStaticMarkup(React.createElement(ProfileAvatar, { name: 'Adonay Bekele' }))
  assert.match(fallbackMarkup, />AB</)
  assert.ok(!fallbackMarkup.includes('<img'))
})
