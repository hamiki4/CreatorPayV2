import {FormEvent,ReactNode,useEffect,useState} from 'react'
import {ReportingWorkspace} from './ReportingWorkspace'
import {clearAuthState,handleUnauthorized} from './authSession'
import {rankMatches,useTypeahead} from './typeahead'
type Row=Record<string,unknown>;type Page={items:Row[];page:number;total:number;totalPages:number};type Ops=Record<string,ReactNode>
const base=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'');const token=()=>localStorage.getItem('creatorpay_access_token')??''
async function api<T>(path:string):Promise<T>{const r=await fetch(`${base}${path}`,{headers:{Authorization:`Bearer ${token()}`}});if(handleUnauthorized(r.status))throw Error('Your session has expired.');const b=await r.json().catch(()=>({}));if(!r.ok)throw Error(r.status===403?'Platform Admin permission is required.':b.detail??b.error??`Request failed (${r.status}).`);return b}
// The redundant admin 'Transactions' page is intentionally not exposed in navigation.
const routes=[['dashboard','Dashboard'],['reports','Reports'],['creators','Creators'],['merchants','Businesses'],['accounts','Accounts'],['commission','Commission'],['deposits','Deposits'],['wallets','Wallets'],['payouts','Payouts'],['fraud','Fraud'],['system','System']]
const endpoints:Record<string,string>={creators:'creators',merchants:'merchants',accounts:'accounts',wallets:'wallets',system:'system'}
function role(){try{const c=JSON.parse(atob(token().split('.')[1].replace(/-/g,'+').replace(/_/g,'/')));return c['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']??c.role}catch{return''}}
export function AdminPortal({operations}:{operations:Ops}){if(role()!=='PlatformAdmin')return <main className="auth"><section className="panel"><h1>{role()?'Permission denied':'Authentication required'}</h1><p>This dashboard requires the PlatformAdmin role.</p></section></main>;const part=location.pathname.split('/')[2]||'dashboard',page=routes.some(x=>x[0]===part)?part:'dashboard';return <div className="admin-shell"><a className="skip" href="#admin-content">Skip to content</a><aside className="admin-sidebar"><a className="admin-brand" href="/admin/dashboard">Weymela <small>Operations</small></a><nav aria-label="Admin navigation">{routes.map(([p,l])=><a key={p} aria-current={p===page?'page':undefined} href={`/admin/${p}`}>{l}</a>)}</nav></aside><div className="admin-main"><AdminHeader/><main id="admin-content" tabIndex={-1}>{page==='dashboard'?<Dashboard/>:page==='reports'?<ReportingWorkspace/>:page==='accounts'?<Accounts/>:page==='system'?<SystemStatus/>:page==='merchants'?<MerchantReview/>:page==='creators'?<CreatorReview/>:endpoints[page]?<Table title={label(page)} endpoint={endpoints[page]}/>:<section><Title text={label(page)}/>{operations[page]??<Empty/>}</section>}</main></div></div>}


