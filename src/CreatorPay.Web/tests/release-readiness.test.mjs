import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'
import test from 'node:test'

const creator=await readFile(new URL('../src/CreatorAdvertising.tsx',import.meta.url),'utf8')
const chrome=await readFile(new URL('../src/AccountChrome.tsx',import.meta.url),'utf8')
const styles=await readFile(new URL('../src/styles.css',import.meta.url),'utf8')
const payouts=await readFile(new URL('../src/EarningsWorkspace.tsx',import.meta.url),'utf8')
const help=await readFile(new URL('../src/PublicPages.tsx',import.meta.url),'utf8')

test('Creator rejection feedback is readable and revision starts with a blank exact-video form',()=>{
  assert.match(creator,/Business feedback/)
  assert.match(creator,/promo\.rejectionReason\?\.trim\(\) \|\| 'Please contact the business for more information\.'/)
  assert.match(creator,/Revise &amp; Resubmit/)
  assert.match(creator,/video\?\.status === 'Rejected' \|\| video\?\.status === 'Expired' \? ''/)
  assert.match(creator,/Revise Promotion Video/)
  assert.match(styles,/\.creator-video-feedback/)
})

test('release touch safeguards preserve role focus, red unread badges, and selectable fields',()=>{
  assert.match(styles,/@media \(hover:none\), \(pointer:coarse\)/)
  assert.match(styles,/\.account-shell \.workspace-nav button\.is-active:hover:not\(:disabled\)/)
  assert.match(styles,/\.admin-sidebar nav a\[aria-current="page"\]:hover/)
  assert.match(styles,/input,textarea,\[contenteditable="true"\],code/)
  assert.match(styles,/-webkit-user-select:text/)
  assert.match(chrome,/className="notification-count"/)
  assert.match(styles,/\.account-shell \.notification-count[\s\S]*background: #d8322b !important/)
})

test('admin create fields and Customer payout filters are constrained responsively',()=>{
  assert.match(styles,/\.admin-create-card input,[\s\S]*max-width: 100%/)
  assert.match(styles,/\.payout-filter-bar \{[\s\S]*display: grid/)
  assert.match(styles,/\.payout-filter-actions \{display:flex/)
  assert.match(payouts,/className="filter-bar payout-filter-bar"/)
  assert.ok(payouts.indexOf('<label>From')<payouts.indexOf('className="payout-filter-actions"'))
  assert.ok(payouts.indexOf('<label>To')<payouts.indexOf('className="payout-filter-actions"'))
  assert.ok(payouts.indexOf('<label>Status')<payouts.indexOf('className="payout-filter-actions"'))
})

test('Help reflects the current Promotion and UGC lifecycle without obsolete partnership guidance',()=>{
  for(const expected of ['Discover Promotions','UGC work','Create and fund a Promotion or UGC opportunity','one-time Offer QR','Platform Admin-only']) assert.ok(help.includes(expected),expected)
  assert.doesNotMatch(help,/four-digit Creator ID|generic Creator request|Find Businesses/i)
  assert.match(help,/current Business interface does not expose a self-service early-end action/)
  assert.match(help,/current Creator interface does not provide a self-service early-end action/)
})
