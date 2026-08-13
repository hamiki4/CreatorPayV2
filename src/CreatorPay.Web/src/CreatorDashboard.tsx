import {useEffect,useState} from 'react'
import {api,statusLabel} from './apiClient'
import {CreatorQrWorkspace} from './QrWorkspace'
import {ActiveAds,AdvertisingRequest,FindBusinesses} from './CreatorAdvertising'

type Tab='overview'|'find'|'ads'|'qr'|'payout'|'profile'
type Profile={displayName:string;creatorStatus:string;accountStatus:string;isEmailVerified:boolean;isPhoneVerified:boolean;email:string;phoneNumber:string;city:string;biography?:string;contentCategories?:string}
type Earnings={currencyCode:string;pendingBalance:number;availableBalance:number;heldBalance:number;scheduledBalance:number;currentPayoutAmount:number;currentPeriodConfirmedSales:number;nextEstimatedPayoutAtUtc?:string;lastPayoutAtUtc?:string}
const money=(value:number,currency='ETB')=>new Intl.NumberFormat('en-ET',{style:'currency',currency}).format(value)

function ProfilePanel({profile,error}:{profile?:Profile;error:string}){return <section className="creator-section"><h2>Profile &amp; Security</h2>{error?<p className="friendly-error">{error}</p>:!profile?<p>Loading…</p>:<div className="compact-panel"><div><strong>{profile.displayName}</strong><p>{profile.email}<br/>{profile.phoneNumber}</p></div><div><span className="status-badge">{statusLabel(profile.creatorStatus)}</span><p>{profile.city}</p><p>Email {profile.isEmailVerified?'verified':'not verified'} · Phone {profile.isPhoneVerified?'verified':'not verified'}</p></div></div>}</section>}

export function CreatorDashboard(){
  const[tab,setTab]=useState<Tab>('overview'),[profile,setProfile]=useState<Profile>(),[earnings,setEarnings]=useState<Earnings>(),[requests,setRequests]=useState<AdvertisingRequest[]>([]),[loading,setLoading]=useState(true),[error,setError]=useState('')
  const load=()=>Promise.allSettled([api<Profile>('/api/v1/creators/me'),api<Earnings>('/api/v1/creator/earnings/summary'),api<AdvertisingRequest[]>('/api/v1/creator/partnerships')]).then(results=>{const[p,e,a]=results;if(p.status==='fulfilled')setProfile(p.value);if(e.status==='fulfilled')setEarnings(e.value);if(a.status==='fulfilled')setRequests(a.value);if(results.some(x=>x.status==='rejected'))setError("We couldn't load some information.");setLoading(false)})
  useEffect(()=>{let current=true;void load().then(()=>{if(!current)return});return()=>{current=false}},[])
  const pending=requests.filter(x=>x.status==='Pending').length,date=(value?:string)=>value?new Intl.DateTimeFormat('en-GB').format(new Date(value)):'—'
  const tabs:[Tab,string][]=[['overview','Overview'],['find','Find Businesses'],['ads','Ads'],['qr','Creator ID'],['payout','Payout'],['profile','Profile']]
  return <div className="creator-dashboard">
    <div className="creator-header"><div><strong>{profile?.displayName??'Creator'}</strong><span className="status-badge">Active</span></div>{profile&&<small>Verified</small>}</div>
    <nav className="creator-tabs" aria-label="Creator Dashboard sections">{tabs.map(([value,label])=><button key={value} className={tab===value?'active':''} aria-current={tab===value?'page':undefined} onClick={()=>setTab(value)}>{label}</button>)}</nav>
    {tab==='overview'&&<><div className="creator-summary">
      <article><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article>
      <article><span>Pending Requests</span><strong>{pending}</strong></article>
    </div>{error&&<p className="friendly-error">{error}</p>}<div className="creator-start"><h2>Start advertising</h2><p>Find a Business, request permission to promote, and start earning money!</p><button onClick={()=>setTab('find')}>Find Businesses</button></div></>}
    {tab==='find'&&<FindBusinesses onRequested={()=>void load()}/>} 
    {tab==='ads'&&<ActiveAds items={requests} loading={loading} refresh={()=>void load()}/>} 
    {tab==='qr'&&<CreatorQrWorkspace/>}
    {tab==='payout'&&<section className="creator-section"><h2>Payout</h2><div className="summary-grid"><article className="summary-card"><span>Next Payout Date</span><strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong></article><article className="summary-card"><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article><article className="summary-card"><span>Reserved Payout</span><strong>{money(earnings?.scheduledBalance??0,earnings?.currencyCode)}</strong></article><article className="summary-card"><span>Last Payout Date</span><strong>{date(earnings?.lastPayoutAtUtc)}</strong></article></div></section>}
    {tab==='profile'&&<ProfilePanel profile={profile} error={!profile&&error?error:''}/>} 
  </div>
}