function CreatorReview(){
  const [items,setItems]=useState<Row[]>([]);
  const [selected,setSelected]=useState<Row>();
  const [reason,setReason]=useState('');
  const [message,setMessage]=useState('');
  const load=()=>api<Row[]>('/api/v1/creators/pending')
    .then(setItems)
    .catch((e:Error)=>setMessage(e.message));

  useEffect(()=>{void load()},[]);

  async function open(id:unknown){
    try{
      setSelected(await api<Row>(`/api/v1/creators/${id}`));
    }catch(e){
      setMessage((e as Error).message);
    }
  }

  async function decide(action:'approve'|'request-correction'|'reject'){
    if(!selected)return;

    const creatorId=selected.creatorId;
    const needsReason=action!=='approve';

    if(needsReason && !reason.trim()){
      setMessage('A reason is required.');
      return;
    }

    const r=await fetch(`${base}/api/v1/creators/${action}`,{
      method:'POST',
      headers:{
        Authorization:`Bearer ${token()}`,
        'Content-Type':'application/json'
      },
      body:JSON.stringify({
        creatorId,
        reason:reason.trim() || null
      })
    });

    const body=await r.json().catch(()=>({}));

    setMessage(r.ok ? 'Creator decision saved and audited.' :
      body.detail ?? 'Creator decision failed.');

    if(r.ok){
      setSelected(undefined);
      setReason('');
      load();
    }
  }

  return <section>
    <Title text="Creator review"/>

    {message && <aside role="status">{message}</aside>}

    {selected ? <div className="panel">
      <button className="back" onClick={()=>setSelected(undefined)}>
        ← Pending creators
      </button>

      <h2>{String(selected.displayName ?? '')}</h2>

      <dl>
        {Object.entries(selected)
          .filter(([k])=>!['profileImage'].includes(k))
          .map(([k,v])=>
            <div key={k}>
              <dt>{label(k)}</dt>
              <dd>
                {k === 'socialProfiles' && Array.isArray(v)
                  ? v.map((social:any,index:number)=>
                      <div key={index}>
                        <strong>{social.platform}</strong>{' '}
                        {social.profileUrl
                          ? <a href={social.profileUrl}
                               target="_blank"
                               rel="noopener noreferrer">
                              Open social profile
                            </a>
                          : null}
                        <br/>
                        Followers: {String(social.followerCount ?? 0)}
                        <br/>
                        Verification: {String(social.verificationStatus ?? '')}
                      </div>)
                  : String(v ?? '—')}
              </dd>
            </div>
          )}
      </dl>

      <label>
        Decision reason
        <textarea
          value={reason}
          onChange={e=>setReason(e.target.value)}
        />
      </label>

      <div className="actions">
        <button onClick={()=>decide('approve')}>Approve</button>
        <button className="quiet"
                onClick={()=>decide('request-correction')}>
          Request correction
        </button>
        <button className="danger"
                onClick={()=>decide('reject')}>
          Reject
        </button>
      </div>
    </div>
    :
    items.length===0 ? <Empty/> :
    <div className="cards">
      {items.map(x=>
        <article key={String(x.creatorId)}>
          <span>Pending Approval</span>
          <strong>{String(x.displayName ?? '')}</strong>
          <p>{String(x.email ?? '')}</p>
          <p>{new Date(String(x.registeredAtUtc)).toLocaleString()}</p>
          <button onClick={()=>open(x.creatorId)}>Review</button>
        </article>
      )}
    </div>}
  </section>
}

