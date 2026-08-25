import {FormEvent,useEffect,useState} from 'react'
import {api,apiBlob} from './apiClient'
const money=(x:number)=>new Intl.NumberFormat('en-ET',{style:'currency',currency:'ETB'}).format(x)
type Wallet={availableBalance:number;heldBalance:number;status:string;currencyCode:string};type Deposit={id:string;merchantId:string;businessName:string;amount:number;status:string;submittedAtUtc:string;failureReason?:string}
const depositStatus=(x:string)=>x==='PendingVerification'?'Pending Review':x==='Completed'?'Approved':x==='Failed'?'Rejected':x
export function MerchantWalletWorkspace(){const[w,setW]=useState<Wallet>();const[d,setD]=useState<Deposit[]>([]);const[walletError,setWalletError]=useState('');const[depositError,setDepositError]=useState('');const load=async()=>{const[walletResult,depositResult]=await Promise.allSettled([api<Wallet>('/api/v1/merchant/wallet'),api<Deposit[]>('/api/v1/merchant/deposits')]);if(walletResult.status==='fulfilled'){setW(walletResult.value);setWalletError('')}else{console.error(walletResult.reason);setW(undefined);setWalletError('Wallet information is temporarily unavailable.')}if(depositResult.status==='fulfilled'){setD(depositResult.value);setDepositError('')}else{console.error(depositResult.reason);setD([]);setDepositError('Deposit history is temporarily unavailable.')}};useEffect(()=>{void load()},[]);return <section className="creator-section"><div className="title"><h2>Wallet</h2><span>{w?.status??'Loading…'}</span></div>{walletError&&<p className="friendly-error">{walletError}</p>}{w&&<div className="compact-panel"><p className="eyebrow">Available balance</p><h2>{money(w.availableBalance)}</h2>{w.status==='LowBalance'&&<p className="friendly-error">Low balance — submit a deposit to continue purchases.</p>}</div>}<DepositForm done={load}/><h3>Deposit history</h3>{depositError&&<p className="friendly-error">{depositError}</p>}{d.length?<div className="cards compact-deposits">{d.map(x=><article key={x.id}><strong>{money(x.amount)}</strong><span>{new Date(x.submittedAtUtc).toLocaleDateString()}</span><span className="status-badge">{depositStatus(x.status)}</span>{x.failureReason&&<small>{x.failureReason}</small>}</article>)}</div>:<p className="compact-empty">No deposits yet.</p>}</section>}
const proofTypes=['image/jpeg','image/png','image/webp']
const maximumProofBytes=5*1024*1024
function DepositForm({done}:{done:()=>Promise<unknown>}){
  const[amount,setAmount]=useState(''),[proof,setProof]=useState<File>(),[previewUrl,setPreviewUrl]=useState<string>(),[msg,setMsg]=useState(''),[busy,setBusy]=useState(false)
  useEffect(()=>()=>{if(previewUrl)URL.revokeObjectURL(previewUrl)},[previewUrl])
  function selectProof(file?:File){
    if(!file){setProof(undefined);setPreviewUrl(undefined);return}
    if(!proofTypes.includes(file.type)){setProof(undefined);setPreviewUrl(undefined);setMsg('Choose a JPG, PNG, or WEBP image.');return}
    if(file.size>maximumProofBytes){setProof(undefined);setPreviewUrl(undefined);setMsg('Choose an image no larger than 5 MB.');return}
    setProof(file);setPreviewUrl(URL.createObjectURL(file));setMsg('')
  }
  async function submit(e:FormEvent){e.preventDefault();if(!proof)return;setBusy(true);setMsg('');try{const form=new FormData();form.append('amount',amount);form.append('proof',proof);await api('/api/v1/merchant/deposits',{method:'POST',headers:{'Idempotency-Key':crypto.randomUUID()},body:form});setMsg('Deposit submitted. Status: Pending Review.');setAmount('');setProof(undefined);setPreviewUrl(undefined);await done()}catch(x){setMsg((x as Error).message)}finally{setBusy(false)}}
  return <form className="compact-panel form" onSubmit={submit}><h3>Submit Deposit</h3><label>Amount (ETB)<input required min="0.01" step="0.01" type="number" value={amount} onChange={e=>setAmount(e.target.value)}/></label><label>Upload Proof of Payment<input required type="file" accept="image/jpeg,image/png,image/webp" onChange={e=>selectProof(e.target.files?.[0])}/></label><small>JPG, PNG, or WEBP · maximum 5 MB</small>{proof&&previewUrl&&<div className="deposit-proof-selection"><img src={previewUrl} alt="Selected payment proof preview"/><small>{proof.name}</small></div>}<button disabled={busy||!proof}>{busy?'Submitting…':'Submit Deposit'}</button>{msg&&<aside role="status">{msg}</aside>}</form>
}
function DepositProofPreview({id,onOpen}:{id:string;onOpen:()=>void}){
  const[url,setUrl]=useState('')
  useEffect(()=>{
    let active=true
    let objectUrl=''
    const preview=async()=>{
      try{
        const blob=await apiBlob(`/api/v1/admin/deposits/${id}/proof`)
        if(!active)return
        objectUrl=URL.createObjectURL(blob)
        setUrl(objectUrl)
      }catch{
        if(active)setUrl('')
      }
    }
    void preview()
    return()=>{
      active=false
      if(objectUrl)URL.revokeObjectURL(objectUrl)
    }
  },[id])
  if(!url)return <button className="quiet" onClick={onOpen}>View Proof</button>
  return <button type="button" className="deposit-proof-thumb" onClick={onOpen}><img src={url} alt="Deposit proof preview"/><span>View</span></button>
}
export function AdminDepositWorkspace(){const[d,setD]=useState<Deposit[]>([]);const[msg,setMsg]=useState('');const load=()=>api<Deposit[]>('/api/v1/admin/deposits').then(setD);useEffect(()=>{void load()},[]);async function proof(x:Deposit){try{const blob=await apiBlob(`/api/v1/admin/deposits/${x.id}/proof`),url=URL.createObjectURL(blob);window.open(url,'_blank','noopener,noreferrer');setTimeout(()=>URL.revokeObjectURL(url),60_000)}catch(e){console.error(e);setMsg('Payment proof is temporarily unavailable.')}}async function act(x:Deposit,approve:boolean){try{await api(`/api/v1/admin/deposits/${x.id}/${approve?'approve':'reject'}`,{method:'POST',headers:{'Idempotency-Key':crypto.randomUUID()},body:approve?undefined:JSON.stringify({reason:prompt('Optional rejection reason')||'Rejected by Platform Admin'})});setMsg(approve?'Deposit approved and wallet credited.':'Deposit rejected.');await load()}catch(e){setMsg((e as Error).message)}}return <section><h2>Deposits</h2>{msg&&<aside>{msg}</aside>}{d.length?<div className="deposit-review-list"><div className="deposit-review-row headings"><span>Business</span><span>Amount</span><span>Submitted</span><span>Status</span><span>Proof</span><span>Action</span></div>{d.map(x=>{const pending=x.status==='PendingVerification';return <article className="deposit-review-row" key={x.id}><strong data-label="Business">{x.businessName}</strong><span data-label="Amount">{money(x.amount)}</span><span data-label="Submitted">{new Date(x.submittedAtUtc).toLocaleDateString()}</span><span data-label="Status"><span className="status-badge">{depositStatus(x.status)}</span></span><span data-label="Proof"><DepositProofPreview id={x.id} onOpen={()=>void proof(x)}/></span><span className="deposit-actions" data-label="Action">{pending&&<><button type="button" onClick={()=>void act(x,true)}>Approve</button><button type="button" className="danger" onClick={()=>void act(x,false)}>Reject</button></>}</span>{x.failureReason&&<small>{x.failureReason}</small>}</article>})}</div>:<p className="empty">No deposits submitted.</p>}</section>}
