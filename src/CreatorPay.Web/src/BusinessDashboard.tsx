import {ReactNode,useEffect,useState} from 'react'
import {api} from './apiClient'
import {AccountChrome} from './AccountChrome'
import {RoleNavigation} from './RoleNavigation'
import {BusinessPromotions} from './PromotionWorkspace'
import {BusinessUgc} from './UgcWorkspace'
import {amount,v3Request} from './v3ProductApi'
import {onActionableRefresh} from './actionableRefresh'
import {ConfirmedSalesWorkspace} from './ConfirmedSalesWorkspace'
import {NavIcon,type NavIconName} from './navIcons'

type Tab='home'|'promotions'|'ugc'|'wallet'|'profile'|'cashiers'|'sales'|'checkout'
type Profile={tradingName:string;effectiveStatus:string}
type Home={wallet:{available:number;reserved:number;history?:{id:string;label:string;amount:number;atUtc:string}[]};activeCampaigns:number;creatorRequests:number;confirmedSales:number}
type Ugc={status:string}
type Pricing={rows:{type:string;views:number;businessPays:number;saleCostPercent:number}[]}
const icons:Record<string,NavIconName>={active:'ads',requests:'requests',content:'video',sales:'sales',wallet:'wallet',pricing:'wallet'}

export function BusinessDashboard({cashiers,checkout,wallet,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[business,setBusiness]=useState<Profile>(),[home,setHome]=useState<Home>(),[ugc,setUgc]=useState<Ugc[]>(),[pricing,setPricing]=useState<Pricing>(),[error,setError]=useState('')
 const load=()=>{
  setError('')
  void api<Profile>('/api/v1/merchants/me').then(setBusiness).catch(()=>{})
  void v3Request<Home>('/api/business/home').then(setHome).catch(e=>setError((e as Error).message))
  void v3Request<Ugc[]>('/api/business/ugc').then(setUgc).catch(()=>{})
  void v3Request<Pricing>('/api/business/pricing').then(setPricing).catch(()=>{})
 }
 useEffect(()=>{load();const active=()=>{if(document.visibilityState==='visible')load()};addEventListener('focus',active);return()=>removeEventListener('focus',active)},[])
 useEffect(()=>onActionableRefresh(load),[])
 const navigate=(target:string)=>{if(target.includes('/ugc'))setTab('ugc');else if(target.includes('promotion'))setTab('promotions');else if(target.includes('wallet')||target.includes('deposit'))setTab('wallet');else if(target.includes('checkout'))setTab('checkout');else if(target.includes('sale'))setTab('sales');else if(target.includes('profile'))setTab('profile');else setTab('home')}
 const rows=[
  {key:'active',label:'Active Promotions',value:home?String(home.activeCampaigns):'—',action:()=>setTab('promotions')},
  {key:'requests',label:'Creator Requests',value:home?String(home.creatorRequests):'—',action:()=>setTab('promotions')},
  {key:'content',label:'Open UGC',value:ugc?String(ugc.filter(x=>x.status==='Open').length):'—',action:()=>setTab('ugc')},
  {key:'sales',label:'Confirmed Sales',value:home?String(home.confirmedSales):'—',action:()=>setTab('sales')},
  {key:'wallet',label:'Available Balance',value:home?amount(home.wallet.available):'—',action:()=>setTab('wallet')},
  {key:'wallet',label:'Reserved Balance',value:home?amount(home.wallet.reserved):'—',action:()=>setTab('wallet')},
 ]
 return <AccountChrome role="Business" name={business?.tradingName} status={business?.effectiveStatus} onProfile={()=>setTab('profile')} onManagement={()=>setTab('cashiers')} onHelp={()=>location.assign('/help?category=Businesses')} onSignOut={onSignOut} onNavigate={navigate}>
  <div className="creator-dashboard business-dashboard"><RoleNavigation role="Business" label="Business sections" items={[{id:'home',label:'Home',icon:'home',active:tab==='home'||tab==='cashiers'||tab==='sales'||tab==='checkout',onSelect:()=>setTab('home')},{id:'promotions',label:'Promotions',icon:'ads',active:tab==='promotions',onSelect:()=>setTab('promotions')},{id:'ugc',label:'UGC',icon:'video',active:tab==='ugc',onSelect:()=>setTab('ugc')},{id:'wallet',label:'Wallet',icon:'wallet',active:tab==='wallet',onSelect:()=>setTab('wallet')},{id:'profile',label:'Profile',icon:'profile',active:tab==='profile',onSelect:()=>setTab('profile')}]} />
   {error&&<p className="friendly-error" role="status">{error}</p>}
   {tab==='home'&&<section className="business-home product-workspace" aria-labelledby="business-home-title"><h2 id="business-home-title">Home</h2>
    <div className="business-dashboard-cards">{rows.map((row,index)=><button type="button" key={`${row.key}-${index}`} onClick={row.action}><span className="business-dashboard-icon"><NavIcon name={icons[row.key]}/></span><span className="business-dashboard-label">{row.label}</span><strong>{row.value}</strong><span className="business-chevron" aria-hidden="true">›</span></button>)}</div>
    {pricing&&<section className="business-home-pricing" aria-label="Promotion pricing"><h3>Promotion Pricing</h3><div>{pricing.rows.map(x=><article key={x.type}><strong>{x.type==='ViewPlusCommission'?'View & Sale':x.type==='ViewOnly'?'View Only':x.type}</strong><span>{x.views} views · Business pays {amount(x.businessPays)} · Sale cost {x.saleCostPercent}%</span></article>)}</div></section>}
    <section className="business-quick-actions" aria-labelledby="business-quick-title"><h3 id="business-quick-title">Quick Actions</h3><div>
     <button type="button" onClick={()=>setTab('promotions')}><NavIcon name="ads"/><span>New Promotion</span></button>
     <button type="button" onClick={()=>setTab('ugc')}><NavIcon name="video"/><span>Create UGC</span></button>
     <button type="button" onClick={()=>setTab('wallet')}><NavIcon name="wallet"/><span>Add Funds</span></button>
     <button type="button" onClick={()=>setTab('checkout')}><NavIcon name="checkout"/><span>Checkout</span></button>
     <button type="button" onClick={()=>setTab('cashiers')}><NavIcon name="cashier"/><span>Cashier Management</span></button>
    </div></section>
    {home?.wallet.history&&<section className="business-recent-activity" aria-labelledby="business-recent-title"><h3 id="business-recent-title">Recent Activity</h3>{home.wallet.history.length===0?<p className="compact-empty">No recent activity.</p>:<div>{home.wallet.history.slice(0,4).map(x=><article key={x.id}><span className="business-activity-icon"><NavIcon name="wallet"/></span><span className="business-activity-copy"><strong>{x.label}</strong><span>{amount(x.amount)}</span></span><time dateTime={x.atUtc}>{new Date(x.atUtc).toLocaleDateString()}</time></article>)}</div>}</section>}
   </section>}
   {tab==='promotions'&&<BusinessPromotions/>}{tab==='ugc'&&<BusinessUgc/>}{tab==='wallet'&&wallet}{tab==='checkout'&&checkout}{tab==='cashiers'&&cashiers}{tab==='sales'&&<ConfirmedSalesWorkspace/>}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
