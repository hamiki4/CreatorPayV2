import {useEffect,useState} from 'react'
import {api} from './apiClient'

const money=(x:number)=>new Intl.NumberFormat('en-ET',{style:'currency',currency:'ETB'}).format(x)
type Sale={transactionId:string;confirmedAtUtc:string;saleAmount:number;commissionAmount:number;creatorName:string;creatorId:string;cashierName:string;status:string;locationName?:string}
type Report={totalSalesAmount:number;totalCommissionAmount:number;sales:Sale[]}

export function ConfirmedSalesWorkspace(){
  const [report,setReport]=useState<Report>(); const [creatorName,setCreatorName]=useState(''); const [cashierName,setCashierName]=useState(''); const [error,setError]=useState('')
  const load=()=>{const q=new URLSearchParams(); if(creatorName.trim())q.set('creatorName',creatorName.trim()); if(cashierName.trim())q.set('cashierName',cashierName.trim()); api<Report>(`/api/v1/merchant/confirmed-sales?${q}`).then(x=>{setReport(x);setError('')}).catch(e=>setError(e.message))}
  useEffect(()=>{void load()},[])
  function clear(){setCreatorName('');setCashierName('');setTimeout(()=>void load(),0)}
  return <section className="creator-section"><h2>Confirmed Sales</h2><div className="filter-bar"><input aria-label="Creator Name" placeholder="Creator Name" value={creatorName} onChange={e=>setCreatorName(e.target.value)}/><input aria-label="Actor Name" placeholder="Actor Name" value={cashierName} onChange={e=>setCashierName(e.target.value)}/><button onClick={load}>Search</button><button className="quiet" onClick={clear}>Clear</button></div>{error&&<p className="friendly-error">{error}</p>}{report&&<div className="table-wrap"><table><thead><tr><th>Date</th><th>Time</th><th>Creator</th><th>Creator ID</th><th>Actor</th><th>Sale Amount</th><th>Commission</th><th>Status</th></tr></thead><tbody>{report.sales.map(x=>{const d=new Date(x.confirmedAtUtc);return <tr key={x.transactionId}><td>{d.toLocaleDateString()}</td><td>{d.toLocaleTimeString()}</td><td>{x.creatorName}</td><td>{x.creatorId}</td><td>{x.cashierName}</td><td>{money(x.saleAmount)}</td><td>{money(x.commissionAmount)}</td><td><span className="status-badge">{x.status}</span></td></tr>})}</tbody><tfoot><tr><th colSpan={5}>TOTAL</th><th>{money(report.totalSalesAmount)}</th><th>{money(report.totalCommissionAmount)}</th><th></th></tr></tfoot></table>{report.sales.length===0&&<p className="compact-empty">No confirmed sales match these filters.</p>}</div>}</section>
}