function MerchantReview(){const[items,setItems]=useState<Row[]>(),[selected,setSelected]=useState<Row>(),[cashiers,setCashiers]=useState<Row[]>([]),[reason,setReason]=useState(''),[message,setMessage]=useState('');const load=()=>api<Row[]>('/api/v1/admin/merchants/pending').then(setItems).catch((e:Error)=>setMessage(e.message));useEffect(()=>{void load()},[]);async function open(id:unknown){try{const[merchant,cashierPage]=await Promise.all([api<Row>(`/api/v1/admin/merchants/${id}`),api<Page>(`/api/v1/admin/merchants/${id}/cashiers?page=1&pageSize=100`)]);setSelected(merchant);setCashiers(cashierPage.items)}catch(e){setMessage((e as Error).message)}}async function decide(action:string){if(!selected)return;const needsReason=action!=='approve'&&action!=='reactivate';if(needsReason&&!reason.trim()){setMessage('A reason is required.');return}const r=await fetch(`${base}/api/v1/admin/merchants/${action}`,{method:'POST',headers:{Authorization:`Bearer ${token()}`,'Content-Type':'application/json'},body:JSON.stringify({merchantId:selected.merchantId,reason})});const b=await r.json().catch(()=>({}));setMessage(r.ok?'Decision saved and audited.':b.detail??'Decision failed.');if(r.ok){setSelected(undefined);setReason('');load()}}return <section><Title text="Business review"/>{message&&<aside role="status">{message}</aside>}{selected?<div className="panel"><button className="back" onClick={()=>setSelected(undefined)}>← Pending registrations</button><h2>{String(selected.tradingName)}</h2><dl>{Object.entries(selected).filter(([k])=>!['documents','logo'].includes(k)).map(([k,v])=><div key={k}><dt>{label(k)}</dt><dd>{String(v??'—')}</dd></div>)}</dl><h3>Cashiers</h3>{cashiers.length===0?<p>No Cashiers yet.</p>:<div className="table-wrap"><table><thead><tr><th>Cashier</th><th>Email</th><th>Phone</th><th>Location</th><th>Status</th></tr></thead><tbody>{cashiers.map(x=><tr key={String(x.id)}><td data-label="Cashier">{cell('cashierName',x.cashierName)}</td><td data-label="Email">{cell('email',x.email)}</td><td data-label="Phone">{cell('phone',x.phone)}</td><td data-label="Location">{cell('assignedLocation',x.assignedLocation)}</td><td data-label="Status">{cell('status',x.status)}</td></tr>)}</tbody></table></div>}<label>Decision reason<textarea value={reason} onChange={e=>setReason(e.target.value)}/></label><div className="actions"><button onClick={()=>decide('approve')}>Approve</button><button className="quiet" onClick={()=>decide('request-correction')}>Request correction</button><button className="danger" onClick={()=>decide('reject')}>Reject</button></div></div>:!items?<Loading/>:items.length===0?<Empty/>:<div className="cards">{items.map(x=><article key={String(x.merchantId)} onClick={()=>open(x.merchantId)}><span>PendingReview</span><strong>{String(x.tradingName)}</strong><p>{String(x.email)}<br/>{new Date(String(x.registeredAtUtc)).toLocaleString()}</p><button>Review</button></article>)}</div>}</section>}
function AdminHeader(){const[q,setQ]=useState('');function go(e:FormEvent){e.preventDefault();location.href=`/admin/reports?q=${encodeURIComponent(q)}`}function logout(){clearAuthState();location.assign('/')}return <header className="admin-header"><form role="search" onSubmit={go}><input aria-label="Support search" minLength={3} value={q} onChange={e=>setQ(e.target.value)} placeholder="Public ID or correlation ID"/><button>Search</button></form><span>Platform Admin</span><button className="quiet" onClick={logout}>Sign out</button></header>}
function Title({text,children}:{text:string;children?:ReactNode}){return <div className="admin-title"><div><p className="eyebrow">Platform operations</p><h1>{text}</h1></div>{children}</div>}
function Dashboard(){const[data,setData]=useState<Row>(),[error,setError]=useState(''),[days,setDays]=useState('0');useEffect(()=>{const to=new Date(),from=new Date();days==='0'?from.setHours(0,0,0,0):from.setDate(to.getDate()-Number(days));api<Row>(`/api/v1/admin/dashboard/summary?from=${from.toISOString()}&to=${to.toISOString()}`).then(setData).catch(e=>setError(e.message))},[days]);const visible=['pendingCreatorApprovals','pendingMerchantApprovals','pendingDeposits','activeCreators','activeBusinesses','pendingPayouts','openFraudAlerts','failedCheckouts','systemHealth'];return <section><Title text="Dashboard"><label>Time period <select value={days} onChange={e=>setDays(e.target.value)}><option value="0">Today</option><option value="7">This week</option><option value="30">This month</option></select></label></Title>{error?<ErrorBox text={error}/>:!data?<Loading/>:<div className="summary-grid">{visible.filter(k=>data[k]!==undefined).map(k=><article className="summary-card" key={k}><span>{label(k)}</span><strong>{String(data[k])}</strong></article>)}</div>}</section>}
function Table({title,endpoint}:{title:string;endpoint:string}){const[data,setData]=useState<Page>(),[error,setError]=useState(''),[page,setPage]=useState(1),[input,setInput]=useState(''),[query,setQuery]=useState('');useTypeahead(input,async term=>term,setQuery);useEffect(()=>{api<Page>(`/api/v1/admin/${endpoint}?page=${page}&pageSize=25${query?`&q=${encodeURIComponent(query)}`:''}`).then(value=>setData({...value,items:rankMatches(value.items,query,row=>Object.values(row).map(String))})).catch(e=>setError(e.message))},[endpoint,page,query]);function filter(e:FormEvent){e.preventDefault();setPage(1);setQuery(input)}const cols=data?.items[0]?Object.keys(data.items[0]).filter(x=>x!=='id').slice(0,7):[];return <section><Title text={title}><form className="filter" onSubmit={filter}><input type="search" aria-label="Filter results" value={input} onChange={e=>setInput(e.target.value)} placeholder="Type 2+ characters to filter"/><button>Apply</button></form></Title>{error?<ErrorBox text={error}/>:!data?<Loading/>:!data.items.length?<Empty/>:<><div className="table-wrap"><table><thead><tr>{cols.map(c=><th scope="col" key={c}>{label(c)}</th>)}</tr></thead><tbody>{data.items.map((row,i)=><tr key={String(row.id??i)}>{cols.map(c=><td key={c} data-label={label(c)}>{cell(c,row[c])}</td>)}</tr>)}</tbody></table></div><nav className="pagination" aria-label="Pagination"><button disabled={page===1} onClick={()=>setPage(page-1)}>Previous</button><span>Page {page} of {Math.max(1,data.totalPages)} · {data.total} records</span><button disabled={page>=data.totalPages} onClick={()=>setPage(page+1)}>Next</button></nav></>}</section>}
function Accounts(){const[data,setData]=useState<Page>(),[message,setMessage]=useState(''),[filters,setFilters]=useState({q:'',role:'',status:'',business:''});const load=()=>{const p=new URLSearchParams({page:'1',pageSize:'100'});Object.entries(filters).forEach(([k,v])=>{if(v)p.set(k,v)});return api<Page>(`/api/v1/admin/accounts?${p}`).then(setData).catch(e=>setMessage(e.message))};useEffect(()=>{void load()},[]);async function action(id:unknown,name:string){if(!confirm(`${label(name)} this account?`))return;const reason=prompt('Reason for this account change')?.trim();if(!reason)return;const r=await fetch(`${base}/api/v1/admin/accounts/${id}/${name}`,{method:'POST',headers:{Authorization:`Bearer ${token()}`,'Content-Type':'application/json'},body:JSON.stringify({reason})});if(!r.ok){setMessage('Unable to change this account.');return}setMessage('Account updated and audited.');load()}const cols=['cashierName','email','phone','businessName','publicBusinessId','assignedLocation','role','status','isEmailVerified','isPhoneVerified','isLocked','lastLoginAtUtc','walletBalance','fundingStatus'];return <section><Title text="Accounts"/><PasswordResetRequests/><form className="panel form account-filters" onSubmit={e=>{e.preventDefault();void load()}}><label>General Search<input value={filters.q} onChange={e=>setFilters({...filters,q:e.target.value})} placeholder="Name, email, phone, Business or public ID"/></label><label>Role<select value={filters.role} onChange={e=>setFilters({...filters,role:e.target.value})}><option value="">All roles</option><option value="Customer">Shopper / Customer</option><option value="Creator">Creator</option><option value="MerchantAdmin">Business Owner</option><option value="Cashier">Cashier</option><option value="PlatformAdmin">PlatformAdmin</option></select></label><label>Status<select value={filters.status} onChange={e=>setFilters({...filters,status:e.target.value})}><option value="">All statuses</option>{['Active','PendingVerification','Suspended','Closed','Locked'].map(x=><option key={x} value={x}>{x==='Closed'?'Deactivated':x}</option>)}</select></label><label>Business<input value={filters.business} onChange={e=>setFilters({...filters,business:e.target.value})} placeholder="Business name or public ID"/></label><div className="actions"><button>Search</button><button type="button" className="quiet" onClick={()=>{setFilters({q:'',role:'',status:'',business:''});setTimeout(()=>void api<Page>('/api/v1/admin/accounts?page=1&pageSize=100').then(setData),0)}}>Clear Filters</button></div></form>{message&&<aside role="status">{message}</aside>}{!data?<Loading/>:<div className="table-wrap"><table><thead><tr>{cols.map(x=><th key={x}>{label(x)}</th>)}<th>Action</th></tr></thead><tbody>{data.items.map(x=><tr key={String(x.id)}>{cols.map(c=><td key={c} data-label={label(c)}>{cell(c,x[c])}</td>)}<td data-label="Action"><div className="actions">{x.isLocked?<button onClick={()=>action(x.id,'unlock')}>Unlock</button>:<button className="quiet" onClick={()=>action(x.id,'lock')}>Lock</button>}{x.status==='Active'?<>{x.role==='Cashier'?<button className="danger" onClick={()=>action(x.id,'deactivate')}>Disable Cashier</button>:<><button className="quiet" onClick={()=>action(x.id,'suspend')}>Suspend</button><button className="danger" onClick={()=>action(x.id,'deactivate')}>Deactivate</button></>}</>:['Suspended','Closed'].includes(String(x.status))?<button onClick={()=>action(x.id,'reactivate')}>{x.role==='Cashier'?'Reactivate Cashier':'Reactivate'}</button>:null}</div></td></tr>)}</tbody></table></div>}</section>}

