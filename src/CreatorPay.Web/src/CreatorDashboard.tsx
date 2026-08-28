import { useEffect, useRef, useState } from 'react'
import { api, statusLabel } from './apiClient'
import { ActiveAds, AdvertisingRequest, CreatorConfirmedSales, CreatorRequests, FindBusinesses } from './CreatorAdvertising'
import { AccountChrome } from './AccountChrome'
import { onActionableRefresh } from './actionableRefresh'
import { creatorPhotoUrl, ProfileAvatar } from './profileMedia'
import { prepareProfilePhoto, profilePhotoAccept, profilePhotoProcessingMessage, profilePhotoUnsupportedMessage } from './photoUpload'
import { NavIcon } from './navIcons'

type Tab = 'home' | 'find' | 'ads' | 'requests' | 'sales' | 'payout' | 'profile'
type Profile = {
  displayName: string
  publicCreatorId: string
  creatorCode: string
  creatorStatus: string
  accountStatus: string
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
              <span className="status-badge">{statusLabel(profile.creatorStatus)}</span>
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
  const date = (value?: string) => (value ? new Intl.DateTimeFormat('en-GB').format(new Date(value)) : '—')
  const photo = creatorPhotoUrl(profile?.publicCreatorId, profile?.profileImage?.fileName)
  const tabs: [Tab, string][] = [
    ['find', 'Find Businesses'],
    ['ads', 'Active Ads'],
    ['requests', 'Requests'],
    ['sales', 'Confirmed Sales'],
    ['payout', 'Payout'],
  ]
  const headerPhoto = creatorPhotoUrl(profile?.publicCreatorId, profile?.profileImage?.fileName)
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

  return (
    <AccountChrome
      role="Creator"
      name={profile?.displayName}
      status={profile ? statusLabel(profile.creatorStatus) : 'Active'}
      photoUrl={photo}
      identityMedia={profile ? <ProfileAvatar name={profile.displayName} photoUrl={headerPhoto} style={{ width: '2.5rem', height: '2.5rem', fontSize: '1rem' }} /> : undefined}
      onProfile={() => setTab('profile')}
      onHelp={() => location.assign('/help')}
      onSignOut={onSignOut}
      onNavigate={navigate}
    >
      <div className="creator-dashboard">
        <nav className="creator-tabs" aria-label="Creator sections">
          {tabs.map(([value, label]) => (
            <button
              key={value}
              className={tab === value ? 'active' : ''}
              data-mobile-hidden={value === 'sales' ? 'true' : undefined}
              aria-current={tab === value ? 'page' : undefined}
              onClick={() => setTab(value)}
            >
              <NavIcon name={value === 'find' ? 'find' : value === 'ads' ? 'ads' : value === 'requests' ? 'requests' : value === 'sales' ? 'sales' : 'payout'} />
              <span>{label}</span>
            </button>
          ))}
        </nav>
        {tab === 'home' && (
          <>
            <div className="creator-summary compact-role-summary">
              <article>
                <span>Pending Requests</span>
                <strong>{pending}</strong>
              </article>
              <article className="summary-action-card" role="button" tabIndex={0} onClick={() => setTab('sales')} onKeyDown={(event) => event.key === 'Enter' && setTab('sales')}>
                <span>Confirmed Sales</span>
                <strong>{requests.filter((x) => x.status === 'Approved').length}</strong>
                <small>View sales</small>
              </article>
              <article>
                <span>Payout Amount</span>
                <strong>{money(earnings?.currentPayoutAmount ?? 0, earnings?.currencyCode)}</strong>
              </article>
              <article>
                <span>Next Payout Date</span>
                <strong>{date(earnings?.nextEstimatedPayoutAtUtc)}</strong>
              </article>
            </div>
            {relationshipsError && <p className="friendly-error">{relationshipsError}</p>}
            {earningsError && <p className="friendly-error">{earningsError}</p>}
            <div className="creator-start">
              <h2>Start advertising</h2>
              <p>Find a Business, request permission to promote, and start earning money!</p>
              <button onClick={() => setTab('find')}>Find Businesses</button>
            </div>
          </>
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
