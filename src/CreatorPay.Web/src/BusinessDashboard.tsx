import {ReactNode,useEffect,useState} from 'react'
import {api} from './apiClient'
import {AccountChrome} from './AccountChrome'
import {RoleNavigation} from './RoleNavigation'
import {BusinessPromotions} from './PromotionWorkspace'
import {BusinessUgc} from './UgcWorkspace'
import {amount,v3Request} from './v3ProductApi'
import {onActionableRefresh} from './actionableRefresh'
import {ConfirmedSalesWorkspace} from './ConfirmedSalesWorkspace'
import {NavIcon} from './navIcons'

type Tab='home'|'promotions'|'ugc'|'wallet'|'profile'|'cashiers'|'sales'|'checkout'
type Profile={tradingName:string;effectiveStatus:string}
type Home={wallet:{available:number;reserved:number;totalBalance:number};activeCampaigns:number;creatorRequests:number;confirmedSales:number}
type Ugc={status:string}
export function BusinessDashboard({cashiers,checkout,wallet,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[business,setBusiness]=useState<Profile>(),[home,setHome]=useState<Home>(),[ugc,setUgc]=useState<Ugc[]>([]),[error,setError]=useState('')
 const load=()=>Promise.all([api<Profile>('/api/v1/merchants/me').then(setBusiness),v3Request<Home>('/api/business/home').then(setHome),v3Request<Ugc[]>('/api/business/ugc').then(setUgc)]).catch(e=>setError((e as Error).message))
 useEffect(()=>{void load();const active=()=>{if(document.visibilityState==='visible')void load()};addEventListener('focus',active);return()=>removeEventListener('focus',active)},[])
 useEffect(()=>onActionableRefresh(()=>{void load()}),[])
 const navigate=(target:string)=>{if(target.includes('/ugc'))setTab('ugc');else if(target.includes('promotion'))setTab('promotions');else if(target.includes('wallet')||target.includes('deposit'))setTab('wallet');else if(target.includes('checkout'))setTab('checkout');else if(target.includes('sale'))setTab('sales');else if(target.includes('profile'))setTab('profile');else setTab('home')}
 return <AccountChrome role="Business" name={business?.tradingName} status={business?.effectiveStatus} onProfile={()=>setTab('profile')} onManagement={()=>setTab('cashiers')} onHelp={()=>location.assign('/help?category=Businesses')} onSignOut={onSignOut} onNavigate={navigate}>
  <div className="creator-dashboard business-dashboard"><RoleNavigation role="Business" label="Business sections" items={[{id:'home',label:'Home',icon:'home',active:tab==='home'||tab==='cashiers'||tab==='sales'||tab==='checkout',onSelect:()=>setTab('home')},{id:'promotions',label:'Promotions',icon:'ads',active:tab==='promotions',onSelect:()=>setTab('promotions')},{id:'ugc',label:'UGC',icon:'video',active:tab==='ugc',onSelect:()=>setTab('ugc')},{id:'wallet',label:'Wallet',icon:'wallet',active:tab==='wallet',onSelect:()=>setTab('wallet')},{id:'profile',label:'Profile',icon:'profile',active:tab==='profile',onSelect:()=>setTab('profile')}]} />
   {error&&<p className="friendly-error">{error}</p>}
   {tab==='home'&&<section className="business-home product-workspace"><h2>Home</h2><div className="summary-grid"><button className="summary-card" onClick={()=>setTab('wallet')}><span>Available</span><strong>{amount(home?.wallet.available)}</strong></button><button className="summary-card" onClick={()=>setTab('wallet')}><span>Reserved</span><strong>{amount(home?.wallet.reserved)}</strong></button><button className="summary-card" onClick={()=>setTab('promotions')}><span>Active Promotions</span><strong>{home?.activeCampaigns??0}</strong></button><button className="summary-card" onClick={()=>setTab('ugc')}><span>Open UGC</span><strong>{ugc.filter(x=>x.status==='Open').length}</strong></button><button className="summary-card" onClick={()=>setTab('promotions')}><span>Pending Requests</span><strong>{home?.creatorRequests??0}</strong></button><button className="summary-card" onClick={()=>setTab('sales')}><span>Sales</span><strong>{home?.confirmedSales??0}</strong></button></div><div className="business-quick-actions"><h3>Quick Actions</h3><div><button onClick={()=>setTab('promotions')}><NavIcon name="ads" size={20}/>Create Promotion</button><button onClick={()=>setTab('ugc')}><NavIcon name="video" size={20}/>Create UGC</button><button onClick={()=>setTab('wallet')}><NavIcon name="wallet" size={20}/>Add Funds</button><button onClick={()=>setTab('checkout')}><NavIcon name="checkout" size={20}/>Checkout</button></div><p>Scan a Customer QR or record an eligible Sale.</p></div></section>}
   {tab==='promotions'&&<BusinessPromotions/>}{tab==='ugc'&&<BusinessUgc/>}{tab==='wallet'&&wallet}{tab==='checkout'&&checkout}{tab==='cashiers'&&cashiers}{tab==='sales'&&<ConfirmedSalesWorkspace/>}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
