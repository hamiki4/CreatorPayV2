import {FormEvent, useEffect, useState} from 'react'
import {getAccessToken} from './sessionStore'
import {rankMatches, useTypeahead} from './typeahead'
import {AccountChrome, AccountStatusBadge} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {api as request} from './apiClient'
import {ProfileAvatar} from './profileMedia'
import {businessTypes} from './AuthWorkspace'
import {RoleNavigation} from './RoleNavigation'

type Wallet = {
  availableCashback: number
  reservedCashback: number
  currencyCode: string
  nextPayoutAtUtc?: string
  lastPayoutAtUtc?: string
  currentPeriodConfirmedPurchases: number
}
type Checkout = {
  id: string
  publicCheckoutId: string
  status: string
  campaignId: string
  purchaseAmount?: number
  cashbackAmount?: number
  expiresAtUtc: string
  qrPayload?: string
  merchantName?: string
  creatorName?: string
  createdAtUtc?: string
  resolvedAtUtc?: string
}
type AdvertisingRow = {
  relationshipId: string
  businessId: string
  publicBusinessId: string
  businessName: string
  city: string
  creatorId: string
  publicCreatorId: string
  creatorCode: string
  creatorName: string
  status: string
  daysLeft: number
  rewardsAvailable: boolean
  creatorProfileImageUrl?: string
  businessType?: string
  addressLine1?: string
  addressLine2?: string
  region?: string
  promotionVideoUrl?: string
  promotionVideoPlatform?: string
  promotionVideoStatus?: string
  promotionVideoRejectionReason?: string
  promotionVideoSubmittedAtUtc?: string
  promotionVideoReviewedAtUtc?: string
  distanceKm?: number
}
type ShopperProfile = {
  name: string
  email: string
  phone: string
  accountStatus: string
  effectiveStatus: string
  effectiveStatusReason: string
  isEmailVerified: boolean
  isPhoneVerified: boolean
}
type Offer = {
  offerCode: string
  title: string
  businessName: string
  description: string
  expiresAtUtc: string
  creatorDisplayName: string
}

const token = getAccessToken

