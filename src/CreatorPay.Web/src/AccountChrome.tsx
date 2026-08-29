import {type CSSProperties,ReactNode,useCallback,useEffect,useRef,useState} from 'react'
import {api} from './apiClient'
import {requestActionableRefresh} from './actionableRefresh'
import {enablePushNotifications} from './pushNotifications'
import {ProfileAvatar} from './profileMedia'

type Notice={notificationId:string;type:string;title:string;body:string;createdAtUtc:string;readAtUtc?:string;data?:Record<string,string>}
type NoticePage={items:Notice[];total:number}

const notificationTarget=(notice:Notice)=>notice.data?.TargetPath??notice.data?.targetPath
const iconFor=(type:string)=>type.includes('Payout')?'₿':type.includes('Cashback')?'✓':type.includes('Request')||type.includes('Invitation')?'♙':type.includes('Earning')||type.includes('Purchase')?'↗':'•'
const normalizeStatus=(value?:string)=>value?.trim().toLowerCase()==='active'

export function AccountStatusBadge({status}:{status?:string}){
  const active=normalizeStatus(status)
  return <span className={`account-status account-status--${active?'active':'inactive'}`}><span className="account-status-dot" aria-hidden="true">●</span><span>{active?'Active':'Inactive'}</span></span>
}

