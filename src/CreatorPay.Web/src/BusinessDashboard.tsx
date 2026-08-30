import {ReactNode,useEffect,useState} from 'react'
import {api} from './apiClient'
import {ActiveCreators,AdvertisingRequests,BusinessRelationship,FindCreators,type BusinessCreator} from './BusinessAdvertising'
import {relationshipState} from './relationshipTime'
import {ConfirmedSalesWorkspace} from './ConfirmedSalesWorkspace'
import {AccountChrome} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {RoleNavigation} from './RoleNavigation'
import {NavIcon,type NavIconName} from './navIcons'
import {currentPartnerships} from './partnershipState'
import {ProfileAvatar} from './profileMedia'
import './low-balance.css'

type Tab='home'|'find'|'active'|'requests'|'cashiers'|'confirmed-sales'|'checkout'|'wallet'|'profile'
type Metrics={confirmedSales:number;period:string}
type Profile={tradingName:string;merchantStatus:string;effectiveStatus:string;effectiveStatusReason:string}
type WalletSummary={availableBalance:number;currencyCode:string;status:string;minimumRequiredBalance?:number;advertisingEligible?:boolean}
type RequestSection='creator'|'video'
const money=(value:number)=>new Intl.NumberFormat('en-ET',{minimumFractionDigits:2,maximumFractionDigits:2}).format(value)
const displayDate=(value:string)=>new Intl.DateTimeFormat('en-GB',{day:'2-digit',month:'2-digit',year:'numeric'}).format(new Date(value))
const followers=(value?:number|null)=>value==null?'—':new Intl.NumberFormat('en-US',{notation:'compact',maximumFractionDigits:1}).format(value)

