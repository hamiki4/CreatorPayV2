import { useEffect, useRef, useState } from 'react'
import { api } from './apiClient'
import { ActiveAds, AdvertisingRequest, CreatorConfirmedSales, CreatorRequests, FindBusinesses } from './CreatorAdvertising'
import { AccountChrome, AccountStatusBadge } from './AccountChrome'
import { onActionableRefresh } from './actionableRefresh'
import { creatorPhotoUrl, ProfileAvatar } from './profileMedia'
import { prepareProfilePhoto, profilePhotoAccept, profilePhotoProcessingMessage, profilePhotoUnsupportedMessage } from './photoUpload'
import { RoleNavigation } from './RoleNavigation'
import { currentPartnerships } from './partnershipState'
import { daysLeftText, relationshipState } from './relationshipTime'

type Tab = 'home' | 'find' | 'ads' | 'requests' | 'sales' | 'payout' | 'profile'
type Profile = {
  displayName: string
  publicCreatorId: string
  creatorCode: string
  creatorStatus: string
  accountStatus: string
  effectiveStatus: string
  effectiveStatusReason: string
  email: string
  phoneNumber: string
  city: string
  biography?: string
  contentCategories?: string
  profileImage?: { fileName: string; contentType: string; sizeBytes: number }
}
type Earnings = {
  currencyCode: string
  pendingBalance: number
  availableBalance: number
  heldBalance: number
  scheduledBalance: number
  currentPayoutAmount: number
  currentPeriodConfirmedSales: number
  nextEstimatedPayoutAtUtc?: string
  lastPayoutAtUtc?: string
}

const money = (value: number, currency = 'ETB') =>
  new Intl.NumberFormat('en-ET', { style: 'currency', currency }).format(value)

