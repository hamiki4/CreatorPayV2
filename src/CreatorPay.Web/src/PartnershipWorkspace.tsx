import {FormEvent, useEffect, useState} from 'react'
import {api, statusLabel} from './apiClient'
import {formatDate} from './displayFormat'

type Partnership = {id: string; merchantName: string; creatorName: string; status: string; requestedAtUtc: string}
type Found = {id: string; publicMerchantId?: string; tradingName?: string; publicCreatorId?: string; displayName?: string; email?: string; city?: string}

export function PartnershipWorkspace({merchant}: {merchant: boolean}) {
  const [search, setSearch] = useState(false)
  const [q, setQ] = useState('')
  const [status, setStatus] = useState(merchant ? 'All' : 'Approved')
  const [items, setItems] = useState<Partnership[] | null>(null)
  const [found, setFound] = useState<Found[]>([])
  const [message, setMessage] = useState('')
  const load = () => api<Partnership[]>(`/api/v1/${merchant ? 'merchant' : 'creator'}/partnerships${status === 'All' ? '' : `?status=${status}`}`).then(setItems).catch((e) => setMessage(e.message))

  useEffect(() => { void load() }, [merchant, status])

  async function find(e: FormEvent) {
    e.preventDefault()
    try { setFound(await api(`/api/v1/${merchant ? 'merchant/creators' : 'creator/merchants'}/search?q=${encodeURIComponent(q)}`)) }
    catch (error) { setMessage((error as Error).message) }
  }

  async function add(item: Found) {
    if (merchant && !confirm(`Approve ${item.displayName}?`)) return
    try {
      await api(merchant ? '/api/v1/merchant/partnerships' : '/api/v1/creator/partnerships/requests', {
        method: 'POST',
        body: JSON.stringify(merchant ? {creatorId: item.id, confirmApproval: true, locationIds: []} : {merchantId: item.id, introductoryMessage: prompt('Introduction (optional):') || null}),
      })
      setMessage(merchant ? 'Creator approved.' : 'Request sent.')
      load()
    } catch (error) { setMessage((error as Error).message) }
  }

  async function action(item: Partnership, name: string) {
    if (!confirm(`Confirm ${name.replace('-', ' ')}?`)) return
    try {
      await api(`/api/v1/${merchant ? 'merchant' : 'creator'}/partnerships/${item.id}/${name}`, {method: 'POST', body: JSON.stringify({reason: prompt('Reason (optional):') || null})})
      load()
    } catch (error) { setMessage((error as Error).message) }
  }

  return <>
    <nav className="subnav"><button className={!search ? 'active' : ''} onClick={() => setSearch(false)}>Partnerships</button><button className={search ? 'active' : ''} onClick={() => setSearch(true)}>Find {merchant ? 'Creators' : 'Businesses'}</button></nav>
    {message && <p className="friendly-error">{message}</p>}
    {search ? <section className="creator-section"><h2>Find {merchant ? 'Creators' : 'Businesses'}</h2><form className="search" onSubmit={find}><input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Name, public ID, city, or email"/><button>Search</button></form>{found.length === 0 ? <p className="compact-empty">Search for eligible partners.</p> : <div className="cards">{found.map((item) => <article key={item.id}><span>{item.publicCreatorId ?? item.publicMerchantId}</span><strong>{item.displayName ?? item.tradingName}</strong><p>{item.email ?? item.city}</p><button onClick={() => add(item)}>{merchant ? 'Add creator' : 'Request to promote'}</button></article>)}</div>}</section> : <section className="creator-section"><div className="title"><h2>{merchant ? 'Creator partnerships' : 'My Businesses'}</h2>{merchant && <select value={status} onChange={(e) => setStatus(e.target.value)}>{['All', 'Pending', 'Approved', 'Rejected', 'Suspended', 'Revoked', 'Expired', 'Blocked'].map((value) => <option key={value}>{value}</option>)}</select>}</div>{!items ? <p>Loading…</p> : items.length === 0 ? <p className="compact-empty">{merchant ? 'No partnerships found.' : 'No business partnerships yet.'}</p> : <div className="cards">{items.map((item) => <article key={item.id}><span>{statusLabel(item.status)}</span><strong>{merchant ? item.creatorName : item.merchantName}</strong><p>{formatDate(item.requestedAtUtc)}</p><div>{merchant && item.status === 'Pending' && <><button onClick={() => action(item, 'approve')}>Approve</button><button className="danger" onClick={() => action(item, 'reject')}>Reject</button></>}{!merchant && item.status === 'Approved' && <button className="danger" onClick={() => action(item, 'stop-promoting')}>Stop promoting</button>}</div></article>)}</div>}</section>}
  </>
}
