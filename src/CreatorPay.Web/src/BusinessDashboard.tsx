import {ReactNode,useEffect,useState} from 'react'
import {api} from './apiClient'
import {ActiveCreators,AdvertisingRequests,BusinessRelationship,FindCreators} from './BusinessAdvertising'
import {relationshipState} from './relationshipTime'
import {ConfirmedSalesWorkspace} from './ConfirmedSalesWorkspace'
import {AccountChrome} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {RoleNavigation} from './RoleNavigation'
import {NavIcon,type NavIconName} from './navIcons'
import './low-balance.css'

type Tab='home'|'find'|'active'|'requests'|'cashiers'|'confirmed-sales'|'checkout'|'wallet'|'profile'
type Metrics={confirmedSales:number;period:string}
type Profile={tradingName:string;merchantStatus:string;effectiveStatus:string;effectiveStatusReason:string}
type WalletSummary={availableBalance:number;currencyCode:string;status:string;minimumRequiredBalance?:number;advertisingEligible?:boolean}
const money=(value:number)=>new Intl.NumberFormat('en-ET',{minimumFractionDigits:2,maximumFractionDigits:2}).format(value)

export function BusinessDashboard({cashiers,checkout,wallet,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[items,setItems]=useState<BusinessRelationship[]>([]),[business,setBusiness]=useState<Profile>(),[balance,setBalance]=useState<WalletSummary>(),[metrics,setMetrics]=useState<Metrics>(),[error,setError]=useState('')
 const load=()=>api<BusinessRelationship[]>('/api/v1/merchant/partnerships').then(value=>{setItems(value);setError('')}).catch(e=>{console.error(e);setError("We couldn't load advertising relationships right now.")})
 const loadBalance=()=>api<WalletSummary>('/api/v1/merchant/wallet').then(setBalance).catch(e=>console.error(e))
 useEffect(()=>{void load();void api<Profile>('/api/v1/merchants/me').then(setBusiness).catch(console.error);void loadBalance();void api<Metrics>('/api/v1/merchant/dashboard-metrics').then(setMetrics).catch(console.error);const refresh=()=>{if(document.visibilityState==='visible'){void load();void loadBalance();void api<Metrics>('/api/v1/merchant/dashboard-metrics').then(setMetrics).catch(console.error)}};const timer=window.setInterval(refresh,12000);document.addEventListener('visibilitychange',refresh);window.addEventListener('focus',refresh);const unsubscribe=onActionableRefresh(refresh);return()=>{window.clearInterval(timer);document.removeEventListener('visibilitychange',refresh);window.removeEventListener('focus',refresh);unsubscribe()}},[])
 const active=items.filter(x=>relationshipState(x).label==='Active').length
 const pending=items.filter(x=>(x.status==='Pending'&&x.initiatedBy==='Creator')||x.promotionVideo?.status==='Pending').length
 const low=balance?.advertisingEligible===false
 const navigate=(target:string)=>setTab(target.includes('profile')?'profile':target.includes('wallet')||target.includes('deposit')?'wallet':target.includes('sale')?'confirmed-sales':target.includes('checkout')?'checkout':target.includes('request')||target.includes('invitation')?'requests':'home')
 const selectedTab:Tab=tab==='home'?'find':tab
 const dashboardCards:Array<{label:string;value:string|number;icon:NavIconName;select:()=>void}>=[
  {label:'Active Ads',value:active,icon:'ads',select:()=>setTab('active')},
  {label:'Pending Requests',value:pending,icon:'requests',select:()=>setTab('requests')},
  {label:'Confirmed Sales',value:metrics?.confirmedSales??0,icon:'sales',select:()=>setTab('confirmed-sales')},
  {label:'Wallet Balance',value:money(balance?.availableBalance??0),icon:'wallet',select:()=>setTab('wallet')},
 ]
 return <AccountChrome role="Business" name={business?.tradingName} status={business?.effectiveStatus} onProfile={()=>setTab('profile')} onManagement={()=>setTab('cashiers')} onHelp={()=>location.assign('/help')} onSignOut={onSignOut} onNavigate={navigate}>
  <div className="creator-dashboard business-dashboard">
   <RoleNavigation role="Business" label="Business sections" items={[{id:'find',label:'Creators',icon:'creators',active:selectedTab==='find',onSelect:()=>setTab('find')},{id:'active',label:'Active Ads',icon:'ads',active:selectedTab==='active',onSelect:()=>setTab('active')},{id:'requests',label:'Requests',icon:'requests',active:selectedTab==='requests',onSelect:()=>setTab('requests')},{id:'checkout',label:'Checkout',icon:'checkout',active:selectedTab==='checkout',onSelect:()=>setTab('checkout')},{id:'profile',label:'Profile',icon:'profile',active:selectedTab==='profile',onSelect:()=>setTab('profile')}]} />
   {error&&tab!=='profile'&&<p className="friendly-error">{error}</p>}
   {low&&<div className="low-balance-warning" role="alert">Your balance is low. Add funds to continue advertising. Available: {money(balance?.availableBalance??0)} · Required minimum: {money(balance?.minimumRequiredBalance??0)}.</div>}
   {tab==='home'&&<section className="business-home" aria-labelledby="business-dashboard-title">
    <h2 id="business-dashboard-title">Dashboard</h2>
    <div className="business-dashboard-cards">{dashboardCards.map(card=><button type="button" key={card.label} onClick={card.select} aria-label={`${card.label}: ${card.value}`}><span className="business-dashboard-icon"><NavIcon name={card.icon}/></span><span className="business-dashboard-label">{card.label}</span><strong>{card.value}</strong><span className="business-chevron" aria-hidden="true">›</span></button>)}</div>
    <section className="business-quick-actions" aria-labelledby="business-quick-actions-title"><h3 id="business-quick-actions-title">Quick Actions</h3><div>
     <button type="button" onClick={()=>setTab('find')}><NavIcon name="ads"/><span>Add New Ad</span></button>
     <button type="button" onClick={()=>setTab('find')}><NavIcon name="userPlus"/><span>Add Creator</span></button>
     <button type="button" onClick={()=>setTab('cashiers')}><NavIcon name="cashier"/><span>Cashier Management</span></button>
    </div></section>
   </section>}
   {tab==='find'&&balance?.advertisingEligible!==false&&<><FindCreators refresh={()=>void load()}/>{active>0&&<div className="secondary-section-link"><span>{active} active ad{active===1?'':'s'}</span><button className="quiet" onClick={()=>setTab('active')}>View all</button></div>}</>}
   {tab==='active'&&balance?.advertisingEligible!==false&&<><ActiveCreators items={items} refresh={()=>void load()}/>{error&&<p className="friendly-error">{error}</p>}</>}
   {tab==='requests'&&<><AdvertisingRequests items={items} refresh={()=>void load()}/>{error&&<p className="friendly-error">{error}</p>}</>}
   {tab==='cashiers'&&cashiers}{tab==='confirmed-sales'&&<ConfirmedSalesWorkspace/>}{tab==='checkout'&&checkout}{tab==='wallet'&&wallet}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
