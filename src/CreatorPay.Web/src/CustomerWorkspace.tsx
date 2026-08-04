import {useEffect,useState} from 'react'

type Wallet={availableCashback:number;reservedCashback:number;paidLifetime:number;currencyCode:string}
type Checkout={id:string;publicCheckoutId:string;status:string;campaignId:string;merchantId:string;purchaseAmount?:number;cashbackAmount?:number;expiresAtUtc:string;qrPayload?:string}
type Promotion={campaignId:string;campaignCode:string;merchantName:string;creatorName:string;featured:boolean;expiresAtUtc:string}
type Merchant={merchantId:string;tradingName:string;zoneCode?:string;featuredLabel:string;promotions:Promotion[]}
const base=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'')
const token=()=>localStorage.getItem('creatorpay_access_token')??''
async function api<T>(path:string,method='GET',body?:unknown):Promise<T>{const r=await fetch(`${base}${path}`,{method,headers:{Authorization:`Bearer ${token()}`,'Content-Type':'application/json','Idempotency-Key':crypto.randomUUID()},body:body?JSON.stringify(body):undefined});const v=await r.json().catch(()=>({}));if(!r.ok)throw Error(v.detail??`Request failed (${r.status}).`);return v}

export function CustomerWorkspace(){
  const [wallet,setWallet]=useState<Wallet>(),[checkouts,setCheckouts]=useState<Checkout[]>([]),[merchant,setMerchant]=useState<Merchant>(),[selectedCampaign,setSelectedCampaign]=useState(''),[message,setMessage]=useState('')
  const discoveryId=location.pathname.match(/^\/m\/([^/]+)/)?.[1]
  const load=()=>{api<Wallet>('/api/v1/customer/wallet').then(setWallet).catch(e=>setMessage(e.message));api<Checkout[]>('/api/v1/customer/checkouts').then(setCheckouts).catch(e=>setMessage(e.message));if(discoveryId)api<Merchant>(`/api/v1/discovery/merchant-qr/${encodeURIComponent(discoveryId)}`).then(setMerchant).catch(e=>setMessage(e.message))}
  useEffect(()=>{load()},[])
  async function create(campaignId:string){try{const x=await api<Checkout>('/api/v1/customer/checkouts','POST',{campaignId});setMessage(`Checkout QR (expires in 3 minutes): ${x.qrPayload}`);load()}catch(e){setMessage((e as Error).message)}}
  async function decide(x:Checkout,approve:boolean){try{await api(`/api/v1/customer/checkouts/${x.id}/${approve?'approve':'reject'}`,'POST');setMessage(approve?'Purchase approved and cashback posted.':'Purchase rejected.');load()}catch(e){setMessage((e as Error).message)}}
  async function payout(){try{await api('/api/v1/customer/cashback-payouts','POST');setMessage('Full-balance manual payout requested.');load()}catch(e){setMessage((e as Error).message)}}
  return <section><h2>{merchant?.tradingName??'Customer cashback'}</h2>{message&&<aside>{message}</aside>}
    {merchant&&<div className="panel"><p>{merchant.zoneCode??''}</p>{merchant.promotions.length===0?<p className="empty">No active creator promotions are available.</p>:<><div className="cards">{merchant.promotions.map(p=><article key={p.campaignId}><span>{p.featured?'Featured creator':'Creator promotion'}</span><strong>{p.creatorName}</strong><p>Valid until {new Date(p.expiresAtUtc).toLocaleString()}</p><label><input type="radio" name="creator-campaign" checked={selectedCampaign===p.campaignId} onChange={()=>setSelectedCampaign(p.campaignId)}/> Select this creator</label></article>)}</div><button disabled={!selectedCampaign} onClick={()=>create(selectedCampaign)}>Use at Checkout</button></>}</div>}
    <div className="summary-grid"><article><span>Available cashback</span><strong>{wallet?.availableCashback??0} {wallet?.currencyCode??'ETB'}</strong></article><article><span>Reserved payout</span><strong>{wallet?.reservedCashback??0} ETB</strong></article></div>
    {(wallet?.availableCashback??0)>=1000&&<button onClick={payout}>Request full cashback payout</button>}
    <div className="cards">{checkouts.map(x=><article key={x.id}><span>{x.status}</span><strong>{x.publicCheckoutId}</strong><p>{x.purchaseAmount??'—'} ETB · cashback {x.cashbackAmount??'—'} ETB</p>{x.status==='AwaitingCustomerApproval'&&<div><button onClick={()=>decide(x,true)}>Approve</button><button className="danger" onClick={()=>decide(x,false)}>Reject</button></div>}</article>)}</div>
  </section>
}
