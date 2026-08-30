import test from 'node:test'
import assert from 'node:assert/strict'
import {readFile} from 'node:fs/promises'

const portal=await readFile(new URL('../src/AdminPortal.tsx',import.meta.url),'utf8')
const styles=await readFile(new URL('../src/styles.css',import.meta.url),'utf8')
const earnings=await readFile(new URL('../src/EarningsWorkspace.tsx',import.meta.url),'utf8')

test('admin shell uses a role-safe mobile drawer and controlled outline icons',()=>{
  assert.match(portal,/admin-menu-toggle/)
  assert.match(portal,/admin-menu-scrim/)
  assert.match(portal,/admin-sidebar\$\{menuOpen \? " is-open"/)
  assert.match(portal,/visibleNav = platformNav\.filter/)
  assert.match(portal,/operationsBlockedPages/)
  assert.match(styles,/\.admin-sidebar\.is-open \{transform:translateX\(0\)\}/)
  assert.match(styles,/@media \(max-width:800px\)/)
  assert.match(styles,/width:min\(20rem,calc\(100vw - 2\.5rem\)\)/)
  assert.match(styles,/-webkit-touch-callout: none/)
  assert.match(styles,/@media \(hover:hover\) and \(pointer:fine\)/)
})

test('Platform dashboard metrics are whole-card links to existing destinations',()=>{
  const routes=[
    ['/admin/creator-review','pendingCreatorApprovals'],
    ['/admin/business-review','pendingMerchantApprovals'],
    ['/admin/deposits','pendingDeposits'],
    ['/admin/creator-accounts?status=Active','activeCreators'],
    ['/admin/business-accounts?status=Active','activeBusinesses'],
    ['/admin/payouts','pendingPayouts'],
    ['/admin/fraud','openFraudAlerts'],
    ['/admin/fraud','failedCheckouts'],
    ['/admin/system','systemHealth'],
  ]
  for(const [route,key] of routes) {
    assert.match(portal,new RegExp(`${key}: \\{ href: "${route.replace(/[?]/g,'\\?')}"`))
  }
  assert.match(portal,/className="summary-card admin-metric-card"/)
  assert.match(portal,/requestedStatus = new URLSearchParams\(location\.search\)\.get\("status"\)/)
})

test('admin forms and operational records stack without page-level overflow',()=>{
  assert.match(styles,/\.admin-create-card form \{display:grid;grid-template-columns:repeat\(2,minmax\(0,1fr\)\)/)
  assert.match(styles,/\.admin-create-card form,\.admin-main \.account-filters \{grid-template-columns:1fr\}/)
  assert.match(styles,/\.admin-main \.admin-payout-table \{min-width:0\}/)
  assert.match(styles,/\.admin-main td\[data-label="Action"\] \.actions \{display:grid/)
  assert.match(styles,/\.admin-create-card input,[\s\S]*\.admin-create-card select,[\s\S]*max-width: 100%/)
  assert.match(styles,/@media \(min-width:801px\)[\s\S]*white-space:nowrap/)
  assert.match(styles,/\.payout-filter-bar \{[\s\S]*grid-template-columns/)
  assert.match(styles,/@media \(max-width:800px\)[\s\S]*\.payout-filter-bar \{grid-template-columns:minmax\(0,1fr\)\}/)
  assert.match(styles,/\.payout-cycle-summary \{grid-template-columns:repeat\(3,minmax\(0,1fr\)\)\}/)
  assert.match(earnings,/className="filter-bar payout-filter-bar"/)
  assert.match(earnings,/className="payout-filter-actions"/)
  for(const label of ['Payout Status','Eligible Amount','Cycle Start','Reference']) assert.match(earnings,new RegExp(`data-label="${label}"`))
})

test('admin notification controls preserve bell, red badge, and friendly states',()=>{
  assert.match(portal,/<NavIcon name="notifications"/)
  assert.match(styles,/\.admin-header \.notification-count[^}]*background:#b42318/s)
  assert.match(portal,/Notifications are temporarily unavailable\./)
  assert.match(portal,/No records found\./)
  assert.match(portal,/Unable to load data\./)
})
