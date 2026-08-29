import {useEffect,useState} from 'react'
import {api} from './apiClient'

const money=(x:number)=>new Intl.NumberFormat('en-ET',{style:'currency',currency:'ETB'}).format(x)
type Sale={transactionId:string;publicTransactionId:string;confirmedAtUtc:string;saleAmount:number;commissionAmount:number;creatorName:string;creatorId:string;cashierName:string;status:string;locationName?:string}
type Report={confirmedSales:number;totalSalesAmount:number;totalCommissionAmount:number;sales:Sale[]}

export function ConfirmedSalesWorkspace(){
  const [report,setReport]=useState<Report>()
  const [creatorName,setCreatorName]=useState('')
  const [cashierName,setCashierName]=useState('')
  const [error,setError]=useState('')
  const load=()=>{
    const q=new URLSearchParams()
    if(creatorName.trim())q.set('creatorName',creatorName.trim())
    if(cashierName.trim())q.set('cashierName',cashierName.trim())
    api<Report>(`/api/v1/merchant/confirmed-sales?${q}`).then(x=>{setReport(x);setError('')}).catch(e=>setError(e.message))
  }
  useEffect(()=>{void load()},[])
  function clear(){setCreatorName('');setCashierName('');setTimeout(()=>void load(),0)}
  return <section className="creator-section"><h2>Confirmed Sales</h2><div className="filter-bar"><input aria-label="Creator Name" placeholder="Creator Name" value={creatorName} onChange={e=>setCreatorName(e.target.value)}/><input aria-label="Cashier" placeholder="Cashier" value={cashierName} onChange={e=>setCashierName(e.target.value)}/><button onClick={load}>Search</button><button className="quiet" onClick={clear}>Clear</button></div>{error&&<p className="friendly-error">{error}</p>}{report&&<><div className="confirmed-sales-summary"><article><span>Confirmed Sales</span><strong>{report.confirmedSales}</strong></article><article><span>Total Sales</span><strong>{money(report.totalSalesAmount)}</strong></article><article><span>Commission Charged</span><strong>{money(report.totalCommissionAmount)}</strong></article></div><div className="confirmed-sale-list business-confirmed-sale-list">{report.sales.map(x=>{const d=new Date(x.confirmedAtUtc);return <article className="confirmed-sale-card" key={x.transactionId}><div className="confirmed-sale-heading"><strong>{money(x.saleAmount)}</strong><span className="status-badge"><span aria-hidden="true">●</span> {x.status}</span></div><time dateTime={x.confirmedAtUtc}>{d.toLocaleDateString()} · {d.toLocaleTimeString([], {hour:'numeric',minute:'2-digit'})}</time><dl><div><dt>Creator</dt><dd>{x.creatorName} · {x.creatorId}</dd></div><div><dt>Cashier</dt><dd>{x.cashierName}</dd></div><div><dt>Commission</dt><dd>{money(x.commissionAmount)}</dd></div><div><dt>Reference</dt><dd>{x.publicTransactionId}</dd></div>{x.locationName&&<div><dt>Location</dt><dd>{x.locationName}</dd></div>}</dl></article>})}{report.sales.length===0&&<p className="compact-empty">No confirmed sales match these filters.</p>}</div></>}</section>
}