export function BusinessDashboard({cashiers,checkout,wallet,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[requestSection,setRequestSection]=useState<RequestSection>('creator'),[items,setItems]=useState<BusinessRelationship[]>([]),[topCreators,setTopCreators]=useState<BusinessCreator[]>([]),[creatorQuery,setCreatorQuery]=useState(''),[business,setBusiness]=useState<Profile>(),[balance,setBalance]=useState<WalletSummary>(),[metrics,setMetrics]=useState<Metrics>(),[error,setError]=useState('')
 const load=()=>api<BusinessRelationship[]>('/api/v1/merchant/partnerships').then(value=>{setItems(value);setError('')}).catch(e=>{console.error(e);setError("We couldn't load advertising relationships right now.")})
 const loadBalance=()=>api<WalletSummary>('/api/v1/merchant/wallet').then(setBalance).catch(e=>console.error(e))
 const loadTopCreators=()=>api<BusinessCreator[]>('/api/v1/merchant/creators/search?q=&sort=followers').then(value=>setTopCreators(value.slice(0,3))).catch(()=>setTopCreators([]))
 useEffect(()=>{void load();void loadTopCreators();void api<Profile>('/api/v1/merchants/me').then(setBusiness).catch(console.error);void loadBalance();void api<Metrics>('/api/v1/merchant/dashboard-metrics').then(setMetrics).catch(console.error);const refresh=()=>{if(document.visibilityState==='visible'){void load();void loadTopCreators();void loadBalance();void api<Metrics>('/api/v1/merchant/dashboard-metrics').then(setMetrics).catch(console.error)}};const timer=window.setInterval(refresh,12000);document.addEventListener('visibilitychange',refresh);window.addEventListener('focus',refresh);const unsubscribe=onActionableRefresh(refresh);return()=>{window.clearInterval(timer);document.removeEventListener('visibilitychange',refresh);window.removeEventListener('focus',refresh);unsubscribe()}},[])
 const currentItems=currentPartnerships(items,x=>x.creatorId)
 const active=currentItems.filter(x=>relationshipState(x).label==='Active').length
 const creatorRequests=currentItems.filter(x=>x.status==='Pending'&&x.initiatedBy==='Creator').length
 const videoApprovals=currentItems.filter(x=>x.promotionVideo?.status==='Pending').length
 const low=balance?.advertisingEligible===false
 const openRequests=(section:RequestSection)=>{setRequestSection(section);setTab('requests')}
 const openCreator=(creator:BusinessCreator)=>{setCreatorQuery(creator.publicCreatorId);setTab('find')}
 const openCreators=()=>{setCreatorQuery('');setTab('find')}
 const navigate=(target:string)=>{
  if(target.includes('profile'))setTab('profile')
  else if(target.includes('wallet')||target.includes('deposit'))setTab('wallet')
  else if(target.includes('sale'))setTab('confirmed-sales')
  else if(target.includes('checkout'))setTab('checkout')
  else if(target.includes('video')||target.includes('promotion'))openRequests('video')
  else if(target.includes('request')||target.includes('invitation'))openRequests('creator')
  else setTab('home')
 }
 const dashboardCards:Array<{label:string;value:string|number;icon:NavIconName;select:()=>void}>=[
  {label:'Active Ads',value:active,icon:'ads',select:()=>setTab('active')},
  {label:'Creator Requests',value:creatorRequests,icon:'requests',select:()=>openRequests('creator')},
  {label:'Video Approvals',value:videoApprovals,icon:'video',select:()=>openRequests('video')},
  {label:'Confirmed Sales',value:metrics?.confirmedSales??0,icon:'sales',select:()=>setTab('confirmed-sales')},
  {label:'Wallet Balance',value:money(balance?.availableBalance??0),icon:'wallet',select:()=>setTab('wallet')},
 ]
 const recentActivity=currentItems.flatMap(x=>{
  if(x.status==='Pending'&&x.initiatedBy==='Creator')return[{id:`request-${x.id}`,label:'Creator request',detail:`${x.creatorName} is waiting for your response.`,at:x.requestedAtUtc,icon:'requests' as NavIconName,select:()=>openRequests('creator')}]
  if(x.promotionVideo?.status==='Pending')return[{id:`video-${x.promotionVideo.id}`,label:'Video approval',detail:`${x.creatorName}'s promo video is ready to review.`,at:x.promotionVideo.submittedAtUtc,icon:'video' as NavIconName,select:()=>openRequests('video')}]
  if(relationshipState(x).label==='Active'&&x.activatedAtUtc)return[{id:`active-${x.id}`,label:'Promotion live',detail:`${x.creatorName}'s promotion is active.`,at:x.activatedAtUtc,icon:'ads' as NavIconName,select:()=>setTab('active')}]
  return[]
 }).sort((a,b)=>new Date(b.at).getTime()-new Date(a.at).getTime()).slice(0,3)
 return <AccountChrome role="Business" name={business?.tradingName} status={business?.effectiveStatus} onProfile={()=>setTab('profile')} onManagement={()=>setTab('cashiers')} onHelp={()=>location.assign('/help?category=Businesses')} onSignOut={onSignOut} onNavigate={navigate}>
  <div className="creator-dashboard business-dashboard">
   <RoleNavigation role="Business" label="Business sections" items={[{id:'home',label:'Home',icon:'home',active:tab==='home'||tab==='confirmed-sales'||tab==='wallet'||tab==='cashiers',onSelect:()=>setTab('home')},{id:'find',label:'Creators',icon:'creators',active:tab==='find',onSelect:openCreators},{id:'active',label:'Active Ads',icon:'ads',active:tab==='active',onSelect:()=>setTab('active')},{id:'requests',label:'Requests',icon:'requests',active:tab==='requests',onSelect:()=>openRequests('creator')},{id:'checkout',label:'Checkout',icon:'checkout',active:tab==='checkout',onSelect:()=>setTab('checkout')},{id:'profile',label:'Profile',icon:'profile',active:tab==='profile',onSelect:()=>setTab('profile')}]} />
   {error&&tab!=='profile'&&<p className="friendly-error">{error}</p>}
   {low&&<div className="low-balance-warning" role="alert">Your balance is low. Add funds to continue advertising. Available: {money(balance?.availableBalance??0)} · Required minimum: {money(balance?.minimumRequiredBalance??0)}.</div>}
   {tab==='home'&&<section className="business-home" aria-labelledby="business-dashboard-title">
    <h2 id="business-dashboard-title">Dashboard</h2>
    <div className="business-dashboard-cards">{dashboardCards.map(card=><button type="button" key={card.label} onClick={card.select} aria-label={`${card.label}: ${card.value}`}><span className="business-dashboard-icon"><NavIcon name={card.icon}/></span><span className="business-dashboard-label">{card.label}</span><strong>{card.value}</strong><span className="business-chevron" aria-hidden="true">›</span></button>)}</div>
    <section className="business-quick-actions" aria-labelledby="business-quick-actions-title"><h3 id="business-quick-actions-title">Quick Actions</h3><div>
     <button type="button" onClick={openCreators}><NavIcon name="ads"/><span>Add New Ad</span></button>
     <button type="button" onClick={openCreators}><NavIcon name="userPlus"/><span>Add Creator</span></button>
     <button type="button" onClick={()=>setTab('cashiers')}><NavIcon name="cashier"/><span>Cashier Management</span></button>
    </div></section>
    {topCreators.length>0&&<section className="business-top-creators" aria-labelledby="business-top-creators-title"><h3 id="business-top-creators-title">Top Creators</h3><div className="business-top-creators-list">{topCreators.map(creator=><article className="business-top-creator-card" data-followers={creator.followerCount??-1} key={creator.id}><div className="business-top-creator-heading"><ProfileAvatar name={creator.displayName} photoUrl={creator.profileImageUrl}/><div><strong>{creator.displayName}</strong><span>{creator.publicCreatorId}</span></div></div><div className="business-top-creator-followers"><span>Followers</span><strong>{followers(creator.followerCount)}</strong></div><button type="button" onClick={()=>openCreator(creator)}>View Creator</button></article>)}</div></section>}
    {recentActivity.length>0&&<section className="business-recent-activity" aria-labelledby="business-recent-activity-title"><h3 id="business-recent-activity-title">Recent Activity</h3><div>{recentActivity.map(activity=><button type="button" key={activity.id} onClick={activity.select}><span className="business-activity-icon"><NavIcon name={activity.icon}/></span><span className="business-activity-copy"><strong>{activity.label}</strong><span>{activity.detail}</span></span><time dateTime={activity.at}>{displayDate(activity.at)}</time><span className="business-chevron" aria-hidden="true">›</span></button>)}</div></section>}
   </section>}
   {tab==='find'&&balance?.advertisingEligible!==false&&<><FindCreators initialQuery={creatorQuery} refresh={()=>{void load();void loadTopCreators()}}/>{active>0&&<div className="secondary-section-link"><span>{active} active ad{active===1?'':'s'}</span><button className="quiet" onClick={()=>setTab('active')}>View all</button></div>}</>}
   {tab==='active'&&balance?.advertisingEligible!==false&&<><ActiveCreators items={items} refresh={()=>void load()}/>{error&&<p className="friendly-error">{error}</p>}</>}
   {tab==='requests'&&<><AdvertisingRequests items={items} businessName={business?.tradingName} initialSection={requestSection} refresh={()=>void load()}/>{error&&<p className="friendly-error">{error}</p>}</>}
   {tab==='cashiers'&&cashiers}{tab==='confirmed-sales'&&<ConfirmedSalesWorkspace/>}{tab==='checkout'&&checkout}{tab==='wallet'&&wallet}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
