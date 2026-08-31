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
  assert.match(styles,/\.admin-create-card :is\(input:not\(\[type="hidden"\]\),select,textarea\) \{[\s\S]*display:block;[\s\S]*width:100%;[\s\S]*min-height:2\.75rem;[\s\S]*pointer-events:auto;/)
  assert.match(styles,/\.admin-create-card select \{[\s\S]*cursor:pointer;[\s\S]*-webkit-user-select:none;[\s\S]*-webkit-touch-callout:none;/)
  assert.match(styles,/:is\(\.account-shell,\.admin-shell,\.role-cashier,\.public\) :is\(input,textarea\) \{[\s\S]*-webkit-user-select:text;/)
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
  assert.match(portal,/className="icon-button admin-notification-trigger"/)
  assert.match(portal,/className="drawer-scrim admin-notification-scrim"/)
  assert.match(portal,/unread > 99 \? "99\+" : unread/)
  assert.match(styles,/\.admin-header \.notification-count[^}]*background:#b42318/s)
  assert.match(styles,/\.admin-header \.notification-count[^}]*pointer-events:none/s)
  assert.match(styles,/\.admin-shell \.admin-notification-scrim[\s\S]*z-index:17;[\s\S]*background:rgb\(15 23 42 \/ \.18\)/)
  assert.match(styles,/\.admin-shell \.admin-notification-scrim[\s\S]*transition:none !important;[\s\S]*appearance:none;/)
  assert.match(styles,/\.admin-shell \.notification-drawer \{z-index:18/)
  assert.match(styles,/\.admin-shell \.notification-drawer h2 \{color:var\(--admin-ink\)/)
  assert.match(styles,/\.admin-shell \.notification-item \{grid-template-columns:2\.5rem minmax\(0,1fr\) \.7rem/)
  assert.match(styles,/\.admin-header \.notification-drawer \.notification-type-icon \{display:grid\}/)
  assert.match(styles,/\.admin-header button:not\(\.admin-notification-scrim\)/)
  assert.match(styles,/\.admin-main button:not\(\.admin-notification-scrim\):hover:not\(:disabled\)/)
  assert.match(portal,/Notifications are temporarily unavailable\./)
  assert.match(portal,/No records found\./)
  assert.match(portal,/Unable to load data\./)
})
