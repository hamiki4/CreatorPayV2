import {useEffect,useState} from 'react'
import {api} from './apiClient'

const money=(value:number)=>new Intl.NumberFormat('en-ET',{minimumFractionDigits:2,maximumFractionDigits:2}).format(value)
const date=(value:string)=>new Intl.DateTimeFormat('en-GB',{day:'2-digit',month:'2-digit',year:'numeric'}).format(new Date(value))
const shortReference=(value:string)=>`#${value.replace(/[^a-z0-9]/gi,'').slice(-6).toUpperCase()}`

type Sale={transactionId:string;publicTransactionId:string;confirmedAtUtc:string;saleAmount:number;commissionAmount:number;creatorName:string;creatorId:string;cashierName:string;status:string;locationName?:string}
type Report={confirmedSales:number;totalSalesAmount:number;totalCommissionAmount:number;sales:Sale[]}

export function ConfirmedSalesWorkspace(){
  const[report,setReport]=useState<Report>()
  const[creatorName,setCreatorName]=useState('')
  const[cashierName,setCashierName]=useState('')
  const[error,setError]=useState('')
  const load=()=>{
    const q=new URLSearchParams()
    if(creatorName.trim())q.set('creatorName',creatorName.trim())
    if(cashierName.trim())q.set('cashierName',cashierName.trim())
    api<Report>(`/api/v1/merchant/confirmed-sales?${q}`).then(x=>{setReport(x);setError('')}).catch(e=>setError(e.message))
  }
  useEffect(()=>{void load()},[])
  function clear(){setCreatorName('');setCashierName('');setTimeout(()=>void load(),0)}
  return <section className="creator-section business-confirmed-sales">
    <h2>Confirmed Sales</h2>
    {report&&<div className="business-sales-summary">
      <article><span>Confirmed Sales</span><strong>{report.confirmedSales}</strong></article>
      <article><span>Total Sales</span><strong>{money(report.totalSalesAmount)}</strong></article>
      <article><span>Platform fees</span><strong>{money(report.totalCommissionAmount)}</strong></article>
    </div>}
    <div className="filter-bar"><input aria-label="Creator Name" placeholder="Creator Name" value={creatorName} onChange={e=>setCreatorName(e.target.value)}/><input aria-label="Cashier" placeholder="Cashier" value={cashierName} onChange={e=>setCashierName(e.target.value)}/><button onClick={load}>Search</button><button className="quiet" onClick={clear}>Clear</button></div>
    {error&&<p className="friendly-error">{error}</p>}
    {report&&<>{report.sales.length===0?<p className="compact-empty">No confirmed sales match these filters.</p>:<div className="business-sales-grid" role="table" aria-label="Confirmed sales transactions">
      <div className="business-sales-row headings" role="row"><span role="columnheader">Date</span><span role="columnheader">Sale</span><span role="columnheader">Creator</span><span role="columnheader">Cashier</span><span role="columnheader">Platform fee</span><span role="columnheader">Status</span><span role="columnheader">Reference</span></div>
      {report.sales.map(x=><div className="business-sales-row" role="row" key={x.transactionId}>
        <span role="cell" data-label="Date">{date(x.confirmedAtUtc)}</span>
        <strong role="cell" data-label="Sale">{money(x.saleAmount)}</strong>
        <span role="cell" data-label="Creator"><b>{x.creatorName}</b></span>
        <span role="cell" data-label="Cashier">{x.cashierName}</span>
        <span role="cell" data-label="Platform fee">{money(x.commissionAmount)}</span>
        <span role="cell" data-label="Status"><span className="status-badge">{x.status}</span></span>
        <button type="button" className="business-short-reference" role="cell" data-label="Reference" title={x.publicTransactionId} aria-label={`Copy full reference ${x.publicTransactionId}`} onClick={()=>void navigator.clipboard?.writeText(x.publicTransactionId)}>{shortReference(x.publicTransactionId)}</button>
      </div>)}
    </div>}</>}
  </section>
}
