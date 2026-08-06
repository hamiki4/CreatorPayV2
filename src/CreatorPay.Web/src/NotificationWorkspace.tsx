import {useEffect,useState} from 'react'

const apiBase=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'')
const token=()=>localStorage.getItem('creatorpay_access_token')??''
const invalidResponse='The notification service returned an invalid response. Please try again.'

async function notificationApi<T>(path:string,init?:RequestInit):Promise<T>{
  const response=await fetch(`${apiBase}${path}`,{...init,headers:{'Content-Type':'application/json',Authorization:`Bearer ${token()}`,...init?.headers}})
  if(response.status===204){if(!response.ok)throw new Error(`Notification request failed (${response.status}).`);return undefined as T}
  const contentType=response.headers.get('content-type')?.toLowerCase()??''
  if(!contentType.includes('json'))throw new Error(invalidResponse)
  let body:unknown
  try{body=await response.json()}catch{throw new Error(invalidResponse)}
  if(!response.ok){const detail=typeof body==='object'&&body!==null&&'detail'in body&&typeof body.detail==='string'?body.detail:null;throw new Error(detail??`Notification request failed (${response.status}).`)}
  return body as T
}

type Notice={notificationId:string;type:string;title:string;body:string;priority:string;status:string;createdAtUtc:string;readAtUtc?:string}

export function NotificationCenter(){
  const[items,setItems]=useState<Notice[]|null>(null),[count,setCount]=useState(0),[error,setError]=useState('')
  const load=()=>{setError('');notificationApi<{items:Notice[]}>('/api/v1/notifications?page=1&pageSize=50').then(x=>setItems(x.items)).catch(e=>setError(e.message));notificationApi<{count:number}>('/api/v1/notifications/unread-count').then(x=>setCount(x.count)).catch(e=>setError(e.message))}
  useEffect(load,[])
  async function action(path:string){try{await notificationApi(path,{method:'POST'});load()}catch(e){setError((e as Error).message)}}
  return <section><div className="title"><h2>Notifications <span className="badge">{count} unread</span></h2><button className="quiet" onClick={()=>void action('/api/v1/notifications/read-all')}>Mark all read</button></div>{error&&<p className="error" role="alert">{error}</p>}{!items?<p>Loading…</p>:items.length===0?<p className="empty">No notifications yet.</p>:<div className="cards">{items.map(x=><article key={x.notificationId} className={x.readAtUtc?'':'unread'}><strong>{x.title}</strong><p>{x.body}</p><small>{new Date(x.createdAtUtc).toLocaleString()}</small>{!x.readAtUtc&&<button onClick={()=>void action(`/api/v1/notifications/${x.notificationId}/read`)}>Mark read</button>}</article>)}</div>}</section>
}

type Outbox={id:string;status:string;attemptCount:number;availableAtUtc:string;lastError?:string}
type Dead={id:string;outboxMessageId:string;status:string;reason:string;deadLetteredAtUtc:string}
type Template={id:string;notificationType:string;channel:string;languageCode:string;versionNumber:number;isActive:boolean}

export function NotificationOperations(){
  const[outbox,setOutbox]=useState<Outbox[]>([]),[dead,setDead]=useState<Dead[]>([]),[templates,setTemplates]=useState<Template[]>([]),[error,setError]=useState('')
  const load=()=>Promise.all([notificationApi<Outbox[]>('/api/v1/admin/notification-outbox').then(setOutbox),notificationApi<Dead[]>('/api/v1/admin/notification-dead-letters').then(setDead),notificationApi<Template[]>('/api/v1/admin/notification-templates').then(setTemplates)]).catch(e=>setError(e.message))
  useEffect(()=>{void load()},[])
  async function action(path:string){try{await notificationApi(path,{method:'POST'});await load()}catch(e){setError((e as Error).message)}}
  return <section><h2>Notification operations</h2>{error&&<p className="error" role="alert">{error}</p>}<div className="panel"><h3>Outbox</h3>{outbox.length===0?<p className="empty">Outbox is empty.</p>:outbox.map(x=><div className="assignment" key={x.id}><span>{x.status} · attempt {x.attemptCount}</span><small>{x.lastError}</small><button onClick={()=>void action(`/api/v1/admin/notification-outbox/${x.id}/retry`)}>Retry</button><button className="danger" onClick={()=>void action(`/api/v1/admin/notification-outbox/${x.id}/cancel`)}>Cancel</button></div>)}</div><div className="panel"><h3>Dead letters</h3>{dead.length===0?<p className="empty">No dead letters.</p>:dead.map(x=><div className="assignment" key={x.id}><span>{x.status} · {x.reason}</span><button onClick={()=>void action(`/api/v1/admin/notification-dead-letters/${x.id}/retry`)}>Retry</button><button onClick={()=>void action(`/api/v1/admin/notification-dead-letters/${x.id}/resolve`)}>Resolve</button></div>)}</div><div className="panel"><h3>Templates</h3>{templates.length===0?<p className="empty">No templates configured; safe notification content is used.</p>:templates.map(x=><div className="assignment" key={x.id}><span>{x.notificationType} · {x.channel} · {x.languageCode} · v{x.versionNumber}</span><span>{x.isActive?'Active':'Inactive'}</span></div>)}</div></section>
}
