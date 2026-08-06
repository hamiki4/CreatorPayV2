import {useEffect,useState} from 'react'
import {brand} from './brand'
import {businessTypes} from './AuthWorkspace'

type FileMetadata={fileName:string;contentType:string;sizeBytes:number}
type DocumentMetadata=FileMetadata&{documentType:string}
type Profile={legalBusinessName:string;tradingName:string;businessType:string;taxRegistrationNumber?:string;phoneNumber:string;email:string;businessAddress:string;city:string;region:string;country:string;timeZone:string;merchantStatus:string;accountStatus:string;registrationProgress:number;nextStep:string;isEmailVerified:boolean;isPhoneVerified:boolean;logo?:FileMetadata;documents:DocumentMetadata[]}
type Wallet={availableBalance:number;status:string;currencyCode:string}
type Trial={maximumTransactions:number;confirmedTransactionCount:number;maximumCommissionAmount:number;commissionConsumed:number;status:string}
type Location={id:string;isActive:boolean}
type Cashier={id:string;isActive:boolean}

const root=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'')
const token=()=>localStorage.getItem('creatorpay_access_token')??''
const locale=(localStorage.getItem('weymela_locale') as 'en' | 'am') ?? 'en'
async function request<T>(path:string,method='GET',body?:unknown):Promise<T>{
  const r=await fetch(`${root}${path}`,{method,headers:{Authorization:`Bearer ${token()}`,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});
  const b=await r.json().catch(()=>({}));
  if(!r.ok) throw Error(b.detail??`Request failed (${r.status}).`);
  return b;
}

export function MerchantWorkspace(){
 const[profile,setProfile]=useState<Profile>(),[wallet,setWallet]=useState<Wallet>(),[trial,setTrial]=useState<Trial>(),[locations,setLocations]=useState<Location[]>([]),[cashiers,setCashiers]=useState<Cashier[]>([]),[error,setError]=useState(''),[businessType,setBusinessType]=useState(''),[saving,setSaving]=useState(false)
 useEffect(()=>{Promise.all([request<Profile>('/api/v1/merchants/me'),request<Location[]>('/api/v1/merchant/locations'),request<Cashier[]>('/api/v1/merchant/cashiers')]).then(([p,l,c])=>{setProfile(p);setBusinessType(p.businessType);setLocations(l);setCashiers(c)}).catch(e=>setError(e.message));request<Wallet>('/api/v1/merchant/wallet').then(setWallet).catch(()=>{});request<Trial>('/api/v1/merchant/trial-credit').then(setTrial).catch(()=>{})},[])
 if(!profile)return error?<section className="panel" role="alert"><h2>Merchant status</h2><p className="error">{error}</p></section>:<p>Loading merchant dashboard...</p>
 const saveBusinessType=async()=>{setSaving(true);setError('');try{const updated=await request<Profile>('/api/v1/merchants/me','PUT',{legalBusinessName:profile.legalBusinessName,tradingName:profile.tradingName,businessType,taxRegistrationNumber:profile.taxRegistrationNumber??null,phoneNumber:profile.phoneNumber,email:profile.email,businessAddress:profile.businessAddress,city:profile.city,region:profile.region,country:profile.country,timeZone:profile.timeZone,logo:profile.logo??null,documents:profile.documents??[]});setProfile(updated);setBusinessType(updated.businessType)}catch(e){setError((e as Error).message)}finally{setSaving(false)}}
 const funding=wallet?.status??(profile.merchantStatus==='Active'?'Funding status unavailable':'Not available until approval')
 return <section aria-labelledby="merchant-dashboard"><div className="title"><div><p className="eyebrow">{brand.productName} merchant</p><h2 id="merchant-dashboard">{profile.tradingName}</h2></div><span className="status">{profile.merchantStatus}</span></div>{profile.merchantStatus!=='Active'&&<aside role="status"><strong>Next step:</strong> {profile.nextStep}</aside>}{wallet?.status==='LowBalance'&&<p className="offline-warning">Low balance. Add pilot funding before the wallet becomes restricted.</p>}{wallet?.status==='FundingRequired'&&<p className="offline-warning">Funding Required. Financial checkout remains unavailable.</p>}<div className="summary-grid"><article className="summary-card"><span>Trial status</span><strong>{trial?.status??'Not started'}</strong><small>{trial?`${trial.confirmedTransactionCount}/${trial.maximumTransactions} transactions · ${trial.commissionConsumed}/${trial.maximumCommissionAmount} ${brand.currency}`:'Available after approval'}</small></article><article className="summary-card"><span>Funding status</span><strong>{funding}</strong><small>Wallet projection: {wallet?.availableBalance??0} {wallet?.currencyCode??brand.currency}</small></article><article className="summary-card"><span>Locations</span><strong>{locations.filter(x=>x.isActive).length}</strong><small>{locations.length} total</small></article><article className="summary-card"><span>Cashiers</span><strong>{cashiers.filter(x=>x.isActive).length}</strong><small>{cashiers.length} invited or created</small></article></div><div className="panel"><h3>Business profile</h3>{error&&<p className="error" role="alert">{error}</p>}<label>Business Type<select required value={businessType} onChange={e=>setBusinessType(e.target.value)}>{!businessTypes.some(x=>x.value===businessType)&&businessType&&<option value={businessType}>{businessType} (legacy value)</option>}{businessTypes.map(x=><option key={x.value} value={x.value}>{x[locale]}</option>)}</select></label><button type="button" disabled={saving||!businessType||businessType===profile.businessType} onClick={saveBusinessType}>{saving?'Saving…':'Save Business Type'}</button></div><div className="panel"><h3>Registration status</h3><progress value={profile.registrationProgress} max="100" aria-label="Registration progress"/><p>{profile.registrationProgress}% complete · Email {profile.isEmailVerified?'verified':'not verified'} · Phone {profile.isPhoneVerified?'verified':'not verified'}</p><p>Campaigns, creator requests, featured creator, and recent transactions appear in their workspaces below when available.</p></div></section>
}