function PasswordResetRequests(){const[items,setItems]=useState<Row[]>([]),[message,setMessage]=useState('');const load=()=>api<Row[]>('/api/v1/admin/password-reset-requests').then(setItems).catch((e:Error)=>setMessage(e.message));useEffect(()=>{void load()},[]);async function review(id:unknown,action:'approve'|'reject'){if(!confirm(`${action==='approve'?'Authorize':'Reject'} this password reset request?`))return;const r=await fetch(`${base}/api/v1/admin/password-reset-requests/${id}/${action}`,{method:'POST',headers:{Authorization:`Bearer ${token()}`}});if(!r.ok){setMessage('Unable to review this request.');return}setMessage(action==='approve'?'Reset authorized. The user can now create a new password.':'Reset request rejected.');void load()}return <section className="admin-reset-requests"><h2>Password Reset Requests</h2>{message&&<aside role="status">{message}</aside>}<div className="table-wrap"><table><thead><tr>{['Name','Phone','Role','Requested At','Status','Action'].map(x=><th key={x}>{x}</th>)}</tr></thead><tbody>{items.length===0?<tr><td colSpan={6}>No password reset requests.</td></tr>:items.map(x=><tr key={String(x.id)}><td>{cell('name',x.name)}</td><td>{cell('phone',x.phone)}</td><td>{cell('role',x.role)}</td><td>{cell('requestedAtUtc',x.requestedAtUtc)}</td><td>{cell('status',x.status)}</td><td><div className="actions">{x.canApprove===true&&<><button onClick={()=>review(x.id,'approve')}>Authorize Reset</button><button className="danger" onClick={()=>review(x.id,'reject')}>Reject</button></>}</div></td></tr>)}</tbody></table></div></section>}
function SystemStatus(){const[data,setData]=useState<Row>(),[error,setError]=useState('');useEffect(()=>{api<Row>('/api/v1/admin/system').then(setData).catch(e=>setError(e.message))},[]);return <section><Title text="System"/>{error?<ErrorBox text={error}/>:!data?<Loading/>:<div className="summary-grid"><article className="summary-card"><span>API Health</span><strong>{String(data.health??'Unknown')}</strong></article><article className="summary-card"><span>Database Health</span><strong>{String(data.database??'Unknown')}</strong></article><article className="summary-card"><span>Worker Health</span><strong>{String((data.worker as Row|undefined)?.status??'Unknown')}</strong></article><article className="summary-card"><span>Web Health</span><strong>Available</strong></article><article className="summary-card"><span>Deployment Version</span><strong>{String(data.version??'Unknown')}</strong></article></div>}</section>}
function adminDateTime(value:string){return new Intl.DateTimeFormat('en-GB',{day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit',hour12:true}).format(new Date(value)).replace(',','')}
function cell(k:string,v:unknown){if(v==null)return'—';if(k.toLowerCase().includes('status'))return <span className="status">{String(v)}</span>;if(k.toLowerCase().includes('correlation'))return <code>{String(v)}</code>;if(typeof v==='boolean')return v?'Yes':'No';if(typeof v==='string'&&(k.endsWith('Utc')||k==='lastDeposit'))return adminDateTime(v);return String(v)}
function Loading(){return <p aria-live="polite">Loading…</p>}function Empty(){return <div className="empty-state"><h2>Nothing here yet</h2><p>No records match the current filters.</p></div>}function ErrorBox({text}:{text:string}){return <div className="error-panel" role="alert"><h2>Unable to load</h2><p>{text}</p></div>}function label(x:string){return x.replace(/-/g,' ').replace(/([a-z])([A-Z])/g,'$1 $2').replace(/^./,c=>c.toUpperCase())}
