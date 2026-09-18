import {FormEvent,ReactNode,useEffect,useState} from 'react'
import {api} from './apiClient'
import {AccountChrome} from './AccountChrome'
import {RoleNavigation} from './RoleNavigation'
import {BusinessPromotions} from './PromotionWorkspace'
import {BusinessUgc} from './UgcWorkspace'
import {amount,v3Post,v3Request} from './v3ProductApi'
import {onActionableRefresh} from './actionableRefresh'
import {ConfirmedSalesWorkspace} from './ConfirmedSalesWorkspace'

type Tab='home'|'promotions'|'ugc'|'wallet'|'profile'|'cashiers'|'sales'
type Profile={tradingName:string;effectiveStatus:string}
type Home={wallet:{available:number;reserved:number;totalBalance:number};activeCampaigns:number;creatorRequests:number;confirmedSales:number}
type Ugc={status:string}
type Movement={id:string;label:string;amount:number;atUtc:string}
type Wallet={totalBalance:number;available:number;reserved:number;history:Movement[]}
type Deposit={id:string;amount:number;status:string;submittedAtUtc:string}

function BusinessWallet(){
 const[wallet,setWallet]=useState<Wallet>(),[deposits,setDeposits]=useState<Deposit[]>(),[message,setMessage]=useState(''),[adding,setAdding]=useState(false)
 const load=()=>Promise.all([v3Request<Wallet>('/api/business/wallet').then(setWallet),v3Request<Deposit[]>('/api/business/deposit-requests').then(setDeposits)]).catch(e=>setMessage((e as Error).message))
 useEffect(()=>{void load()},[])
 async function submit(event:FormEvent<HTMLFormElement>){event.preventDefault();const form=new FormData(event.currentTarget);try{await v3Post('/api/business/deposit-requests',{amount:Number(form.get('amount')),externalReference:form.get('reference'),proofReference:form.get('proof')||null});setMessage('Funding request submitted for review.');setAdding(false);await load()}catch(e){setMessage((e as Error).message)}}
 return <section className="product-workspace"><div className="product-title"><h2>Wallet</h2><button onClick={()=>setAdding(!adding)}>Add Funds</button></div>{message&&<p className="product-message" role="status">{message}</p>}{wallet&&<><div className="summary-grid"><article><span>Available</span><strong>{amount(wallet.available)}</strong></article><article><span>Reserved</span><strong>{amount(wallet.reserved)}</strong></article></div>{adding&&<form className="panel product-form" onSubmit={e=>void submit(e)}><label>Amount<input name="amount" type="number" min="0.01" step="0.01" required/></label><label>Payment reference<input name="reference" required maxLength={200}/></label><label>Proof reference <small>Optional secure URL/reference</small><input name="proof" maxLength={500}/></label><div className="actions"><button>Submit for review</button><button className="quiet" type="button" onClick={()=>setAdding(false)}>Cancel</button></div></form>}<h3>Recent activity</h3>{wallet.history.length===0?<p className="compact-empty">No wallet activity yet.</p>:<div className="product-list">{wallet.history.map(x=><article key={x.id}><div><strong>{x.label}</strong><span>{new Date(x.atUtc).toLocaleDateString()}</span></div><strong>{amount(x.amount)}</strong></article>)}</div>}</>}
 {deposits&&deposits.length>0&&<><h3>Funding requests</h3><div className="product-list">{deposits.map(x=><article key={x.id}><div><strong>{amount(x.amount)}</strong><span>{new Date(x.submittedAtUtc).toLocaleDateString()}</span></div><span className="status-badge">{x.status}</span></article>)}</div></>}
 </section>
}

export function BusinessDashboard({cashiers,profile,onSignOut}:{cashiers:ReactNode;checkout:ReactNode;wallet:ReactNode;profile:ReactNode;onSignOut:()=>void}){
 const[tab,setTab]=useState<Tab>('home'),[business,setBusiness]=useState<Profile>(),[home,setHome]=useState<Home>(),[ugc,setUgc]=useState<Ugc[]>([]),[error,setError]=useState('')
 const load=()=>Promise.all([api<Profile>('/api/v1/merchants/me').then(setBusiness),v3Request<Home>('/api/business/home').then(setHome),v3Request<Ugc[]>('/api/business/ugc').then(setUgc)]).catch(e=>setError((e as Error).message))
 useEffect(()=>{void load();const active=()=>{if(document.visibilityState==='visible')void load()};addEventListener('focus',active);return()=>removeEventListener('focus',active)},[])
 useEffect(()=>onActionableRefresh(()=>{void load()}),[])
 const navigate=(target:string)=>{if(target.includes('/ugc'))setTab('ugc');else if(target.includes('promotion'))setTab('promotions');else if(target.includes('wallet')||target.includes('deposit'))setTab('wallet');else if(target.includes('sale'))setTab('sales');else if(target.includes('profile'))setTab('profile');else setTab('home')}
 return <AccountChrome role="Business" name={business?.tradingName} status={business?.effectiveStatus} onProfile={()=>setTab('profile')} onManagement={()=>setTab('cashiers')} onHelp={()=>location.assign('/help?category=Businesses')} onSignOut={onSignOut} onNavigate={navigate}>
  <div className="creator-dashboard business-dashboard"><RoleNavigation role="Business" label="Business sections" items={[{id:'home',label:'Home',icon:'home',active:tab==='home'||tab==='cashiers'||tab==='sales',onSelect:()=>setTab('home')},{id:'promotions',label:'Promotions',icon:'ads',active:tab==='promotions',onSelect:()=>setTab('promotions')},{id:'ugc',label:'UGC',icon:'video',active:tab==='ugc',onSelect:()=>setTab('ugc')},{id:'wallet',label:'Wallet',icon:'wallet',active:tab==='wallet',onSelect:()=>setTab('wallet')},{id:'profile',label:'Profile',icon:'profile',active:tab==='profile',onSelect:()=>setTab('profile')}]} />
   {error&&<p className="friendly-error">{error}</p>}
   {tab==='home'&&<section className="business-home product-workspace"><h2>Home</h2><div className="summary-grid"><button className="summary-card" onClick={()=>setTab('wallet')}><span>Available Balance</span><strong>{amount(home?.wallet.available)}</strong></button><button className="summary-card" onClick={()=>setTab('wallet')}><span>Reserved</span><strong>{amount(home?.wallet.reserved)}</strong></button><button className="summary-card" onClick={()=>setTab('promotions')}><span>Active Promotions</span><strong>{home?.activeCampaigns??0}</strong></button><button className="summary-card" onClick={()=>setTab('ugc')}><span>Open UGC</span><strong>{ugc.filter(x=>x.status==='Open').length}</strong></button><button className="summary-card" onClick={()=>setTab('promotions')}><span>Pending Requests</span><strong>{home?.creatorRequests??0}</strong></button><button className="summary-card" onClick={()=>setTab('sales')}><span>Sales</span><strong>{home?.confirmedSales??0}</strong></button></div><div className="business-quick-actions"><h3>Quick Actions</h3><div><button onClick={()=>setTab('promotions')}>Create Promotion</button><button onClick={()=>setTab('ugc')}>Create UGC</button><button onClick={()=>setTab('wallet')}>Add Funds</button></div></div></section>}
   {tab==='promotions'&&<BusinessPromotions/>}{tab==='ugc'&&<BusinessUgc/>}{tab==='wallet'&&<BusinessWallet/>}{tab==='cashiers'&&cashiers}{tab==='sales'&&<ConfirmedSalesWorkspace/>}{tab==='profile'&&profile}
  </div>
 </AccountChrome>
}
