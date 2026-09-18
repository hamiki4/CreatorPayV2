import {useEffect,useState} from 'react'
import {api} from './apiClient'
import {AccountChrome,AccountStatusBadge} from './AccountChrome'
import {RoleNavigation} from './RoleNavigation'
import {creatorPhotoUrl,ProfileAvatar} from './profileMedia'
import {getExternalSession} from './externalSession'
import {CreatorDiscoverPromotions,CreatorPromotionRequests} from './PromotionWorkspace'
import {CreatorUgc} from './UgcWorkspace'
import {amount,v3Request} from './v3ProductApi'
import {onActionableRefresh} from './actionableRefresh'
import {SocialPlatformIcon} from './SocialPlatformIcon'

type Tab='home'|'discover'|'ugc'|'earnings'|'profile'|'requests'
type SocialPlatform='TikTok'|'Instagram'|'YouTube'|'Facebook'
type Profile={displayName:string;publicCreatorId:string;creatorCode:string;effectiveStatus:string;email:string;phoneNumber:string;city:string;biography?:string;contentCategories?:string;socialProfiles?:{platform:SocialPlatform;profileUrl?:string;followerCount:number;verificationStatus:string}[];profileImage?:{fileName:string}}
type SocialDraft={platform:SocialPlatform;profileUrl:string;audienceCount:string}
type Earnings={availableEarnings:number;minimumToCashOut:number;amountNeeded:number;eligibleAmount:number;viewEarnings:number;saleEarnings:number;ugcEarnings:number;history:{id:string;campaign:string;source:string;amount:number;atUtc:string}[];payoutHistory:{id:string;amount:number;status:string;eligibleAtUtc:string;paidAtUtc?:string}[]}
type Home={requests:number;activeCampaigns:number;earnings:Earnings}
type ActivePromotion={budgetId:string;title:string;business:{displayName:string};status:string;type:string;yourBudget:number;budgetRemaining:number}
const platforms:SocialPlatform[]=['TikTok','Instagram','YouTube','Facebook']
const activeAssignment=(status:string)=>['InProgress','Submitted','ChangesRequested'].includes(status)

function EarningsView({value}:{value?:Earnings}){
  if(!value)return <section className="product-workspace creator-section"><h2>Earnings</h2><p>Loading earnings…</p></section>
  const needed=Math.max(0,Number.isFinite(value.amountNeeded)?value.amountNeeded:0)
  const minimum=Math.max(0,Number.isFinite(value.minimumToCashOut)?value.minimumToCashOut:0)
  const available=Math.max(0,Number.isFinite(value.availableEarnings)?value.availableEarnings:0)
  return <section className="product-workspace creator-section">
    <h2>Earnings</h2>
    <div className="summary-grid creator-earnings-metrics">
      <article><span>View Earnings</span><strong>{amount(value.viewEarnings)}</strong></article>
      <article><span>Sale Earnings</span><strong>{amount(value.saleEarnings)}</strong></article>
      <article><span>UGC Earnings</span><strong>{amount(value.ugcEarnings)}</strong></article>
      <article><span>Available</span><strong>{amount(available)}</strong></article>
    </div>
    <section className="creator-payout-progress"><h3>Payout Progress</h3><progress aria-label="Payout threshold progress" max={Math.max(1,minimum)} value={Math.min(minimum,available)}/><p>{amount(needed)} until next payout</p></section>
    <h3>Recent Earnings</h3>{value.history.length===0?<p className="compact-empty">No earnings yet. Your approved Promotion, Sale and UGC earnings will appear here.</p>:<div className="product-list">{value.history.map(x=><article key={x.id}><div><strong>{x.source}</strong><span>{x.campaign} · {new Date(x.atUtc).toLocaleDateString()}</span></div><strong>{amount(x.amount)}</strong></article>)}</div>}
    <h3>Payout Status / History</h3>{value.payoutHistory.length===0?<p className="compact-empty">No payout history yet.</p>:<div className="product-list">{value.payoutHistory.map(x=><article key={x.id}><div><strong>{amount(x.amount)}</strong><span>{new Date(x.paidAtUtc??x.eligibleAtUtc).toLocaleDateString()}</span></div><span className="status-badge">{x.status}</span></article>)}</div>}
  </section>
}

