import {FormEvent, useEffect, useState} from 'react'
import {getAccessToken} from './sessionStore'
import {rankMatches, useTypeahead} from './typeahead'
import {AccountChrome, AccountStatusBadge} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {api as request} from './apiClient'
import {ProfileAvatar} from './profileMedia'
import {businessTypes} from './AuthWorkspace'
import {RoleNavigation} from './RoleNavigation'
import {NavIcon} from './navIcons'
import {formatAmount, formatDate, formatDateTime, formatTime} from './displayFormat'
import {createUuid} from './uuid'

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
  businessLatitude?: number
  businessLongitude?: number
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
    headers: method === 'GET' ? undefined : {'Idempotency-Key': createUuid()},
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

const money = (v?: number, _currency?: string) => formatAmount(v)
const locationRadiusOptions = [1, 3, 5, 10, 20] as const
const missingDisplayValues = new Set(['not provided', 'n/a', 'na', 'unknown'])

function displayText(value?: string) {
  const text = value?.trim()
  return text && !missingDisplayValues.has(text.toLocaleLowerCase()) ? text : undefined
}

function businessTypeLabel(value?: string) {
  const text = displayText(value)
  if (!text) return undefined
  return displayText(businessTypes.find((item) => item.value === text)?.en ?? text)
}

function businessAddress(row: AdvertisingRow) {
  return [row.addressLine1, row.addressLine2, row.city, row.region]
    .flatMap((part) => part?.split(',') ?? [])
    .map((part) => displayText(part))
    .filter((part): part is string => Boolean(part))
    .filter((part, index, parts) => parts.findIndex((candidate) => candidate.toLocaleLowerCase() === part.toLocaleLowerCase()) === index)
    .join(', ')
}