export function AccountChrome({role,name,status,photoUrl,identityMedia,onProfile,onHelp,onSignOut,onManagement,onNavigate,children}:{role:'Customer'|'Creator'|'Business';name?:string;status?:string;photoUrl?:string;identityMedia?:ReactNode;onProfile:()=>void;onHelp:()=>void;onSignOut:()=>void;onManagement?:()=>void;onNavigate?:(target:string,notice:Notice)=>void;children:ReactNode}){
  const[settingsOpen,setSettingsOpen]=useState(false),[notificationsOpen,setNotificationsOpen]=useState(false),[items,setItems]=useState<Notice[]>([]),[unread,setUnread]=useState(0),[notificationError,setNotificationError]=useState('')
  const mounted=useRef(true)
  const settingsRef=useRef<HTMLDivElement>(null)
  const settingsButtonRef=useRef<HTMLButtonElement>(null)
  const refresh=useCallback(async()=>{try{const[list,count]=await Promise.all([api<NoticePage>('/api/v1/notifications?page=1&pageSize=30'),api<{count:number}>('/api/v1/notifications/unread-count')]);if(mounted.current){setItems(list.items);setUnread(count.count);setNotificationError('');requestActionableRefresh()}}catch(error){console.error(error);if(mounted.current)setNotificationError('Notifications are temporarily unavailable.')}},[])
  useEffect(()=>{mounted.current=true;void refresh();const timer=window.setInterval(()=>{if(document.visibilityState==='visible'&&navigator.onLine)void refresh()},12_000);const active=()=>{if(document.visibilityState==='visible'&&navigator.onLine)void refresh()};addEventListener('online',active);addEventListener('weymela:app-resume',active);document.addEventListener('visibilitychange',active);return()=>{mounted.current=false;clearInterval(timer);removeEventListener('online',active);removeEventListener('weymela:app-resume',active);document.removeEventListener('visibilitychange',active)}},[refresh])
  useEffect(()=>{
    const onPointerDown=(event:PointerEvent)=>{
      if(!settingsOpen)return
      const target=event.target
      if(!(target instanceof Node))return
      if(settingsRef.current?.contains(target))return
      if(settingsButtonRef.current?.contains(target))return
      setSettingsOpen(false)
    }
    const onEscape=(event:KeyboardEvent)=>{if(event.key==='Escape')setSettingsOpen(false)}
    document.addEventListener('pointerdown',onPointerDown,true)
    document.addEventListener('keydown',onEscape)
    return()=>{
      document.removeEventListener('pointerdown',onPointerDown,true)
      document.removeEventListener('keydown',onEscape)
    }
  },[settingsOpen])
  async function markRead(notice:Notice){if(!notice.readAtUtc){setItems(current=>current.map(x=>x.notificationId===notice.notificationId?{...x,readAtUtc:new Date().toISOString()}:x));setUnread(current=>Math.max(0,current-1));try{await api(`/api/v1/notifications/${notice.notificationId}/read`,{method:'POST'})}catch{void refresh()}}requestActionableRefresh();const target=notificationTarget(notice);if(target&&onNavigate){onNavigate(target,notice);setNotificationsOpen(false)}}
  async function markAll(){const now=new Date().toISOString();setItems(current=>current.map(x=>x.readAtUtc?x:{...x,readAtUtc:now}));setUnread(0);requestActionableRefresh();try{await api('/api/v1/notifications/read-all',{method:'POST'})}catch{void refresh()}}
  const roleStyles: Record<typeof role, CSSProperties> = {
    Customer: {'--role-accent': '#1f8a3b', '--role-accent-soft': '#e6f5ea'} as CSSProperties,
    Creator: {'--role-accent': '#7c4dff', '--role-accent-soft': '#f1eaff'} as CSSProperties,
    Business: {'--role-accent': '#2563eb', '--role-accent-soft': '#e8f0ff'} as CSSProperties,
  }
  return (
    <div className={`account-shell role-${role.toLowerCase()}`} style={roleStyles[role]}>
      <header className="account-header">
        <div className="account-header-copy">
          <p className="eyebrow">WEYMELA</p>
          <h1>{role}</h1>
          {role === 'Creator' ? (
            <div className="account-identity account-identity--creator">
              <strong className="account-identity-name">{name ?? role}</strong>
              <span className="account-identity-media">
                {identityMedia ?? <ProfileAvatar name={name ?? role} photoUrl={photoUrl} className="account-identity-avatar" />}
              </span>
              {status && <AccountStatusBadge status={status} />}
            </div>
          ) : (
            <>
              <p className="account-identity">
                <span>{name ?? role}</span>
                {status && <AccountStatusBadge status={status} />}
              </p>
            </>
          )}
        </div>
        <div className="account-actions">
          <button
            type="button"
            className="icon-button"
            aria-label="Notifications"
            aria-expanded={notificationsOpen}
            onClick={() => {
              setNotificationsOpen(!notificationsOpen)
              setSettingsOpen(false)
              requestActionableRefresh()
              if (!notificationsOpen) void refresh()
            }}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.9} strokeLinecap="round" strokeLinejoin="round">
              <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4" />
            </svg>
            {unread > 0 && <span className="notification-count">{unread > 99 ? '99+' : unread}</span>}
          </button>
          <button
            ref={settingsButtonRef}
            type="button"
            className="icon-button"
            aria-label="Settings"
            aria-expanded={settingsOpen}
            onClick={() => {
              setSettingsOpen(!settingsOpen)
              setNotificationsOpen(false)
            }}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.9} strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z" />
              <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2h-4V21a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H2.8v-4H3a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1a1.7 1.7 0 0 0 1.9.3A1.7 1.7 0 0 0 10 3V2.8h4V3a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.2v4H21a1.7 1.7 0 0 0-1.6 1Z" />
            </svg>
          </button>
          {settingsOpen && (
            <div ref={settingsRef} className="settings-menu" role="menu">
              {onManagement && <button role="menuitem" onClick={() => { setSettingsOpen(false); onManagement() }}>Cashier Management</button>}
              <button role="menuitem" onClick={() => { setSettingsOpen(false); void enablePushNotifications().catch(() => setNotificationError('Notifications could not be enabled.')) }}>Enable device notifications</button>
              <button role="menuitem" onClick={() => { setSettingsOpen(false); onHelp() }}>Help</button>
              <button role="menuitem" className="signout-action" onClick={() => { setSettingsOpen(false); onSignOut() }}>Sign out</button>
            </div>
          )}
        </div>
      </header>
    {children}
    {notificationsOpen&&<><button className="drawer-scrim" aria-label="Close notifications" onClick={()=>setNotificationsOpen(false)}/><aside className="notification-drawer" aria-label="Notifications"><div className="notification-drawer-header"><h2>Notifications</h2><button className="icon-button" aria-label="Close notifications" onClick={()=>setNotificationsOpen(false)}>×</button></div>{notificationError&&<p className="friendly-error" role="alert">{notificationError}</p>}{items.length===0&&!notificationError?<p className="compact-empty">No notifications yet.</p>:<div className="notification-list">{items.map(notice=><button type="button" className={`notification-item${notice.readAtUtc?'':' unread'}`} key={notice.notificationId} onClick={()=>void markRead(notice)}><span className="notification-type-icon" aria-hidden="true">{iconFor(notice.type)}</span><span><strong>{notice.title}</strong><small>{notice.body}</small><time dateTime={notice.createdAtUtc}>{new Intl.DateTimeFormat('en-GB',{dateStyle:'medium',timeStyle:'short'}).format(new Date(notice.createdAtUtc))}</time></span>{!notice.readAtUtc&&<i aria-label="Unread"/>}</button>)}</div>}{unread>0&&<button className="mark-all-read" onClick={()=>void markAll()}>Mark all as read</button>}</aside></>}
    </div>
  )
}