async function api<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  return request<T>(path, {
    method,
    headers: method === 'GET' ? undefined : {'Idempotency-Key': crypto.randomUUID()},
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

const money = (v?: number, c = 'ETB') => `${(v ?? 0).toFixed(2)} ${c}`

export function ShopperOfferPage({code}: {code: string}) {
  const [offer, setOffer] = useState<Offer>()
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  useEffect(() => {
    api<Offer>(`/api/v1/discovery/offers/${encodeURIComponent(code)}`)
      .then(setOffer)
      .catch((e) => setError(e.message))
  }, [code])

  async function useOffer() {
    if (!token()) {
      sessionStorage.setItem('weymela_offer_return', location.pathname)
      location.assign('/')
      return
    }
    try {
      const x = await api<Checkout>('/api/v1/customer/checkouts/by-offer', 'POST', {offerCode: offer!.offerCode})
      setMessage(`Temporary checkout QR (valid for about 3 minutes): ${x.qrPayload}`)
    } catch (e) {
      setMessage((e as Error).message)
    }
  }

  if (error) {
    return (
      <main className="auth">
        <section className="panel">
          <h1>Offer unavailable</h1>
          <p>{error}</p>
          <a href="/">Discover Promotions</a>
        </section>
      </main>
    )
  }

  if (!offer) return <main className="auth"><p>Loading…</p></main>

  return (
    <main className="auth">
      <section className="panel offer-detail">
        <p className="eyebrow">Weymela</p>
        <h1>{offer.title}</h1>
        <h2>{offer.businessName}</h2>
        <p>{offer.description}</p>
        <p>With {offer.creatorDisplayName}</p>
        <button onClick={useOffer}>Support this Creator</button>
        {message && <aside>{message}</aside>}
      </section>
    </main>
  )
}

export function CustomerWorkspace({onSignOut}: {onSignOut: () => void}) {
  const [wallet, setWallet] = useState<Wallet>()
  const [checkouts, setCheckouts] = useState<Checkout[]>([])
  const [profile, setProfile] = useState<ShopperProfile>()
  const [rows, setRows] = useState<AdvertisingRow[]>([])
  const [query, setQuery] = useState('')
  const [businessType, setBusinessType] = useState('')
  const [sortBy, setSortBy] = useState<'recommended' | 'nearest'>('recommended')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [locationState, setLocationState] = useState<{
    latitude?: number
    longitude?: number
    label?: string
    error?: string
  }>()
  const [discoverError, setDiscoverError] = useState('')
  const [walletError, setWalletError] = useState('')
  const [checkoutError, setCheckoutError] = useState('')
  const [profileError, setProfileError] = useState('')
  const [message, setMessage] = useState('')
  const [processing, setProcessing] = useState<string>()
  const [view, setView] = useState<'discover' | 'confirmations' | 'cashback' | 'profile'>(() =>
    new URLSearchParams(location.search).get('view') === 'confirmations' ? 'confirmations' : 'discover',
  )

  const loadAccount = () => {
    api<Wallet>('/api/v1/customer/wallet')
      .then((value) => {
        setWallet(value)
        setWalletError('')
      })
      .catch((error) => {
        console.error(error)
        setWalletError("We couldn't load this information.")
      })
    api<Checkout[]>('/api/v1/customer/checkouts')
      .then((value) => {
        setCheckouts(value)
        setCheckoutError('')
      })
      .catch((error) => {
        console.error(error)
        setCheckoutError("We couldn't load this information.")
      })
    api<ShopperProfile>('/api/v1/customer/profile')
      .then((value) => {
        setProfile(value)
        setProfileError('')
      })
      .catch((error) => {
        console.error('Shopper profile unavailable', error)
        setProfileError('Profile is temporarily unavailable.')
      })
  }

  const find = async (term: string) => {
    try {
      setDiscoverError('')
      const p = new URLSearchParams()
      if (term) p.set('q', term)
      if (businessType) p.set('businessType', businessType)
      if (locationState?.latitude != null && locationState?.longitude != null) {
        p.set('latitude', String(locationState.latitude))
        p.set('longitude', String(locationState.longitude))
      }
      const found = rankMatches(
        await api<AdvertisingRow[]>(`/api/v1/customer/discovery/advertising?${p}`),
        term,
        (x) => [x.businessName, x.city, x.creatorName, x.publicBusinessId, x.publicCreatorId, x.businessType ?? ''],
      )
      if (sortBy === 'nearest' && locationState?.latitude != null && locationState?.longitude != null) {
        return [...found].sort(
          (a, b) =>
            (a.distanceKm ?? Number.POSITIVE_INFINITY) - (b.distanceKm ?? Number.POSITIVE_INFINITY) ||
            a.businessName.localeCompare(b.businessName),
        )
      }
      return found
    } catch {
      setDiscoverError("We couldn't load promotions right now.")
      return []
    }
  }

  useTypeahead(query, find, setRows, [businessType, sortBy, locationState?.latitude, locationState?.longitude])

  useEffect(() => {
    loadAccount()
    return onActionableRefresh(loadAccount)
  }, [])

  async function submit(e: FormEvent) {
    e.preventDefault()
    setRows(await find(query.trim()))
  }

  async function nearMe() {
    if (!navigator.geolocation) {
      setLocationState({error: 'Location is unavailable in this browser.'})
      return
    }
    setLocationState({label: 'Detecting your location…'})
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setSortBy('nearest')
        setLocationState({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          label: 'Using your current location',
        })
        void find(query.trim()).then(setRows)
      },
      (error) => {
        setLocationState({error: error.message || 'Location permission is needed for Nearby sorting.'})
      },
      {enableHighAccuracy: true, timeout: 8000, maximumAge: 60000},
    )
  }

  async function decide(x: Checkout, approve: boolean) {
    if (processing) return
    setProcessing(x.id)
    try {
      const resolved = await api<Checkout>(`/api/v1/customer/checkouts/${x.id}/${approve ? 'approve' : 'reject'}`, 'POST')
      setCheckouts((current) => current.map((item) => (item.id === x.id ? resolved : item)))
      setMessage(approve ? 'Purchase confirmed.' : 'Purchase rejected.')
      loadAccount()
    } catch (e) {
      console.error(e)
      setMessage("We couldn't update this purchase confirmation.")
    } finally {
      setProcessing(undefined)
    }
  }

  const pendingCount = checkouts.filter((x) => x.status === 'AwaitingCustomerApproval').length
  const navigate = (target: string) =>
    setView(
      target.includes('confirmation')
        ? 'confirmations'
        : target.includes('cashback')
          ? 'cashback'
          : target.includes('profile')
            ? 'profile'
            : 'discover',
    )
  const visibleRows = rows.filter((x) => x.rewardsAvailable)
  const navItems = [
    {id: 'discover', label: 'Discover', icon: 'discover' as const, active: view === 'discover', onSelect: () => setView('discover')},
    {id: 'cashback', label: 'Cashback', icon: 'cashback' as const, active: view === 'cashback', onSelect: () => setView('cashback')},
    {id: 'profile', label: 'Profile', icon: 'profile' as const, active: view === 'profile', onSelect: () => setView('profile')},
  ]

  return (
    <AccountChrome
      role="Customer"
      name={profile?.name}
      status={profile?.effectiveStatus}
      onProfile={() => setView('profile')}
      onHelp={() => location.assign('/help')}
      onSignOut={onSignOut}
      onNavigate={navigate}
    >
      <section className="shopper">
        <RoleNavigation role="Customer" label="Customer navigation" items={navItems} />

        {view === 'discover' && (
          <section>
            <h2>Discover Promotions</h2>
            {discoverError && <p className="friendly-error" role="alert">{discoverError}</p>}
            {pendingCount > 0 && (
              <button className="pending-confirmation-link" onClick={() => setView('confirmations')}>
                Purchase confirmations <span>{pendingCount}</span>
              </button>
            )}
            <form className="search-bar shopper-relationship-search" onSubmit={submit}>
              <label className="shopper-search-field">
                <span>Business or Creator</span>
                <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search business or creator" />
              </label>
              <button
                type="button"
                className="filter-toggle quiet"
                aria-label="Open filters"
                aria-expanded={filtersOpen}
                onClick={() => setFiltersOpen(!filtersOpen)}
              >
                <svg aria-hidden="true" viewBox="0 0 24 24">
                  <path d="M4 5h16l-6 7v5l-4 2v-7z" />
                </svg>
                <span className="filter-toggle-label">Filter</span>
              </button>
              <div className="search-actions">
                <button>Search</button>
              </div>
            </form>

            {(businessType || sortBy !== 'recommended') && (
              <p className="filter-summary" role="status">
                {businessType || 'All Business Types'} · {sortBy === 'nearest' ? 'Nearest' : 'Recommended'}
              </p>
            )}

            {locationState?.label && (
              <p className="success-note" role="status">
                {locationState.label}
              </p>
            )}
            {locationState?.error && (
              <p className="friendly-error" role="alert">
                {locationState.error}
              </p>
            )}

            {visibleRows.length ? (
              <div className="shopper-relationship-list simplified customer-discovery-list" aria-label="Active promotions">
                <div className="shopper-relationship-row headings">
                  <span>Business</span>
                  <span>Promoted by</span>
                  <span>Video</span>
                </div>
                {visibleRows.map((x) => (
                  <article className="shopper-relationship-row customer-discovery-row" key={x.relationshipId}>
                    <div>
                      <strong data-label="Business">{x.businessName}</strong>
                      <small>
                        {x.addressLine1 ?? x.city}
                        {x.addressLine2 ? ` · ${x.addressLine2}` : ''}
                        {x.region ? `, ${x.region}` : ''}
                      </small>
                      <small>{x.businessType ?? 'Business'}</small>
                      {typeof x.distanceKm === 'number' && <small>{x.distanceKm.toFixed(1)} km</small>}
                    </div>
                    <span data-label="Promoted by">
                      <span className="creator-heading">
                        <ProfileAvatar name={x.creatorName} photoUrl={x.creatorProfileImageUrl} />
                        <span>
                          <strong>{x.creatorName}</strong>
                          <small>{x.creatorCode}</small>
                        </span>
                      </span>
                    </span>
                    <span data-label="Video">
                      {x.promotionVideoUrl && x.promotionVideoStatus === 'Live' ? (
                        <a
                          className="promo-video-link"
                          href={x.promotionVideoUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          {x.promotionVideoPlatform === 'TikTok' ? 'TikTok Video' : 'Promo Video'}
                        </a>
                      ) : null}
                    </span>
                  </article>
                ))}
              </div>
            ) : (
              <p className="compact-empty">No active promotions found.</p>
            )}

            {filtersOpen && (
              <div className="modal-backdrop">
                <section className="help-dialog filters-dialog" role="dialog" aria-modal="true" aria-labelledby="discovery-filters-title">
                  <h2 id="discovery-filters-title">Filters</h2>
                  <p>Refine active promotions.</p>
                  <div className="filter-sheet">
                    <label>
                      <span>Business Type</span>
                      <select value={businessType} onChange={(e) => setBusinessType(e.target.value)}>
                        <option value="">All Business Types</option>
                        {businessTypes.map((x) => (
                          <option key={x.value} value={x.value}>
                            {x.en}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="filter-sheet-group">
                      <span>Sort</span>
                      <button type="button" className={sortBy === 'nearest' ? 'active' : ''} onClick={() => { setSortBy('nearest'); void nearMe() }}>
                        Nearest
                      </button>
                      <button type="button" className={sortBy === 'recommended' ? 'active' : ''} onClick={() => setSortBy('recommended')}>
                        Recommended
                      </button>
                    </div>
                    <button type="button" className="quiet near-me-compact" onClick={() => void nearMe()}>
                      📍 Near Me
                    </button>
                  </div>
                  <div className="actions">
                    <button type="button" onClick={() => setFiltersOpen(false)}>
                      Done
                    </button>
                    <button
                      type="button"
                      className="quiet"
                      onClick={() => {
                        setBusinessType('')
                        setSortBy('recommended')
                      }}
                    >
                      Clear
                    </button>
                  </div>
                </section>
              </div>
            )}
          </section>
        )}

        {view === 'confirmations' && (
          <>
            {message && <aside role="status">{message}</aside>}
            {checkoutError && <p className="friendly-error" role="alert">{checkoutError}</p>}
            <Confirmations items={checkouts} processing={processing} decide={decide} />
          </>
        )}

        {view === 'cashback' && (
          <>
            {walletError && <p className="friendly-error" role="alert">{walletError}</p>}
            <Summary wallet={wallet} />
          </>
        )}

        {view === 'profile' && (
          <>
            {profileError && <p className="friendly-error" role="alert">{profileError}</p>}
            <ShopperProfileCard profile={profile} onHelp={() => location.assign('/help')} onSignOut={onSignOut} />
          </>
        )}
      </section>
    </AccountChrome>
  )
}

function Confirmations({
  items,
  processing,
  decide,
}: {
  items: Checkout[]
  processing?: string
  decide: (x: Checkout, approve: boolean) => void
}) {
  const pending = items
    .filter((x) => x.status === 'AwaitingCustomerApproval')
    .sort((a, b) => new Date(b.createdAtUtc ?? 0).getTime() - new Date(a.createdAtUtc ?? 0).getTime())
  const history = items
    .filter((x) => ['Completed', 'Rejected'].includes(x.status))
    .sort(
      (a, b) =>
        new Date(b.resolvedAtUtc ?? b.createdAtUtc ?? 0).getTime() -
        new Date(a.resolvedAtUtc ?? a.createdAtUtc ?? 0).getTime(),
    )

  return (
    <section>
      <h2>Action Required</h2>
      {pending.length ? (
        <div className="confirmations pending-confirmations">
          {pending.map((x) => {
            const when = new Date(x.createdAtUtc ?? x.expiresAtUtc)
            const busy = processing === x.id
            return (
              <article key={x.id}>
                <div className="confirmation-details">
                  <span>Business</span>
                  <strong>{x.merchantName ?? 'Business'}</strong>
                  <span>Amount</span>
                  <strong>{money(x.purchaseAmount)}</strong>
                  <span>Date / Time</span>
                  <strong>{new Intl.DateTimeFormat('en-GB', {dateStyle: 'medium', timeStyle: 'short'}).format(when)}</strong>
                </div>
                <p>Is this your purchase?</p>
                <div className="confirmation-actions">
                  <button disabled={busy} onClick={() => decide(x, true)}>
                    {busy ? 'PROCESSING…' : "YES, IT'S ME"}
                  </button>
                  <button disabled={busy} className="danger" onClick={() => decide(x, false)}>
                    NO, IT'S NOT ME
                  </button>
                </div>
              </article>
            )
          })}
        </div>
      ) : (
        <p className="compact-empty">No confirmations need your action.</p>
      )}
      <h2>History</h2>
      {history.length ? (
        <div className="confirmation-history">
          <div className="confirmation-row headings">
            <span>Business</span>
            <span>Amount</span>
            <span>Status</span>
            <span>Date</span>
            <span>Time</span>
          </div>
          {history.map((x) => {
            const when = new Date(x.resolvedAtUtc ?? x.createdAtUtc ?? x.expiresAtUtc)
            return (
              <article className="confirmation-row" key={x.id}>
                <strong data-label="Business">{x.merchantName ?? 'Business'}</strong>
                <span data-label="Amount">{money(x.purchaseAmount)}</span>
                <span data-label="Status">{x.status === 'Completed' ? 'Completed' : 'Rejected'}</span>
                <span data-label="Date">{new Intl.DateTimeFormat('en-GB').format(when)}</span>
                <span data-label="Time">{when.toLocaleTimeString([], {hour: '2-digit', minute: '2-digit'})}</span>
              </article>
            )
          })}
        </div>
      ) : (
        <p className="compact-empty">No completed confirmations yet.</p>
      )}
    </section>
  )
}

function Summary({wallet}: {wallet?: Wallet}) {
  const date = (value?: string) => (value ? new Intl.DateTimeFormat('en-GB').format(new Date(value)) : '—')
  return (
    <div className="summary-grid">
      <article className="summary-card">
        <span>Available Cashback</span>
        <strong>{money(wallet?.availableCashback, wallet?.currencyCode)}</strong>
      </article>
      <article className="summary-card">
        <span>Next Payout Date</span>
        <strong>{date(wallet?.nextPayoutAtUtc)}</strong>
      </article>
    </div>
  )
}

function ShopperProfileCard({profile,onHelp,onSignOut}: {profile?: ShopperProfile;onHelp:()=>void;onSignOut:()=>void}) {
  if (!profile) return <p className="compact-empty">Profile is temporarily unavailable.</p>
  const rows = [
    ['Name', profile.name],
    ['Email', profile.email],
    ['Phone', profile.phone],
  ]
  return (
    <section className="shopper-profile">
      <h2>Profile</h2>
      <dl>
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
        <div>
          <dt>Status</dt>
          <dd><AccountStatusBadge status={profile.effectiveStatus} /></dd>
        </div>
      </dl>
      <div className="profile-actions">
        <button type="button" className="quiet" onClick={onHelp}>Help</button>
        <button type="button" className="danger" onClick={onSignOut}>Sign out</button>
      </div>
    </section>
  )
}

export const shopperLabels = {
  en: ['Discover Promotions', 'Cashback', 'Search business or creator', 'Creator ID', 'No active promotions found.', 'Available Cashback', 'Next Payout Date'],
}