function directionsUrl(row: AdvertisingRow) {
  const destination =
    typeof row.businessLatitude === 'number' && typeof row.businessLongitude === 'number'
      ? `${row.businessLatitude},${row.businessLongitude}`
      : [businessAddress(row), 'Ethiopia'].filter(Boolean).join(', ')
  return destination
    ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(destination)}`
    : undefined
}

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
  const [homeRows, setHomeRows] = useState<AdvertisingRow[]>([])
  const [homeLoading, setHomeLoading] = useState(true)
  const [rows, setRows] = useState<AdvertisingRow[]>([])
  const [query, setQuery] = useState('')
  const [businessType, setBusinessType] = useState('')
  const [sortBy, setSortBy] = useState<'recommended' | 'nearest'>('recommended')
  const [radiusKm, setRadiusKm] = useState<number | ''>('')
  const [filtersOpen, setFiltersOpen] = useState(true)
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
  const [view, setView] = useState<'home' | 'discover' | 'confirmations' | 'cashback' | 'profile'>(() =>
    new URLSearchParams(location.search).get('view') === 'confirmations' ? 'confirmations' : 'home',
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

  const fetchPromotions = async (
    term: string,
    selectedBusinessType = businessType,
    coordinates = locationState,
  ) => {
    const p = new URLSearchParams()
    if (term) p.set('q', term)
    if (selectedBusinessType) p.set('businessType', selectedBusinessType)
    if (coordinates?.latitude != null && coordinates.longitude != null) {
      p.set('latitude', String(coordinates.latitude))
      p.set('longitude', String(coordinates.longitude))
    }
    return rankMatches(
      await api<AdvertisingRow[]>(`/api/v1/customer/discovery/advertising?${p}`),
      term,
      (x) => [x.businessName, x.city, x.creatorName, x.publicBusinessId, x.publicCreatorId, x.creatorCode, x.businessType ?? ''],
    )
  }

  const find = async (term: string) => {
    try {
      setDiscoverError('')
      let found = await fetchPromotions(term)
      if (radiusKm !== '' && locationState?.latitude != null && locationState.longitude != null) {
        found = found.filter((row) => typeof row.distanceKm === 'number' && row.distanceKm <= radiusKm)
      }
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

  useTypeahead(query, find, setRows, [businessType, sortBy, radiusKm, locationState?.latitude, locationState?.longitude])

  useEffect(() => {
    let active = true
    setHomeLoading(true)
    void fetchPromotions('', '', locationState)
      .then((value) => {
        if (active) setHomeRows(value)
      })
      .catch((error) => {
        console.error(error)
        if (active) setDiscoverError("We couldn't load promotions right now.")
      })
      .finally(() => {
        if (active) setHomeLoading(false)
      })
    return () => {
      active = false
    }
  }, [locationState?.latitude, locationState?.longitude])

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
      setLocationState({error: 'Location is unavailable on this device. You can still browse all promotions.'})
      return
    }
    setLocationState({label: 'Finding your location…'})
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setSortBy('nearest')
        setLocationState({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          label: 'Location is on. Distances are shown where Business coordinates are available.',
        })
      },
      (error) => {
        setLocationState({
          error:
            error.code === 1
              ? 'Location access is off. Enable location to see promotions near you.'
              : "We couldn't determine your location. You can still browse all promotions.",
        })
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
            : target.includes('discover') || target.includes('promotion')
              ? 'discover'
              : 'home',
    )
  const visibleRows = rows.filter((x) => x.rewardsAvailable)
  const liveHomeRows = homeRows.filter((x) => x.rewardsAvailable)
  const nearbyRows = [...liveHomeRows]
    .filter((x) => typeof x.distanceKm === 'number')
    .sort((a, b) => (a.distanceKm ?? 0) - (b.distanceKm ?? 0))
    .slice(0, 3)
  const nearbyIds = new Set(nearbyRows.map((x) => x.relationshipId))
  const recommendedRows = liveHomeRows.filter((x) => !nearbyIds.has(x.relationshipId)).slice(0, 3)
  const navItems = [
    {id: 'home', label: 'Home', icon: 'home' as const, active: view === 'home', onSelect: () => setView('home')},
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
      onHelp={() => location.assign('/help?category=Customers')}
      onSignOut={onSignOut}
      onNavigate={navigate}
    >
      <section className="shopper">
        <RoleNavigation role="Customer" label="Customer navigation" items={navItems} />

        {view === 'home' && (
          <section className="customer-home">
            <div className="customer-home-heading">
              <div>
                <p className="customer-kicker">Welcome back</p>
                <h2>Home</h2>
              </div>
              {profile?.name && <strong>{profile.name}</strong>}
            </div>

            <section className="customer-home-section" aria-labelledby="nearby-promotions-title">
              <div className="customer-section-heading">
                <h3 id="nearby-promotions-title">Nearby Promotions</h3>
                <button type="button" className="customer-text-action" onClick={() => setView('discover')}>See all</button>
              </div>
              {nearbyRows.length ? (
                <div className="customer-promotion-feed customer-promotion-feed--home">
                  {nearbyRows.map((row) => <PromotionCard key={row.relationshipId} row={row} compact />)}
                </div>
              ) : (
                <div className="customer-location-prompt">
                  <NavIcon name="mapPin" />
                  <div>
                    <strong>See what is close to you</strong>
                    <span>Use your location to sort eligible live promotions by distance.</span>
                  </div>
                  <button type="button" onClick={() => void nearMe()}>Use Location</button>
                </div>
              )}
            </section>

            {(recommendedRows.length > 0 || (nearbyRows.length === 0 && liveHomeRows.length > 0)) && (
              <section className="customer-home-section" aria-labelledby="recommended-promotions-title">
                <div className="customer-section-heading">
                  <h3 id="recommended-promotions-title">Recommended Promotions</h3>
                </div>
                <div className="customer-promotion-feed customer-promotion-feed--home">
                  {(recommendedRows.length ? recommendedRows : liveHomeRows.slice(0, 3)).map((row) => (
                    <PromotionCard key={row.relationshipId} row={row} compact />
                  ))}
                </div>
              </section>
            )}

            {homeLoading && (
              <div className="customer-home-loading" role="status">Loading promotions…</div>
            )}

            {!homeLoading && !liveHomeRows.length && !discoverError && (
              <div className="customer-home-empty">
                <NavIcon name="discover" />
                <strong>No live promotions yet</strong>
                <span>New eligible promotions will appear here after their Creators go live.</span>
              </div>
            )}

            <section className="customer-home-section" aria-labelledby="cashback-summary-title">
              <div className="customer-section-heading">
                <h3 id="cashback-summary-title">Cashback</h3>
                <button type="button" className="customer-text-action" onClick={() => setView('cashback')}>View details</button>
              </div>
              <div className="customer-cashback-summary">
                <div><span>Available Cashback</span><strong>{money(wallet?.availableCashback)}</strong></div>
                <div><span>Next Payout Date</span><strong>{formatDate(wallet?.nextPayoutAtUtc)}</strong></div>
              </div>
            </section>

            <button type="button" className="customer-discover-cta" onClick={() => setView('discover')}>
              <NavIcon name="discover" />
              Discover Promotions
            </button>
          </section>
        )}

        {view === 'discover' && (
          <section className="customer-discover">
            <h2>Discover Promotions</h2>
            {discoverError && <p className="friendly-error" role="alert">{discoverError}</p>}
            {pendingCount > 0 && (
              <button className="pending-confirmation-link" onClick={() => setView('confirmations')}>
                Purchase confirmations <span>{pendingCount}</span>
              </button>
            )}
            <form className="customer-discovery-search" onSubmit={submit}>
              <label className="customer-search-field">
                <span className="sr-only">Search business or creator</span>
                <NavIcon name="discover" />
                <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search business or creator" />
              </label>
              <button
                type="button"
                className="filter-toggle quiet"
                aria-label={filtersOpen ? 'Hide filters' : 'Show filters'}
                aria-expanded={filtersOpen}
                onClick={() => setFiltersOpen(!filtersOpen)}
              >
                <NavIcon name="filter" />
                <span>Filter</span>
              </button>
            </form>

            {filtersOpen && (
              <section className="customer-filter-panel" aria-label="Promotion filters">
                <h3>Filters</h3>
                <div className="customer-filter-controls">
                  <label>
                    <span className="sr-only">Business Type</span>
                    <select aria-label="Business Type" value={businessType} onChange={(e) => setBusinessType(e.target.value)}>
                      <option value="">Business Type</option>
                      {businessTypes.map((x) => <option key={x.value} value={x.value}>{x.en}</option>)}
                    </select>
                  </label>
                  <label>
                    <span className="sr-only">Distance</span>
                    <select
                      aria-label="Distance"
                      value={radiusKm}
                      onChange={(e) => {
                        const value = e.target.value ? Number(e.target.value) : ''
                        setRadiusKm(value)
                        if (value !== '') setSortBy('nearest')
                      }}
                    >
                      <option value="">Nearest</option>
                      {locationRadiusOptions.map((radius) => <option key={radius} value={radius}>Within {radius} km</option>)}
                    </select>
                  </label>
                  <label>
                    <span className="sr-only">Sort promotions</span>
                    <select aria-label="Sort promotions" value={sortBy} onChange={(e) => setSortBy(e.target.value as 'recommended' | 'nearest')}>
                      <option value="recommended">Recommended</option>
                      <option value="nearest">Nearest</option>
                    </select>
                  </label>
                </div>
              </section>
            )}

            {locationState?.label && (
              <p className="success-note" role="status">
                {locationState.label}
              </p>
            )}
            {locationState?.error && (
              <div className="customer-location-message friendly-error" role="alert">
                <span>{locationState.error}</span>
                <button type="button" className="quiet" onClick={() => void nearMe()}>Try Again</button>
              </div>
            )}

            {(sortBy === 'nearest' || radiusKm !== '') && locationState?.latitude == null && !locationState?.error && (
              <div className="customer-location-message" role="status">
                <span>Use your location to see distances and nearest promotions.</span>
                <button type="button" onClick={() => void nearMe()}>Use Location</button>
              </div>
            )}

            {visibleRows.length ? (
              <div className="customer-promotion-feed" aria-label="Active promotions">
                {visibleRows.map((row) => <PromotionCard key={row.relationshipId} row={row} />)}
              </div>
            ) : (
              <div className="customer-discover-empty compact-empty">
                <NavIcon name="discover" />
                <strong>No active promotions found.</strong>
                <span>Try a different search or filter.</span>
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
            <ShopperProfileCard profile={profile} />
          </>
        )}
      </section>
    </AccountChrome>
  )
}

function PromotionCard({row, compact = false}: {row: AdvertisingRow; compact?: boolean}) {
  const address = businessAddress(row)
  const direction = directionsUrl(row)
  return (
    <article className={`customer-promotion-card${compact ? ' is-compact' : ''}`}>
      <div className="customer-promotion-business">
        <div>
          <p className="customer-card-kicker">Live promotion</p>
          <h3>{row.businessName}</h3>
        </div>
        {row.daysLeft >= 0 && <span className="customer-days-left">{row.daysLeft} days left</span>}
      </div>
      <div className="customer-business-meta">
        {businessTypeLabel(row.businessType) && <span>{businessTypeLabel(row.businessType)}</span>}
        {address && <span>{address}</span>}
        {typeof row.distanceKm === 'number' && <strong><NavIcon name="mapPin" size={16} /> {row.distanceKm.toFixed(1)} km away</strong>}
      </div>
      <div className="customer-promoted-by">
        <ProfileAvatar name={row.creatorName} photoUrl={row.creatorProfileImageUrl} />
        <div>
          <span>Promoted by</span>
          <strong>{row.creatorName}</strong>
          <small className="customer-creator-id"><span>Creator ID</span>{' '}<b>{row.creatorCode}</b></small>
        </div>
      </div>
      <div className="customer-promotion-actions">
        {row.promotionVideoUrl && row.promotionVideoStatus === 'Live' && (
          <a className="customer-watch-promotion" href={row.promotionVideoUrl} target="_blank" rel="noopener noreferrer">
            <NavIcon name="video" />
            Watch Promotion
          </a>
        )}
        {direction && (
          <a className="customer-get-directions" href={direction} target="_blank" rel="noopener noreferrer">
            <NavIcon name="mapPin" />
            Get Directions
          </a>
        )}
      </div>
    </article>
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
            const busy = processing === x.id
            return (
              <article key={x.id}>
                <div className="confirmation-details">
                  <span>Business</span>
                  <strong>{x.merchantName ?? 'Business'}</strong>
                  <span>Amount</span>
                  <strong>{money(x.purchaseAmount)}</strong>
                  <span>Date / Time</span>
                  <strong>{formatDateTime(x.createdAtUtc ?? x.expiresAtUtc)}</strong>
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
            const when = x.resolvedAtUtc ?? x.createdAtUtc ?? x.expiresAtUtc
            return (
              <article className="confirmation-row" key={x.id}>
                <strong data-label="Business">{x.merchantName ?? 'Business'}</strong>
                <span data-label="Amount">{money(x.purchaseAmount)}</span>
                <span data-label="Status">{x.status === 'Completed' ? 'Completed' : 'Rejected'}</span>
                <span data-label="Date">{formatDate(when)}</span>
                <span data-label="Time">{formatTime(when)}</span>
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
  return (
    <div className="summary-grid">
      <article className="summary-card">
        <span>Available Cashback</span>
        <strong>{money(wallet?.availableCashback, wallet?.currencyCode)}</strong>
      </article>
      <article className="summary-card">
        <span>Next Payout Date</span>
        <strong>{formatDate(wallet?.nextPayoutAtUtc)}</strong>
      </article>
    </div>
  )
}

function ShopperProfileCard({profile}: {profile?: ShopperProfile}) {
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
    </section>
  )
}

export const shopperLabels = {
  en: ['Home', 'Discover Promotions', 'Cashback', 'Search business or creator', 'Creator ID', 'No active promotions found.', 'Available Cashback', 'Next Payout Date'],
}
