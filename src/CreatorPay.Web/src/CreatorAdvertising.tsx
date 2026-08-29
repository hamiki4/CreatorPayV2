import {FormEvent, useEffect, useState} from 'react'
import {api} from './apiClient'
import {daysLeftText, relationshipState} from './relationshipTime'
import {currentPartnerships} from './partnershipState'
import {rankMatches, useTypeahead} from './typeahead'

type Business = {id: string; publicMerchantId: string; tradingName: string; legalBusinessName: string; city: string; businessType: string}
type PromotionVideo = {id: string; videoUrl: string; platform: string; status: string; submittedAtUtc: string; reviewedAtUtc?: string; rejectionReason?: string}
export type AdvertisingRequest = {
  id: string
  merchantId: string
  merchantName: string
  status: string
  relationshipState?: string
  activationRequired?: boolean
  requestedAtUtc: string
  startDateUtc?: string
  endDateUtc?: string
  activatedAtUtc?: string
  expiresAtUtc?: string
  promotionActive: boolean
  initiatedBy?: 'Business' | 'Creator' | 'Unknown'
  promotionVideo?: PromotionVideo
}

export function FindBusinesses({onRequested}: {onRequested: () => void}) {
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('')
  const [items, setItems] = useState<Business[]>([])
  const [requests, setRequests] = useState<AdvertisingRequest[]>([])
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')

  const loadRequests = () => api<AdvertisingRequest[]>('/api/v1/creator/partnerships').then(setRequests).catch(console.error)

  useEffect(() => {
    void loadRequests()
  }, [])

  async function find(term: string) {
    setLoading(true)
    setMessage('')
    try {
      const found = await api<Business[]>(`/api/v1/creator/merchants/search?q=${encodeURIComponent(term)}`)
      const filtered = category ? found.filter((x) => x.businessType === category) : found
      return rankMatches(filtered, term, (x) => [x.tradingName, x.legalBusinessName, x.city, x.publicMerchantId])
    } catch (error) {
      console.error(error)
      setMessage("We couldn't load this information.")
      return []
    } finally {
      setLoading(false)
    }
  }

  useTypeahead(query, find, setItems, [category])

  async function search(e?: FormEvent) {
    e?.preventDefault()
    setItems(await find(query))
  }

  async function request(business: Business) {
    setMessage('')
    try {
      await api('/api/v1/creator/partnerships/requests', {
        method: 'POST',
        body: JSON.stringify({merchantId: business.id, introductoryMessage: null}),
      })
      setMessage(`Advertising request sent to ${business.tradingName}.`)
      await loadRequests()
      onRequested()
    } catch (error) {
      console.error(error)
      setMessage("We couldn't complete that advertising request.")
    }
  }

  const categories = ['Restaurant / Café', 'Grocery / Mini-market', 'Clothing / Boutique', 'Beauty / Salon', 'Furniture', 'Electronics', 'Hotel / Travel', 'Professional Services', 'Other']
  const currentRequests = currentPartnerships(requests, (x) => x.merchantId)

  function stateFor(business: Business) {
    const relationship =
      currentRequests.find((x) => x.merchantId === business.id) ||
      currentRequests.find((x) => x.merchantName.toLowerCase() === business.tradingName.toLowerCase())
    if (!relationship) return {label: 'No relationship', canRequest: true}
    if (relationship.status === 'Pending') return {label: 'Request Pending', canRequest: false}
    if (relationship.status === 'Approved') {
      const state = relationshipState(relationship)
      return {label: state.label === 'Active' ? 'Active Ad' : state.label, canRequest: false}
    }
    if (['Revoked', 'Suspended'].includes(relationship.status)) return {label: 'Deactivated', canRequest: false, reactivationManagedByBusiness: true}
    if (relationship.status === 'Blocked') return {label: 'Blocked', canRequest: false}
    if (relationship.status === 'Rejected') return {label: 'Declined', canRequest: true}
    return {label: 'Unavailable', canRequest: false}
  }

  return (
    <section className="creator-section">
      <h2>Find Businesses</h2>
      <p>Find a Business and ask for permission to advertise.</p>
      <form className="business-search" onSubmit={search}>
        <label>
          Business name
          <input type="search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search by name, city, or public ID" />
        </label>
        <label>
          Category
          <select value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All categories</option>
            {categories.map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </label>
        <button disabled={loading}>{loading ? 'Searching…' : 'Search'}</button>
      </form>
      {message && <p className={message.includes('sent') || message.includes('Active') ? 'success-note' : 'friendly-error'} role="status">{message}</p>}
      {!loading && items.length === 0 ? (
        <p className="compact-empty">No eligible Businesses found.</p>
      ) : (
        <div className="discovery-list">
          {items.map((x) => {
            const state = stateFor(x)
            return (
              <article className="discovery-row business-discovery-card" key={x.id}>
                <div className="business-card-top">
                  <strong>{x.tradingName}</strong>
                  <span>{x.businessType}</span>
                  <small>📍 {x.city}</small>
                </div>
                <div className="business-card-meta">
                  <span className="status-badge">{state.label}</span>
                </div>
                <div className="business-card-actions">
                  {state.canRequest && <button onClick={() => void request(x)}>{state.label === 'Declined' ? 'Request Again' : 'Request to Advertise'}</button>}
                  {state.reactivationManagedByBusiness && <span className="hint">The Business can Reactivate this advertising relationship.</span>}
                </div>
              </article>
            )
          })}
        </div>
      )}
    </section>
  )
}

type AdBusiness = {partnershipId: string; merchantId: string; businessName: string; status: 'Active' | 'Deactivated' | 'Expired'; expiresAtUtc?: string; confirmedSales: number; creatorEarned: number; currencyCode: string}
type AdTransaction = {transactionId: string; partnershipId: string; merchantId: string; businessName: string; confirmedAtUtc: string; status: 'Confirmed'; creatorEarned: number; currencyCode: string}
type AdReport = {businesses: AdBusiness[]; transactions: AdTransaction[]}
const adMoney = (value: number, currency = 'ETB') => new Intl.NumberFormat('en-ET', {style: 'currency', currency}).format(value)

export function CreatorRequests({items, refresh}: {items: AdvertisingRequest[]; refresh: () => void}) {
  const [message, setMessage] = useState('')
  async function decide(item: AdvertisingRequest, accept: boolean) {
    try {
      await api(`/api/v1/creator/partnerships/${item.id}/${accept ? 'accept-invitation' : 'decline-invitation'}`, {method: 'POST'})
      setMessage(accept ? `Invitation from ${item.merchantName} accepted.` : `Invitation from ${item.merchantName} declined.`)
      refresh()
    } catch (e) {
      setMessage((e as Error).message)
    }
  }
  async function requestAgain(item: AdvertisingRequest) {
    try {
      await api('/api/v1/creator/partnerships/requests', {method: 'POST', body: JSON.stringify({merchantId: item.merchantId, introductoryMessage: null})})
      setMessage(`Request sent to ${item.merchantName}.`)
      refresh()
    } catch (e) {
      setMessage((e as Error).message)
    }
  }
  const active = currentPartnerships(items, (x) => x.merchantId).filter((x) => x.status === 'Pending' || x.status === 'Rejected' || x.status === 'Suspended' || x.status === 'Revoked')
  return (
    <section className="creator-section">
      <h2>Requests</h2>
      {message && <p className="friendly-error" role="status">{message}</p>}
      {active.length === 0 ? (
        <p className="compact-empty">No requests or invitations.</p>
      ) : (
        <div className="request-groups">
          {active.map((x) => (
            <article key={x.id}>
              <div>
                <strong>{x.merchantName}</strong>
                <small>{new Date(x.requestedAtUtc).toLocaleDateString()}</small>
              </div>
              <span className="status-badge">{x.status === 'Rejected' ? 'Declined' : x.status}</span>
              {x.status === 'Pending' && x.initiatedBy === 'Business' && (
                <div>
                  <button onClick={() => void decide(x, true)}>Accept</button>
                  <button className="quiet" onClick={() => void decide(x, false)}>Decline</button>
                </div>
              )}
              {x.status === 'Rejected' && <button onClick={() => void requestAgain(x)}>Request Again</button>}
            </article>
          ))}
        </div>
      )}
    </section>
  )
}

export function CreatorConfirmedSales() {
  const [report, setReport] = useState<AdReport>()
  const [error, setError] = useState('')

  useEffect(() => {
    api<AdReport>('/api/v1/creator/ads/performance').then(setReport).catch((e) => setError((e as Error).message))
  }, [])

  const rows = report?.transactions ?? []
  return (
    <section className="creator-section">
      <h2>Confirmed Sales</h2>
      {error && <p className="friendly-error">{error}</p>}
      {rows.length === 0 ? (
        <p className="compact-empty">No confirmed sales yet.</p>
      ) : (
        <div className="confirmed-sale-list creator-confirmed-sale-list">
          {rows.map((x) => {
            const confirmed = new Date(x.confirmedAtUtc)
            return (
              <article className="confirmed-sale-card" key={x.transactionId}>
                <div className="confirmed-sale-heading">
                  <strong>{x.businessName}</strong>
                  <span className="status-badge"><span aria-hidden="true">●</span> {x.status}</span>
                </div>
                <time dateTime={x.confirmedAtUtc}>
                  {confirmed.toLocaleDateString()} · {confirmed.toLocaleTimeString([], {hour: 'numeric', minute: '2-digit'})}
                </time>
                <div className="confirmed-sale-earned">
                  <span>You earned</span>
                  <strong>+{adMoney(x.creatorEarned, x.currencyCode)}</strong>
                </div>
              </article>
            )
          })}
        </div>
      )}
    </section>
  )
}

export function ActiveAds({items, loading, refresh}: {items: AdvertisingRequest[]; loading: boolean; refresh: () => void}) {
  const [editing, setEditing] = useState<{id: string; businessName: string; video?: PromotionVideo} | null>(null)
  const [videoUrl, setVideoUrl] = useState('')
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)

  const activeRelationships = items.filter((x) => relationshipState(x).label === 'Active')

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!editing || saving) return
    setSaving(true)
    setMessage('')
    try {
      await api(`/api/v1/creator/partnerships/${editing.id}/promotion-video`, {
        method: 'POST',
        body: JSON.stringify({videoUrl}),
      })
      setMessage('Promotion video submitted. Waiting for business approval.')
      setEditing(null)
      refresh()
    } catch (error) {
      console.error(error)
      setMessage((error as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const promptForVideo = (relationship: AdvertisingRequest, video?: PromotionVideo) => {
    setEditing({id: relationship.id, businessName: relationship.merchantName, video})
    setVideoUrl(video?.videoUrl ?? '')
  }

  return (
    <section className="creator-section">
      <h2>Active Ads</h2>
      {message && <p className={message.includes('submitted') ? 'success-note' : 'friendly-error'} role="status">{message}</p>}
      {activeRelationships.length === 0 ? (
        <p className="compact-empty">No active advertising relationships yet.</p>
      ) : (
        <div className="table-wrap">
          <div className="creator-ads-table creator-ads-list" role="table" aria-label="Active Ads">
            <div className="creator-ads-row headings" role="row">
              <span role="columnheader">Business</span>
              <span role="columnheader">Promo Video</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Date Activated</span>
              <span role="columnheader">Days Left</span>
            </div>
            {activeRelationships.map((x) => {
              const state = relationshipState(x)
              const days = state.daysLeft === null ? '—' : daysLeftText(state.daysLeft, state.tone)
              const promo = x.promotionVideo
              const statusText = promo?.status === 'Pending' ? 'Active' : 'Active'
              const action =
                promo?.status === 'Live' ? (
                  <a href={promo.videoUrl} target="_blank" rel="noopener noreferrer">View TikTok Video</a>
                ) : promo?.status === 'Pending' ? (
                  <a href={promo.videoUrl} target="_blank" rel="noopener noreferrer">View Submitted Link</a>
                ) : promo?.status === 'Rejected' ? (
                  <button type="button" className="quiet" onClick={() => promptForVideo(x, promo)}>Submit New Video</button>
                ) : promo?.status === 'Expired' ? (
                  <button type="button" className="quiet" onClick={() => promptForVideo(x, promo)}>Add Promo Video</button>
                ) : (
                  <button type="button" className="quiet" onClick={() => promptForVideo(x, promo)}>Add Promo Video</button>
                )

              return (
                <div className="creator-ads-row relationship-active" role="row" key={x.id}>
                  <div className="creator-ads-business" role="cell" data-label="Business">
                    <strong>{x.merchantName}</strong>
                  </div>
                  <div className="creator-ads-promo" role="cell" data-label="Promo Video">
                    {action}
                  </div>
                  <div className="creator-ads-status" role="cell" data-label="Status">
                    <span className="status-badge">{statusText}</span>
                  </div>
                  <div className="creator-ads-activated" role="cell" data-label="Date Activated">
                    {x.activatedAtUtc ? new Date(x.activatedAtUtc).toLocaleDateString() : '—'}
                  </div>
                  <div className="creator-ads-days" role="cell" data-label="Days Left">
                    {days}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {editing && (
        <div className="modal-backdrop">
          <section className="help-dialog promo-video-dialog" role="dialog" aria-modal="true" aria-labelledby="promo-video-title">
            <h2 id="promo-video-title">Add Promo Video</h2>
            <p><strong>{editing.businessName}</strong></p>
            <p>Paste the link to the exact TikTok video promoting this business.</p>
            <form onSubmit={(e) => void submit(e)}>
              <label>
                Promotion Video Link
                <input
                  value={videoUrl}
                  onChange={(e) => setVideoUrl(e.target.value)}
                  placeholder="https://www.tiktok.com/@creator/video/1234567890"
                />
              </label>
              <div className="actions">
                <button type="submit" disabled={saving}>{saving ? 'Submitting…' : 'Submit for Approval'}</button>
                <button type="button" className="quiet" onClick={() => setEditing(null)}>Cancel</button>
              </div>
            </form>
          </section>
        </div>
      )}
    </section>
  )
}