function ProfileView({profile,refresh}:{profile?:Profile;refresh:()=>Promise<void>}){
  const[drafts,setDrafts]=useState<SocialDraft[]>([]),[message,setMessage]=useState(''),[busy,setBusy]=useState(false)
  useEffect(()=>{setDrafts((profile?.socialProfiles??[]).map(x=>({platform:x.platform,profileUrl:x.profileUrl??'',audienceCount:String(Number.isFinite(x.followerCount)?x.followerCount:0)})))},[profile])
  if(!profile)return <section className="creator-section"><h2>Profile</h2><p>Loading profile…</p></section>
  const account=getExternalSession()
  function update(index:number,value:Partial<SocialDraft>){setDrafts(drafts.map((x,i)=>i===index?{...x,...value}:x))}
  function add(){const platform=platforms.find(x=>!drafts.some(y=>y.platform===x));if(platform)setDrafts([...drafts,{platform,profileUrl:'',audienceCount:''}])}
  async function save(){setBusy(true);setMessage('');try{if(drafts.length===0)throw new Error('Keep at least one social profile.');const socialProfiles=drafts.map(x=>{const audience=Number(x.audienceCount);if(!x.profileUrl.trim()||!Number.isSafeInteger(audience)||audience<0)throw new Error(`Complete ${x.platform} URL and audience count.`);return{platform:x.platform,profileUrl:x.profileUrl.trim(),audienceCount:audience}});await api('/api/v1/integration/v3/creator/social-profiles',{method:'PUT',body:JSON.stringify({socialProfiles})});await refresh();setMessage('Social profiles updated.')}catch(e){setMessage((e as Error).message)}finally{setBusy(false)}}
  const publicId=/^\d{4}$/.test(profile.creatorCode)?profile.creatorCode:null
  return <section className="creator-section product-workspace creator-profile-workspace">
    <h2>Profile</h2>{message&&<p className="product-message" role="status">{message}</p>}
    <section className="creator-identity-panel"><ProfileAvatar name={profile.displayName} photoUrl={creatorPhotoUrl(profile.publicCreatorId,profile.profileImage?.fileName)}/><div><span className="creator-profile-label">Creator</span><strong>{profile.displayName||'Creator'}</strong><span>{profile.city||'City not provided'}</span><AccountStatusBadge status={profile.effectiveStatus||'Pending'}/></div>{publicId&&<p><span>Creator ID</span><strong>{publicId}</strong></p>}</section>
    <section className="compact-panel creator-account-panel"><h3>Weymela Account</h3><label>Email<input readOnly value={account?.accountEmail??profile.email??''}/></label><label>Phone<input readOnly value={account?.accountPhone??profile.phoneNumber??''}/></label></section>
    <section className="compact-panel creator-social-editor"><h3>Social Profiles</h3>{drafts.length===0&&<p className="compact-empty">Add at least one social profile to discover matching Promotions.</p>}<div className="social-profile-list">{drafts.map((row,index)=><article className="social-profile-row" key={row.platform}>
      <div className="social-platform-heading"><SocialPlatformIcon platform={row.platform}/><strong>{row.platform}</strong></div>
      <label>Platform<select value={row.platform} onChange={e=>update(index,{platform:e.target.value as SocialPlatform})}>{platforms.map(p=><option key={p} value={p} disabled={drafts.some((x,i)=>i!==index&&x.platform===p)}>{p}</option>)}</select></label>
      <label>{row.platform==='YouTube'?'Channel URL':row.platform==='Facebook'?'Profile/Page URL':'Profile URL'}<input type="url" value={row.profileUrl} onChange={e=>update(index,{profileUrl:e.target.value})}/></label>
      <label>{row.platform==='YouTube'?'Subscriber Count':'Follower Count'}<input type="number" min="0" step="1" value={row.audienceCount} onChange={e=>update(index,{audienceCount:e.target.value})}/></label>
      <div className="creator-social-row-footer"><span className="status-badge">{profile.socialProfiles?.find(x=>x.platform===row.platform)?.verificationStatus??'Pending Review'}</span><button type="button" className="quiet" disabled={drafts.length===1||busy} onClick={()=>setDrafts(drafts.filter((_,i)=>i!==index))}>Remove</button></div>
    </article>)}</div>
    <div className="creator-social-actions"><button type="button" className="quiet" disabled={drafts.length===platforms.length||busy} onClick={add}>Add Social Platform</button><button type="button" disabled={busy||drafts.length===0} onClick={()=>void save()}>{busy?'Saving…':'Save Changes'}</button></div></section>
  </section>
}