function ProfilePanel({
  profile,
  error,
  refresh,
  onProfileChange,
}: {
  profile?: Profile
  error: string
  refresh: () => Promise<Profile | undefined>
  onProfileChange: (profile: Profile) => void
}) {
  const [copy, setCopy] = useState('')
  const [busy, setBusy] = useState(false)
  const [photoMessage, setPhotoMessage] = useState('')
  const [photoState, setPhotoState] = useState<'idle' | 'success' | 'error'>('idle')
  const input = useRef<HTMLInputElement>(null)

  if (error && !profile) {
    return (
      <section className="creator-section">
        <h2>Profile</h2>
        <p className="friendly-error">{error}</p>
      </section>
    )
  }
  if (!profile) return <p>Loading…</p>

  const photo = creatorPhotoUrl(profile.publicCreatorId, profile.profileImage?.fileName)

  async function submit(file?: File) {
    if (!file) return
    setBusy(true)
    setPhotoMessage('')
    setPhotoState('idle')
    try {
      const prepared = await prepareProfilePhoto(file)
      const form = new FormData()
      form.append('photo', prepared)
      const updated = await api<Profile>('/api/v1/creators/me/profile-photo', { method: 'POST', body: form })
      onProfileChange(updated)
      const confirmed = (await refresh()) ?? updated
      if (!confirmed.profileImage) throw new Error(profilePhotoProcessingMessage)
      onProfileChange(confirmed)
      setPhotoState('success')
      setPhotoMessage('Profile photo updated.')
    } catch (error) {
      console.error(error)
      setPhotoState('error')
      const message = (error as Error).message
      setPhotoMessage(message === profilePhotoUnsupportedMessage || message === profilePhotoProcessingMessage ? message : profilePhotoProcessingMessage)
    } finally {
      setBusy(false)
      if (input.current) input.current.value = ''
    }
  }

  async function remove() {
    if (!confirm('Remove your profile photo?')) return
    setBusy(true)
    setPhotoMessage('')
    setPhotoState('idle')
    try {
      const updated = await api<Profile>('/api/v1/creators/me/profile-photo', { method: 'DELETE' })
      onProfileChange(updated)
      const confirmed = (await refresh()) ?? updated
      if (confirmed.profileImage) throw new Error(profilePhotoProcessingMessage)
      onProfileChange(confirmed)
      setPhotoState('success')
      setPhotoMessage('Profile photo removed.')
    } catch (error) {
      console.error(error)
      setPhotoState('error')
      const message = (error as Error).message
      setPhotoMessage(message === profilePhotoUnsupportedMessage || message === profilePhotoProcessingMessage ? message : profilePhotoProcessingMessage)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="creator-section">
      <h2>Profile</h2>
      {photoMessage && (
        <p className={photoState === 'success' ? 'success-note' : 'friendly-error'} role={photoState === 'error' ? 'alert' : 'status'}>
          {photoMessage}
        </p>
      )}
      <div className="compact-panel profile-details">
        <div className="creator-profile-card">
          <div className="creator-heading">
            <ProfileAvatar name={profile.displayName} photoUrl={photo} className="creator-profile-avatar" />
            <div>
              <strong>{profile.displayName}</strong>
              <p>
                {profile.email || 'No email provided'}
                <br />
                {profile.phoneNumber}
                <br />
                {profile.city}
              </p>
              <AccountStatusBadge status={profile.effectiveStatus} />
            </div>
          </div>
          <div className="actions creator-photo-actions">
            <input
              ref={input}
              type="file"
              accept={profilePhotoAccept}
              className="sr-only"
              onChange={(e) => void submit(e.target.files?.[0])}
            />
            <button type="button" disabled={busy} onClick={() => input.current?.click()}>
              {profile.profileImage ? 'Change Photo' : 'Upload Photo'}
            </button>
            {profile.profileImage && (
              <button type="button" className="quiet" disabled={busy} onClick={() => void remove()}>
                Remove Photo
              </button>
            )}
          </div>
        </div>
        <div className="creator-id-profile">
          <span>Creator ID</span>
          <strong>{profile.creatorCode}</strong>
          <small>{profile.publicCreatorId}</small>
          <button
            className="quiet copy-creator-id"
            onClick={async () => {
              await navigator.clipboard.writeText(profile.creatorCode)
              setCopy('Copied')
            }}
          >
            Copy Creator ID
          </button>
          {copy && <small role="status">{copy}</small>}
        </div>
      </div>
    </section>
  )
}

export function CreatorDashboard({ onSignOut }: { onSignOut: () => void }) {
  const [tab, setTab] = useState<Tab>('home')
  const [profile, setProfile] = useState<Profile>()
  const [profileError, setProfileError] = useState('')
  const [earnings, setEarnings] = useState<Earnings>()
  const [requests, setRequests] = useState<AdvertisingRequest[]>([])
  const [earningsError, setEarningsError] = useState('')
  const [relationshipsError, setRelationshipsError] = useState('')
  const [loading, setLoading] = useState(true)

  const loadProfile = async () =>
    api<Profile>('/api/v1/creators/me')
      .then((value) => {
        setProfile(value)
        setProfileError('')
        return value
      })
      .catch((error) => {
        console.error(error)
        setProfileError((error as Error).message)
        return undefined
      })

  const loadSupporting = () =>
    Promise.allSettled([
      api<Earnings>('/api/v1/creator/earnings/summary'),
      api<AdvertisingRequest[]>('/api/v1/creator/partnerships'),
    ]).then((results) => {
      const [e, a] = results
      if (e.status === 'fulfilled') {
        setEarnings(e.value)
        setEarningsError('')
      } else {
        setEarningsError("We couldn't load payout information right now.")
      }
      if (a.status === 'fulfilled') {
        setRequests(a.value)
        setRelationshipsError('')
      } else {
        setRelationshipsError("We couldn't load advertising relationships right now.")
      }
    })

  const load = () => Promise.all([loadProfile(), loadSupporting()]).finally(() => setLoading(false))

  useEffect(() => {
    let current = true
    setLoading(true)
    void load().then(() => {
      if (!current) return
    })
    const unsubscribe = onActionableRefresh(() => {
      setLoading(true)
      void load()
    })
    return () => {
      current = false
      unsubscribe()
    }
  }, [])

  const pending = requests.filter((x) => x.status === 'Pending').length
  const activeAds = currentPartnerships(requests, (request) => request.merchantId)
    .filter((request) => request.status === 'Approved' && relationshipState(request).label === 'Active')
    .slice(0, 3)
  const date = (value?: string) => (value ? new Intl.DateTimeFormat('en-GB').format(new Date(value)) : '—')
  const photo = creatorPhotoUrl(profile?.publicCreatorId, profile?.profileImage?.fileName)
  const navigate = (target: string) =>
    setTab(
      target.includes('payout')
        ? 'payout'
        : target.includes('sales')
          ? 'sales'
          : target.includes('requests')
            ? 'requests'
            : target.includes('ads')
              ? 'ads'
              : target.includes('profile')
                ? 'profile'
                : target.includes('find')
                  ? 'find'
                  : 'home',
    )
  const selectedTab: Tab = tab === 'sales' ? 'home' : tab

  return (
    <AccountChrome
      role="Creator"
      name={profile?.displayName}
      status={profile?.effectiveStatus}
      photoUrl={photo}
      onProfile={() => setTab('profile')}
      onHelp={() => location.assign('/help')}
      onSignOut={onSignOut}
      onNavigate={navigate}
    >
      <div className="creator-dashboard">
        <RoleNavigation
          role="Creator"
          label="Creator sections"
          items={[
            {id: 'home', label: 'Home', icon: 'home', active: selectedTab === 'home', onSelect: () => setTab('home')},
            {id: 'find', label: 'Find Businesses', icon: 'find', active: selectedTab === 'find', onSelect: () => setTab('find')},
            {id: 'ads', label: 'Active Ads', icon: 'ads', active: selectedTab === 'ads', onSelect: () => setTab('ads')},
            {id: 'requests', label: 'Requests', icon: 'requests', active: selectedTab === 'requests', onSelect: () => setTab('requests')},
            {id: 'payout', label: 'Payout', icon: 'payout', active: selectedTab === 'payout', onSelect: () => setTab('payout')},
            {id: 'profile', label: 'Profile', icon: 'profile', active: selectedTab === 'profile', onSelect: () => setTab('profile')},
          ]}
        />
        {tab === 'home' && (
          <section className="creator-home" aria-labelledby="creator-home-title">
            <h2 id="creator-home-title" className="creator-home-title">Dashboard</h2>
            <div className="creator-home-summary">
              <button type="button" className="creator-home-stat creator-home-stat--accent" onClick={() => setTab('requests')}>
                <span>Pending Requests</span>
                <strong>{pending}</strong>
              </button>
              <button type="button" className="creator-home-stat creator-home-stat--accent" onClick={() => setTab('sales')}>
                <span>Confirmed Sales</span>
                <strong>{earnings?.currentPeriodConfirmedSales ?? 0}</strong>
              </button>
              <button type="button" className="creator-home-stat creator-home-stat--accent" onClick={() => setTab('payout')}>
                <span>Payout Amount</span>
                <strong>{money(earnings?.currentPayoutAmount ?? 0, earnings?.currencyCode)}</strong>
              </button>
            </div>
            <button type="button" className="creator-next-payout" onClick={() => setTab('payout')}>
              <span>
                <span>Next Payout Date</span>
                <strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong>
              </span>
              <span aria-hidden="true">›</span>
            </button>
            {relationshipsError && <p className="friendly-error">{relationshipsError}</p>}
            {earningsError && <p className="friendly-error">{earningsError}</p>}
            <button type="button" className="creator-start-card" onClick={() => setTab('find')}>
              <span className="creator-start-copy">
                <strong>Start advertising</strong>
                <span>Find businesses, request permission to promote, and start earning money!</span>
              </span>
              <span className="creator-start-action">
                <span>Find Businesses</span>
                <span aria-hidden="true">›</span>
              </span>
            </button>
            <div className="creator-recent-ads">
              <button type="button" className="creator-recent-heading" onClick={() => setTab('ads')}>
                <strong>Recent Active Ads</strong>
                <span>{activeAds.length} <span aria-hidden="true">›</span></span>
              </button>
              {activeAds.length === 0 ? (
                <p className="compact-empty">No active ads yet.</p>
              ) : (
                <div className="creator-recent-list">
                  {activeAds.map((ad) => {
                    const state = relationshipState(ad)
                    const days = state.daysLeft === null ? '—' : daysLeftText(state.daysLeft, state.tone)
                    const promoState = ad.promotionVideo
                      ? ad.promotionVideo.status === 'Approved' ? 'Promo video approved' : `Promo video ${ad.promotionVideo.status.toLowerCase()}`
                      : 'Add Promo Video'
                    return (
                      <button type="button" className="creator-recent-ad" key={ad.id} onClick={() => setTab('ads')}>
                        <span className="creator-recent-ad-main">
                          <strong>{ad.merchantName}</strong>
                          <span>Activated {date(ad.activatedAtUtc)}</span>
                          <span>{promoState}</span>
                        </span>
                        <span className="creator-recent-ad-state">
                          <span className="status-badge">Active</span>
                          <span>{days}</span>
                        </span>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>
          </section>
        )}
        {tab === 'find' && <><FindBusinesses onRequested={() => void load()} />{relationshipsError && <p className="friendly-error">{relationshipsError}</p>}</>}
        {tab === 'ads' && <><ActiveAds items={requests} loading={loading} refresh={() => void load()} />{relationshipsError && <p className="friendly-error">{relationshipsError}</p>}</>}
        {tab === 'requests' && <><CreatorRequests items={requests} refresh={() => void load()} />{relationshipsError && <p className="friendly-error">{relationshipsError}</p>}</>}
        {tab === 'sales' && <><CreatorConfirmedSales />{earningsError && <p className="friendly-error">{earningsError}</p>}</>}
        {tab === 'payout' && (
          <section className="creator-section">
            <h2>Payout</h2>
            <div className="summary-grid">
              <article className="summary-card">
                <span>Payout Amount</span>
                <strong>{money(earnings?.currentPayoutAmount ?? 0, earnings?.currencyCode)}</strong>
              </article>
              <article className="summary-card">
                <span>Next Payout Date</span>
                <strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong>
              </article>
            </div>
            {earningsError && <p className="friendly-error">{earningsError}</p>}
          </section>
        )}
        {tab === 'profile' && (
          <ProfilePanel
            profile={profile}
            error={!profile ? profileError : ''}
            refresh={async () => loadProfile()}
            onProfileChange={setProfile}
          />
        )}
      </div>
    </AccountChrome>
  )
}
