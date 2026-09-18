import {FormEvent,useEffect,useState} from 'react'
import jsQR from 'jsqr'
import {api} from './apiClient'
import {AccountStatusBadge} from './AccountChrome'
import {formatAmount,formatDate,formatTime} from './displayFormat'
import {NavIcon} from './navIcons'
import {v3Post,v3Request} from './v3ProductApi'

type Mode='cashier'|'merchant'
type Tab='checkout'|'recent'|'profile'
type EntryMode='scan'|'manual'
type Staff={firstName:string;lastName:string;username:string;businessName:string;effectiveStatus:string;locations:{name:string}[]}
type V3Offer={sessionId:string;offer:string;business:{displayName:string};creator?:{displayName:string};customer:string;expiresAtUtc:string;source:'VIEW_AND_SALE_PROMOTION'|'UGC_CUSTOMER_OFFER';customerDiscountPercent?:number}
type V3Sale={saleId:string;purchaseAmount:{amount:number};totalBusinessCharge:{amount:number};createdAtUtc:string;customerPays?:{amount:number};customerDiscount?:{amount:number};source:string}
type RecentSale={id:string;offer:string;source:string;purchaseAmount:number;customerDiscount:number;customerPays:number;platformFee:number;createdAtUtc:string}

export function CashierCheckoutWorkspace({initialQrPayload,mode='cashier'}:{initialQrPayload?:string;mode?:Mode}={}){
  const merchantMode=mode==='merchant'
  const[tab,setTab]=useState<Tab>('checkout'),[entryMode,setEntryMode]=useState<EntryMode>(initialQrPayload?'manual':'scan')
  const[staff,setStaff]=useState<Staff>(),[recent,setRecent]=useState<RecentSale[]>([]),[token,setToken]=useState(initialQrPayload??'')
  const[offer,setOffer]=useState<V3Offer>(),[amount,setAmount]=useState(''),[sale,setSale]=useState<V3Sale>()
  const[message,setMessage]=useState(''),[busy,setBusy]=useState(false)
  const load=async()=>{try{setRecent(await v3Request<RecentSale[]>('/api/checkout/recent'));if(!merchantMode)setStaff(await api<Staff>('/api/v1/cashier/me'))}catch(error){console.error(error);setMessage("We couldn't load this information.")}}
  useEffect(()=>{void load()},[])

  async function scan(file?:File){if(!file)return;setMessage('');try{const bitmap=await createImageBitmap(file),canvas=document.createElement('canvas');canvas.width=bitmap.width;canvas.height=bitmap.height;const context=canvas.getContext('2d',{willReadFrequently:true});if(!context)throw new Error();context.drawImage(bitmap,0,0);const image=context.getImageData(0,0,canvas.width,canvas.height),decoded=jsQR(image.data,image.width,image.height);if(!decoded)throw new Error();setToken(decoded.data);await resolve(decoded.data)}catch{setMessage("We couldn't read that QR image. Try again or enter the scanned code manually.")}}
  async function resolve(value=token){if(!value.trim()||busy)return;setBusy(true);setMessage('');try{setOffer(await v3Post<V3Offer>('/api/checkout/resolve',{token:value.trim()}));setSale(undefined)}catch(error){setOffer(undefined);setMessage((error as Error).message)}finally{setBusy(false)}}
  async function confirm(event:FormEvent){event.preventDefault();if(!offer||busy)return;setBusy(true);setMessage('');try{const saved=await v3Post<V3Sale>('/api/checkout/confirm',{token:token.trim(),purchaseAmount:Number(amount)});setSale(saved);setOffer(undefined);setAmount('');setToken('');setMessage('Sale completed.');await load()}catch(error){setMessage((error as Error).message)}finally{setBusy(false)}}
  function next(){setSale(undefined);setOffer(undefined);setToken('');setAmount('');setMessage('')}

  const tabs:[Tab,string,Parameters<typeof NavIcon>[0]['name']][]=merchantMode
    ?[['checkout','Checkout','checkout'],['recent','Recent Sales','sales']]
    :[['checkout','Checkout','checkout'],['recent','Recent Sales','sales'],['profile','Profile','profile']]
  return <div className="creator-dashboard cashier-dashboard">
    <nav className="cashier-tabs" aria-label="Cashier sections">{tabs.map(([id,label,icon])=><button key={id} className={tab===id?'active':''} onClick={()=>setTab(id)}><NavIcon name={icon} size={24}/><span>{label}</span></button>)}</nav>
    {message&&<p role="status" className={message==='Sale completed.'?'success-note':'friendly-error'}>{message}</p>}
    {tab==='checkout'&&<section className="creator-section cashier-scan"><h2>Checkout</h2><p>Scan a Customer QR for an eligible View &amp; Sale or UGC Customer Offer.</p><div className="segmented" aria-label="Checkout entry method"><button className={entryMode==='scan'?'active':''} onClick={()=>setEntryMode('scan')}>Scan Customer QR</button><button className={entryMode==='manual'?'active':''} onClick={()=>setEntryMode('manual')}>Enter Manually</button></div>
      {sale?<aside className="success-note"><strong>Sale completed</strong><p>Sale {formatAmount(sale.purchaseAmount.amount)}</p>{sale.customerDiscount&&<p>Customer discount {formatAmount(sale.customerDiscount.amount)}</p>}{sale.customerPays&&<p>Customer pays {formatAmount(sale.customerPays.amount)}</p>}<button onClick={next}>Next Customer</button></aside>:offer?<><article className="compact-panel checkout-offer-context"><h3>{offer.business.displayName}</h3><p>{offer.offer}</p><dl><div><dt>Customer</dt><dd>{offer.customer.split(' · ')[0]}</dd></div><div><dt>Offer type</dt><dd>{offer.source==='UGC_CUSTOMER_OFFER'?'UGC Customer Offer':'View & Sale'}</dd></div>{offer.customerDiscountPercent!=null&&<div><dt>Customer discount</dt><dd>{offer.customerDiscountPercent}%</dd></div>}</dl></article><form className="panel form cashier-sale" onSubmit={confirm}><label>Sale amount<input required type="number" min="0.01" step="0.01" value={amount} onChange={e=>setAmount(e.target.value)}/></label><button className="full" disabled={busy||!amount}>{busy?'Completing…':'Complete Sale'}</button><button type="button" className="quiet" onClick={next}>Back</button></form></>:entryMode==='scan'?<section className="panel qr-scan-action"><NavIcon name="checkout" size={44}/><h3>Scan Customer QR</h3><label className="button-like">Open Camera<input className="sr-only" type="file" accept="image/*" capture="environment" onChange={e=>void scan(e.target.files?.[0])}/></label></section>:<form className="panel form cashier-sale" onSubmit={e=>{e.preventDefault();void resolve()}}><label>Scanned QR code<input required type="password" autoComplete="off" spellCheck={false} maxLength={256} value={token} onChange={e=>setToken(e.target.value)}/></label><button className="full" disabled={busy||!token.trim()}>{busy?'Checking…':'Continue'}</button></form>}
    </section>}
    {tab==='recent'&&<section className="creator-section"><h2>Recent Sales</h2>{recent.length?<div className="product-list">{recent.map(x=><article key={x.id}><div><strong>{x.offer}</strong><span>{x.source==='UGC_CUSTOMER_OFFER'?'UGC Customer Offer':'View & Sale'} · {formatDate(x.createdAtUtc)} {formatTime(x.createdAtUtc)}</span></div><strong>{formatAmount(x.purchaseAmount)}</strong></article>)}</div>:<p className="compact-empty">No recent Sales.</p>}</section>}
    {!merchantMode&&tab==='profile'&&<section className="creator-section"><h2>Profile</h2><div className="compact-panel"><strong>{staff?`${staff.firstName} ${staff.lastName}`:'Cashier'}</strong>{staff&&<AccountStatusBadge status={staff.effectiveStatus}/>}<p>{staff?.businessName}</p><p>{staff?.locations.map(x=>x.name).join(', ')}</p><small>Username: {staff?.username}</small></div></section>}
  </div>
}
