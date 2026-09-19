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
import {creatorPhotoUrl,ProfileAvatar} from './profileMedia'

type Tab='home'|'promotions'|'ugc'|'wallet'|'profile'|'cashiers'|'sales'|'checkout'
type Profile={tradingName:string;effectiveStatus:string}
type Home={wallet:{available:number;reserved:number;history?:{id:string;label:string;amount:number;atUtc:string}[]};activeCampaigns:number;creatorRequests:number;confirmedSales:number}
type Ugc={status:string}
type Pricing={rows:{type:string;views:number;businessPays:number;saleCostPercent:number}[]}
type RateableCreator={partnershipId:string;creatorId:string;displayName:string;existingRating?:number|null}
type TopCreator={creatorId:string;publicCreatorId:string;displayName:string;profileImageFileName?:string|null;averageRating:number;ratingCount:number;primaryPlatform?:string|null;audienceCount?:number|null}
const icons:Record<string,NavIconName>={active:'ads',requests:'requests',content:'video',sales:'sales',wallet:'wallet',pricing:'wallet'}

export function BusinessDashboard({cashiers,checkout,wallet,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[business,setBusiness]=useState<Profile>(),[home,setHome]=useState<Home>(),[ugc,setUgc]=useState<Ugc[]>(),[pricing,setPricing]=useState<Pricing>(),[rateableCreators,setRateableCreators]=useState<RateableCreator[]>([]),[topCreators,setTopCreators]=useState<TopCreator[]>([]),[ratingBusy,setRatingBusy]=useState<string|null>(null),[ratingMessage,setRatingMessage]=useState(''),[error,setError]=useState('')
 const load=()=>{
  setError('')
  void api<Profile>('/api/v1/merchants/me').then(setBusiness).catch(()=>{})
  void v3Request<Home>('/api/business/home').then(setHome).catch(e=>setError((e as Error).message))
  void v3Request<Ugc[]>('/api/business/ugc').then(setUgc).catch(()=>{})
  void v3Request<Pricing>('/api/business/pricing').then(setPricing).catch(()=>{});
void v3Request<RateableCreator[]>('/api/business/creator-ratings/eligible').then(setRateableCreators).catch(()=>{});
void v3Request<TopCreator[]>('/api/business/top-creators').then(setTopCreators).catch(()=>{})
 }
 const rateCreator=async(partnershipId:string,rating:number)=>{
setRatingBusy(partnershipId);setRatingMessage('');
try{
await v3Request(`/api/business/creator-ratings/${partnershipId}`,{
method:'PUT',
body:JSON.stringify({rating})
});
setRatingMessage('Rating saved.');
await Promise.all([
v3Request<RateableCreator[]>('/api/business/creator-ratings/eligible').then(setRateableCreators),
v3Request<TopCreator[]>('/api/business/top-creators').then(setTopCreators)
]);
}catch(e){
setRatingMessage((e as Error).message)
}finally{
setRatingBusy(null)
}
};
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
   {tab==='home'&&<>
<section className="business-creator-ratings">
<h3>Rate Creators</h3>

{ratingMessage&&
<p className="business-rating-message" role="status">
{ratingMessage}
</p>}

{rateableCreators.length===0?
<p className="compact-empty">No Creators are ready to rate yet.</p>:
<div className="business-rateable-list">
{rateableCreators.map(creator=>
<article className="business-rateable-card" key={creator.partnershipId}>
<div className="business-rateable-copy">
<strong>{creator.displayName}</strong>
<span>
{creator.existingRating
?`Your rating: ${creator.existingRating}/5`
:'Rate approved work'}
</span>
</div>

<div className="business-star-picker"
role="group"
aria-label={`Rate ${creator.displayName}`}>
{[1,2,3,4,5].map(star=>
<button
type="button"
key={star}
className={star<=(creator.existingRating??0)?'is-selected':''}
aria-label={`${star} star${star===1?'':'s'}`}
disabled={ratingBusy===creator.partnershipId}
onClick={()=>void rateCreator(creator.partnershipId,star)}>
★
</button>
)}
</div>
</article>
)}
</div>}
</section>

<section className="business-top-creators">
<div className="business-section-heading">
<h3>Top Creators</h3>
{topCreators.length>0&&<span>{topCreators.length}</span>}
</div>

{topCreators.length===0?
<p className="compact-empty">
Top Creators will appear after Businesses submit ratings.
</p>:
<div className="business-top-creators-track">
{topCreators.map(creator=>
<article className="business-top-creator-card" key={creator.creatorId}>
<div className="business-top-creator-identity">

<ProfileAvatar
name={creator.displayName}
photoUrl={creatorPhotoUrl(
creator.publicCreatorId,
creator.profileImageFileName ?? undefined
)}
/>

<div className="business-top-creator-copy">
<strong>{creator.displayName}</strong>
<span>
{creator.primaryPlatform??'Creator'}
{creator.audienceCount!=null
?` · ${creator.audienceCount.toLocaleString()}`
:''}
</span>
</div>
</div>

<div className="business-top-creator-rating"
aria-label={`${creator.averageRating} out of 5 from ${creator.ratingCount} ratings`}>

<span className="business-gold-stars" aria-hidden="true">
{'★'.repeat(Math.max(0,Math.min(5,Math.round(creator.averageRating))))}
{'☆'.repeat(Math.max(0,5-Math.round(creator.averageRating)))}
</span>

<strong>{creator.averageRating.toFixed(1)}</strong>
<span>({creator.ratingCount})</span>
</div>
</article>
)}
</div>}
</section>
</>}

{tab==='promotions'&&<BusinessPromotions/>}{tab==='ugc'&&<BusinessUgc/>}{tab==='wallet'&&wallet}{tab==='checkout'&&checkout}{tab==='cashiers'&&cashiers}{tab==='sales'&&<ConfirmedSalesWorkspace/>}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
