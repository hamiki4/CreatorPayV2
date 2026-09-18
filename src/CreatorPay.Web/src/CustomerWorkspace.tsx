import {FormEvent, useEffect, useState} from 'react'
import {useTypeahead} from './typeahead'
import {AccountChrome, AccountStatusBadge} from './AccountChrome'
import {onActionableRefresh} from './actionableRefresh'
import {api as request} from './apiClient'
import {ProfileAvatar} from './profileMedia'
import {businessTypes} from './AuthWorkspace'
import {RoleNavigation} from './RoleNavigation'
import {NavIcon} from './navIcons'
import {formatAmount, formatDate, formatDateTime, formatTime} from './displayFormat'
import {createUuid} from './uuid'
import QRCode from 'qrcode'
import {v3Post, v3Request} from './v3ProductApi'

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
type V3Offer = {
  id:string
  campaignId?:string
  source:'VIEW_AND_SALE_PROMOTION'|'UGC_CUSTOMER_OFFER'
  offer:string
  business:{displayName:string;directionsUrl?:string}
  creator?:{displayName:string}
  benefitPercent:number
  watchUrl?:string
  slogan?:string
  location?:string
}
type V3Qr = {id:string;token?:string;expiresAtUtc:string;replayed:boolean}

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

function AuthoritativeOfferCard({offer,onQr}:{offer:V3Offer;onQr:(offer:V3Offer)=>void}){
  return <article className="customer-promotion-card"><div className="customer-promotion-copy"><strong>{offer.business.displayName}</strong><h3>{offer.slogan??offer.offer}</h3>{offer.location&&<p>{offer.location}</p>}<span className="cashback">{formatAmount(offer.benefitPercent)}% {offer.source==='UGC_CUSTOMER_OFFER'?'off':'Customer benefit'}</span></div><div className="actions">{offer.watchUrl&&<a className="quiet" href={offer.watchUrl} target="_blank" rel="noopener noreferrer">Watch Promotion</a>}{offer.business.directionsUrl&&<a className="quiet" href={offer.business.directionsUrl} target="_blank" rel="noopener noreferrer">Get Directions</a>}<button type="button" onClick={()=>onQr(offer)}>Use Offer</button></div></article>
}

export function ShopperOfferPage({code}: {code: string}) {
  void code
  return (
    <main className="auth">
      <section className="panel">
        <p className="eyebrow">Weymela</p>
        <h1>Customer Offers</h1>
        <p>Sign in to see eligible offers and generate a secure checkout QR.</p>
        <a href="/">Continue to Weymela</a>
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
  const [v3Offers, setV3Offers] = useState<V3Offer[]>([])
  const [v3OfferError, setV3OfferError] = useState('')
  const [activeQr, setActiveQr] = useState<{offer: V3Offer; image: string; expiresAtUtc: string}>()
  const [qrBusy, setQrBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [processing, setProcessing] = useState<string>()
  const [view, setView] = useState<'home' | 'discover' | 'confirmations' | 'cashback' | 'profile'>(() =>
    new URLSearchParams(location.search).get('view') === 'confirmations' ? 'confirmations' : 'home',
  )

  const loadAccount = () => {
    v3Request<V3Offer[]>('/api/customer/offers').then(value=>{setV3Offers(value);setV3OfferError('')}).catch(error=>{console.error(error);setV3OfferError("We couldn't load current offers.")})
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
    // The V3 Customer Offer query is authoritative. The legacy advertising
    // endpoint is intentionally not queried because it cannot express the
    // View Only / View & Sale / UGC Customer Offer visibility matrix.
    void term; void selectedBusinessType; void coordinates
    return [] as AdvertisingRow[]
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

  async function prepareOfferQr(offer:V3Offer){if(qrBusy)return;setQrBusy(true);setMessage('');try{const result=await v3Post<V3Qr>(`/api/customer/offers/${offer.id}/qr`,{});if(!result.token)throw new Error('This one-time QR cannot be displayed again.');const image=await QRCode.toDataURL(result.token,{width:280,margin:2,errorCorrectionLevel:'M'});setActiveQr({offer,image,expiresAtUtc:result.expiresAtUtc})}catch(error){setMessage((error as Error).message)}finally{setQrBusy(false)}}

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

            {v3Offers.length>0&&<section className="customer-home-section" aria-labelledby="current-offers-title"><div className="customer-section-heading"><h3 id="current-offers-title">Available Offers</h3><button type="button" className="customer-text-action" onClick={()=>setView('discover')}>See all</button></div><div className="customer-promotion-feed customer-promotion-feed--home">{v3Offers.slice(0,3).map(offer=><AuthoritativeOfferCard key={offer.id} offer={offer} onQr={x=>void prepareOfferQr(x)}/>)}</div></section>}
            {!v3Offers.length && !v3OfferError && (
              <div className="customer-home-empty">
                <NavIcon name="discover" />
                <strong>No offers available right now</strong>
                <span>Eligible View &amp; Sale and Customer Offers will appear here.</span>
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
            {message&&<p className="product-message" role="status">{message}</p>}
            {v3OfferError&&<p className="friendly-error" role="alert">{v3OfferError}</p>}
            {v3Offers.length>0&&<section className="customer-home-section" aria-label="Available Customer Offers"><div className="customer-promotion-feed">{v3Offers.map(offer=><AuthoritativeOfferCard key={offer.id} offer={offer} onQr={x=>void prepareOfferQr(x)}/>)}</div></section>}
            {discoverError && <p className="friendly-error" role="alert">{discoverError}</p>}
            {pendingCount > 0 && (
              <button className="pending-confirmation-link" onClick={() => setView('confirmations')}>
                Purchase confirmations <span>{pendingCount}</span>
              </button>
            )}
            {!v3Offers.length && !v3OfferError && (
              <div className="customer-discover-empty compact-empty">
                <NavIcon name="discover" />
                <strong>No offers available right now.</strong>
                <span>Only eligible Customer-facing offers appear here.</span>
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
        {activeQr&&<div className="modal-backdrop"><section className="help-dialog offer-qr-dialog" role="dialog" aria-modal="true" aria-labelledby="offer-qr-title"><h2 id="offer-qr-title">{activeQr.offer.business.displayName}</h2><p>{activeQr.offer.slogan??activeQr.offer.offer}</p><img className="qr-image" src={activeQr.image} alt="Offer QR for the cashier"/><p>Show this QR at checkout.</p><small>Expires {formatTime(activeQr.expiresAtUtc)}</small><button type="button" onClick={()=>setActiveQr(undefined)}>Close</button></section></div>}
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
  en: ['Home', 'Discover Promotions', 'Cashback', 'Search business or creator', 'No active promotions found.', 'Available Cashback', 'Next Payout Date'],
}