export function CreatorDashboard({onSignOut}:{onSignOut:()=>void}){
  const[tab,setTab]=useState<Tab>('home'),[profile,setProfile]=useState<Profile>(),[home,setHome]=useState<Home>(),[earnings,setEarnings]=useState<Earnings>(),[ugcAssignments,setUgcAssignments]=useState<{status:string}[]>([]),[activePromotions,setActivePromotions]=useState<ActivePromotion[]>([]),[error,setError]=useState('')
  const loadProfile=()=>api<Profile>('/api/v1/creators/me').then(setProfile)
  const load=()=>Promise.all([loadProfile(),v3Request<Home>('/api/creator/home').then(x=>{setHome(x);setEarnings(x.earnings)}),v3Request<Earnings>('/api/creator/earnings').then(setEarnings),v3Request<{status:string}[]>('/api/creator/ugc/assignments').then(setUgcAssignments),v3Request<ActivePromotion[]>('/api/creator/campaigns').then(setActivePromotions)]).then(()=>setError('')).catch(e=>setError((e as Error).message))
  useEffect(()=>{void load();const active=()=>{if(document.visibilityState==='visible')void load()};addEventListener('focus',active);return()=>removeEventListener('focus',active)},[])
  useEffect(()=>onActionableRefresh(()=>{void load()}),[])
  const navigate=(target:string)=>{if(target.includes('/ugc'))setTab('ugc');else if(target.includes('request'))setTab('requests');else if(target.includes('earning')||target.includes('payout'))setTab('earnings');else if(target.includes('profile'))setTab('profile');else if(target.includes('promotion')||target.includes('discover'))setTab('discover');else setTab('home')}
  const photo=creatorPhotoUrl(profile?.publicCreatorId,profile?.profileImage?.fileName)
  const inProgress=ugcAssignments.filter(x=>activeAssignment(x.status)).length
  return <AccountChrome role="Creator" name={profile?.displayName} status={profile?.effectiveStatus} photoUrl={photo} onProfile={()=>setTab('profile')} onHelp={()=>location.assign('/help?category=Content%20Creators')} onSignOut={onSignOut} onNavigate={navigate}><div className="creator-dashboard">
    <RoleNavigation role="Creator" label="Creator sections" items={[{id:'home',label:'Home',icon:'home',active:tab==='home'||tab==='requests',onSelect:()=>setTab('home')},{id:'discover',label:'Discover',icon:'discover',active:tab==='discover',onSelect:()=>setTab('discover')},{id:'ugc',label:'UGC',icon:'video',active:tab==='ugc',onSelect:()=>setTab('ugc')},{id:'earnings',label:'Earnings',icon:'payout',active:tab==='earnings',onSelect:()=>setTab('earnings')},{id:'profile',label:'Profile',icon:'profile',active:tab==='profile',onSelect:()=>setTab('profile')}]} />
    {error&&<p className="friendly-error" role="status">{error}</p>}
    {tab==='home'&&<section className="creator-home creator-home-dashboard product-workspace"><h2>Dashboard</h2>
      <div className="creator-dashboard-metrics">
        <article><span>Pending Requests</span><strong>{amount(home?.requests)}</strong></article>
        <article><span>Active Promotions</span><strong>{amount(home?.activeCampaigns)}</strong></article>
        <article><span>UGC in Progress</span><strong>{amount(inProgress)}</strong></article>
        <article><span>Available Earnings</span><strong>{amount(home?.earnings.availableEarnings??earnings?.availableEarnings)}</strong></article>
        <article><span>Minimum to Cash Out</span><strong>{amount(earnings?.minimumToCashOut??home?.earnings.minimumToCashOut)}</strong></article>
      </div>
      <button type="button" className="creator-how-earn" onClick={()=>setTab('earnings')}><span>How You Earn</span><span aria-hidden="true">›</span></button>
      <section className="creator-start"><h3>Start Earning</h3><div className="creator-start-actions"><button type="button" onClick={()=>setTab('discover')}>Discover Promotions</button><button type="button" className="quiet" onClick={()=>setTab('ugc')}>Browse UGC</button><button type="button" className="quiet" onClick={()=>setTab('requests')}>My Requests</button></div></section>
      <section className="creator-recent-promotions"><h3>Recent Active Promotions</h3>{activePromotions.length===0?<p className="compact-empty">No active Promotions yet. Discover Promotions to find an opportunity.</p>:<div className="creator-active-promotion-list">{activePromotions.slice(0,3).map(x=><article key={x.budgetId}><div><strong>{x.title}</strong><span>{x.business?.displayName||'Business'} · {x.type}</span></div><span className="status-badge">{x.status}</span></article>)}</div>}</section>
    </section>}
    {tab==='discover'&&<CreatorDiscoverPromotions/>}{tab==='ugc'&&<CreatorUgc/>}{tab==='requests'&&<CreatorPromotionRequests/>}{tab==='earnings'&&<EarningsView value={earnings}/>} {tab==='profile'&&<ProfileView profile={profile} refresh={async()=>{await loadProfile()}}/>}
  </div></AccountChrome>
}
