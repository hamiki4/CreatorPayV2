import {useEffect,useState} from 'react'
import {api,statusLabel} from './apiClient'
import {ActiveAds,AdvertisingRequest,FindBusinesses} from './CreatorAdvertising'
import {AccountChrome} from './AccountChrome'

type Tab='home'|'find'|'ads'|'payout'|'profile'
type Profile={displayName:string;publicCreatorId:string;creatorCode:string;creatorStatus:string;accountStatus:string;email:string;phoneNumber:string;city:string;biography?:string;contentCategories?:string}
type Earnings={currencyCode:string;pendingBalance:number;availableBalance:number;heldBalance:number;scheduledBalance:number;currentPayoutAmount:number;currentPeriodConfirmedSales:number;nextEstimatedPayoutAtUtc?:string;lastPayoutAtUtc?:string}
const money=(value:number,currency='ETB')=>new Intl.NumberFormat('en-ET',{style:'currency',currency}).format(value)

function ProfilePanel({profile,error}:{profile?:Profile;error:string}){const[copy,setCopy]=useState('');if(error)return <section className="creator-section"><h2>Profile</h2><p className="friendly-error">{error}</p></section>;if(!profile)return <p>Loading…</p>;return <section className="creator-section"><h2>Profile</h2><div className="compact-panel profile-details"><div><strong>{profile.displayName}</strong><p>{profile.email||'No email provided'}<br/>{profile.phoneNumber}<br/>{profile.city}</p><span className="status-badge">{statusLabel(profile.creatorStatus)}</span></div><div className="creator-id-profile"><span>Creator ID</span><strong>{profile.creatorCode}</strong><small>{profile.publicCreatorId}</small><button className="quiet" onClick={async()=>{await navigator.clipboard.writeText(profile.creatorCode);setCopy('Copied')}}>Copy Creator ID</button>{copy&&<small role="status">{copy}</small>}</div></div></section>}

export function CreatorDashboard({onSignOut}:{onSignOut:()=>void}){
  const[tab,setTab]=useState<Tab>('home'),[profile,setProfile]=useState<Profile>(),[earnings,setEarnings]=useState<Earnings>(),[requests,setRequests]=useState<AdvertisingRequest[]>([]),[loading,setLoading]=useState(true),[error,setError]=useState('')
  const load=()=>Promise.allSettled([api<Profile>('/api/v1/creators/me'),api<Earnings>('/api/v1/creator/earnings/summary'),api<AdvertisingRequest[]>('/api/v1/creator/partnerships')]).then(results=>{const[p,e,a]=results;if(p.status==='fulfilled')setProfile(p.value);if(e.status==='fulfilled')setEarnings(e.value);if(a.status==='fulfilled')setRequests(a.value);if(results.some(x=>x.status==='rejected'))setError("We couldn't load some information.");setLoading(false)})
  useEffect(()=>{let current=true;void load().then(()=>{if(!current)return});return()=>{current=false}},[])
  const pending=requests.filter(x=>x.status==='Pending').length,date=(value?:string)=>value?new Intl.DateTimeFormat('en-GB').format(new Date(value)):'—'
  const tabs:[Tab,string][]=[['find','Find Businesses'],['ads','Ads'],['payout','Payout']]
  const navigate=(target:string)=>setTab(target.includes('payout')?'payout':target.includes('ads')?'ads':target.includes('profile')?'profile':target.includes('find')?'find':'home')
  return <AccountChrome role="Creator" name={profile?.displayName} status={profile?statusLabel(profile.creatorStatus):'Active'} onProfile={()=>setTab('profile')} onHelp={()=>location.assign('/help')} onSignOut={onSignOut} onNavigate={navigate}><div className="creator-dashboard">
    <nav className="creator-tabs" aria-label="Creator sections">{tabs.map(([value,label])=><button key={value} className={tab===value?'active':''} aria-current={tab===value?'page':undefined} onClick={()=>setTab(value)}>{label}</button>)}</nav>
    {tab==='home'&&<><div className="creator-summary compact-role-summary">
      <article><span>Pending Requests</span><strong>{pending}</strong></article>
      <article><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article>
      <article><span>Next Payout Date</span><strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong></article>
    </div>{error&&<p className="friendly-error">{error}</p>}<div className="creator-start"><h2>Start advertising</h2><p>Find a Business, request permission to promote, and start earning money!</p><button onClick={()=>setTab('find')}>Find Businesses</button></div></>}
    {tab==='find'&&<FindBusinesses onRequested={()=>void load()}/>} 
    {tab==='ads'&&<ActiveAds items={requests} loading={loading} refresh={()=>void load()}/>} 
    {tab==='payout'&&<section className="creator-section"><h2>Payout</h2><div className="summary-grid"><article className="summary-card"><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article><article className="summary-card"><span>Next Payout Date</span><strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong></article></div></section>}
    {tab==='profile'&&<ProfilePanel profile={profile} error={!profile&&error?error:''}/>} 
  </div></AccountChrome>
}
