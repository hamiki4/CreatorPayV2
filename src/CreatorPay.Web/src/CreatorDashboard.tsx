import {useEffect,useRef,useState} from 'react'
import {api,statusLabel} from './apiClient'
import {ActiveAds,AdvertisingRequest,CreatorConfirmedSales,CreatorRequests,FindBusinesses} from './CreatorAdvertising'
import {AccountChrome} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {creatorPhotoUrl,ProfileAvatar} from './profileMedia'

type Tab='home'|'find'|'ads'|'requests'|'sales'|'payout'|'profile'
type Profile={displayName:string;publicCreatorId:string;creatorCode:string;creatorStatus:string;accountStatus:string;email:string;phoneNumber:string;city:string;biography?:string;contentCategories?:string;profileImage?:{fileName:string;contentType:string;sizeBytes:number}}
type Earnings={currencyCode:string;pendingBalance:number;availableBalance:number;heldBalance:number;scheduledBalance:number;currentPayoutAmount:number;currentPeriodConfirmedSales:number;nextEstimatedPayoutAtUtc?:string;lastPayoutAtUtc?:string}
const money=(value:number,currency='ETB')=>new Intl.NumberFormat('en-ET',{style:'currency',currency}).format(value)

function ProfilePanel({profile,error,refresh}:{profile?:Profile;error:string;refresh:()=>void}){const[copy,setCopy]=useState(''),[busy,setBusy]=useState(false),[notice,setNotice]=useState(''),input=useRef<HTMLInputElement>(null);if(error)return <section className="creator-section"><h2>Profile</h2><p className="friendly-error">{error}</p></section>;if(!profile)return <p>Loading…</p>;const photo=creatorPhotoUrl(profile.publicCreatorId,profile.profileImage?.fileName);async function submit(file?:File){if(!file)return;setBusy(true);setNotice('');try{const form=new FormData();form.append('photo',file);await api('/api/v1/creators/me/profile-photo',{method:'POST',body:form});setNotice('Profile photo updated.');refresh()}catch(error){console.error(error);setNotice((error as Error).message)}finally{setBusy(false);if(input.current)input.current.value=''}}async function remove(){if(!confirm('Remove your profile photo?'))return;setBusy(true);setNotice('');try{await api('/api/v1/creators/me/profile-photo',{method:'DELETE'});setNotice('Profile photo removed.');refresh()}catch(error){console.error(error);setNotice((error as Error).message)}finally{setBusy(false)}}return <section className="creator-section"><h2>Profile</h2>{notice&&<p className={notice.includes('updated')||notice.includes('removed')?'success-note':'friendly-error'} role="status">{notice}</p>}<div className="compact-panel profile-details"><div className="creator-profile-card"><div className="creator-heading"><ProfileAvatar name={profile.displayName} photoUrl={photo} className="creator-profile-avatar" /><div><strong>{profile.displayName}</strong><p>{profile.email||'No email provided'}<br/>{profile.phoneNumber}<br/>{profile.city}</p><span className="status-badge">{statusLabel(profile.creatorStatus)}</span></div></div><div className="actions"><input ref={input} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" onChange={e=>void submit(e.target.files?.[0])}/><button type="button" disabled={busy} onClick={()=>input.current?.click()}>{profile.profileImage?'Change Photo':'Upload Photo'}</button>{profile.profileImage&&<button type="button" className="quiet" disabled={busy} onClick={()=>void remove()}>Remove Photo</button>}</div></div><div className="creator-id-profile"><span>Creator ID</span><strong>{profile.creatorCode}</strong><small>{profile.publicCreatorId}</small><button className="quiet" onClick={async()=>{await navigator.clipboard.writeText(profile.creatorCode);setCopy('Copied')}}>Copy Creator ID</button>{copy&&<small role="status">{copy}</small>}</div></div></section>}

export function CreatorDashboard({onSignOut}:{onSignOut:()=>void}){
  const[tab,setTab]=useState<Tab>('home'),[profile,setProfile]=useState<Profile>(),[earnings,setEarnings]=useState<Earnings>(),[requests,setRequests]=useState<AdvertisingRequest[]>([]),[loading,setLoading]=useState(true),[error,setError]=useState('')
  const load=()=>Promise.allSettled([api<Profile>('/api/v1/creators/me'),api<Earnings>('/api/v1/creator/earnings/summary'),api<AdvertisingRequest[]>('/api/v1/creator/partnerships')]).then(results=>{const[p,e,a]=results;if(p.status==='fulfilled')setProfile(p.value);if(e.status==='fulfilled')setEarnings(e.value);if(a.status==='fulfilled')setRequests(a.value);if(results.some(x=>x.status==='rejected'))setError("We couldn't load some information.");setLoading(false)})
  useEffect(()=>{let current=true;void load().then(()=>{if(!current)return});const unsubscribe=onActionableRefresh(()=>{void load()});return()=>{current=false;unsubscribe()}},[])
  const pending=requests.filter(x=>x.status==='Pending').length,date=(value?:string)=>value?new Intl.DateTimeFormat('en-GB').format(new Date(value)):'—'
  const tabs:[Tab,string][]=[['find','Find Businesses'],['ads','Active Ads'],['requests','Requests'],['sales','Confirmed Sales'],['payout','Payout']]
  const navigate=(target:string)=>setTab(target.includes('payout')?'payout':target.includes('sales')?'sales':target.includes('requests')?'requests':target.includes('ads')?'ads':target.includes('profile')?'profile':target.includes('find')?'find':'home')
  return <AccountChrome role="Creator" name={profile?.displayName} status={profile?statusLabel(profile.creatorStatus):'Active'} onProfile={()=>setTab('profile')} onHelp={()=>location.assign('/help')} onSignOut={onSignOut} onNavigate={navigate}><div className="creator-dashboard">
    <nav className="creator-tabs" aria-label="Creator sections">{tabs.map(([value,label])=><button key={value} className={tab===value?'active':''} aria-current={tab===value?'page':undefined} onClick={()=>setTab(value)}>{label}</button>)}</nav>
    {tab==='home'&&<><div className="creator-summary compact-role-summary">
      <article><span>Pending Requests</span><strong>{pending}</strong></article>
      <article><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article>
      <article><span>Next Payout Date</span><strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong></article>
    </div>{error&&<p className="friendly-error">{error}</p>}<div className="creator-start"><h2>Start advertising</h2><p>Find a Business, request permission to promote, and start earning money!</p><button onClick={()=>setTab('find')}>Find Businesses</button></div></>}
    {tab==='find'&&<FindBusinesses onRequested={()=>void load()}/>}
    {tab==='ads'&&<ActiveAds items={requests} loading={loading}/>}
    {tab==='requests'&&<CreatorRequests items={requests} refresh={()=>void load()}/>}
    {tab==='sales'&&<CreatorConfirmedSales/>}
    {tab==='payout'&&<section className="creator-section"><h2>Payout</h2><div className="summary-grid"><article className="summary-card"><span>Payout Amount</span><strong>{money(earnings?.currentPayoutAmount??0,earnings?.currencyCode)}</strong></article><article className="summary-card"><span>Next Payout Date</span><strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong></article></div></section>}
    {tab==='profile'&&<ProfilePanel profile={profile} error={!profile&&error?error:''} refresh={()=>void load()}/>}
  </div></AccountChrome>
}
