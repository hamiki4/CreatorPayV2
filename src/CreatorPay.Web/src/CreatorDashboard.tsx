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
import { formatAmount, formatDate } from './displayFormat'
import { NavIcon } from './navIcons'
import { getExternalSession, isExternalSession } from './externalSession'

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
  socialProfiles?: { platform: SocialPlatform; profileUrl?: string; followerCount: number; verificationStatus: string; isPrimary: boolean }[]
  profileImage?: { fileName: string; contentType: string; sizeBytes: number }
}
type SocialPlatform = 'TikTok' | 'Instagram' | 'YouTube' | 'Facebook'
type SocialDraft = { platform: SocialPlatform; profileUrl: string; audienceCount: string }
const socialPlatforms: SocialPlatform[] = ['TikTok', 'Instagram', 'YouTube', 'Facebook']
type Earnings = {
  currencyCode: string
  pendingBalance: number
  availableBalance: number
  heldBalance: number
  scheduledBalance: number
  currentPayoutAmount: number
  currentPeriodConfirmedSales: number
  confirmedSalesCount: number
  upcomingPayoutAmount: number
  nextEstimatedPayoutAtUtc?: string
  lastPayoutAtUtc?: string
}
type CreatorPayout = {
  id: string
  publicPayoutId: string
  amount: number
  status: string
  scheduledAtUtc: string
  paidAtUtc?: string
}

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
  const [socialDrafts, setSocialDrafts] = useState<SocialDraft[]>([])
  const [socialMessage, setSocialMessage] = useState('')
  const [socialState, setSocialState] = useState<'idle' | 'success' | 'error'>('idle')
  const input = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (!profile) return
    setSocialDrafts((profile.socialProfiles ?? []).filter(row => socialPlatforms.includes(row.platform))
      .map(row => ({ platform: row.platform, profileUrl: row.profileUrl ?? '', audienceCount: String(row.followerCount) })))
  }, [profile])

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

  function addSocial() {
    const platform = socialPlatforms.find(item => !socialDrafts.some(row => row.platform === item))
    if (platform) setSocialDrafts([...socialDrafts, { platform, profileUrl: '', audienceCount: '' }])
  }

  function updateSocial(index: number, value: Partial<SocialDraft>) {
    setSocialDrafts(socialDrafts.map((row, rowIndex) => rowIndex === index ? { ...row, ...value } : row))
  }

  async function saveSocials() {
    setBusy(true); setSocialMessage(''); setSocialState('idle')
    try {
      if (socialDrafts.length === 0) throw new Error('Keep at least one social profile.')
      const socialProfiles = socialDrafts.map(row => {
        const audienceCount = Number(row.audienceCount)
        if (!row.profileUrl.trim() || row.audienceCount === '' || !Number.isSafeInteger(audienceCount) || audienceCount < 0)
          throw new Error(`Complete a valid URL and audience count for ${row.platform}.`)
        return { platform: row.platform, profileUrl: row.profileUrl.trim(), audienceCount }
      })
      await api('/api/v1/integration/v3/creator/social-profiles', {
        method: 'PUT', body: JSON.stringify({ socialProfiles }),
      })
      const confirmed = await refresh()
      if (confirmed) onProfileChange(confirmed)
      setSocialState('success'); setSocialMessage('Social profiles updated.')
    } catch (error) {
      setSocialState('error'); setSocialMessage((error as Error).message)
    } finally { setBusy(false) }
  }

  const account = getExternalSession()
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
                <small>Weymela account email</small><br />
                {account?.accountEmail || profile.email || 'Unavailable'}
                <br />
                <small>Weymela account phone</small><br />
                {account?.accountPhone || profile.phoneNumber || 'Unavailable'}
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
          <button
            className="quiet copy-creator-id"
            onClick={async () => {
              await navigator.clipboard.writeText(profile.creatorCode)
              setCopy('Copied')
            }}
          >
            <NavIcon name="copy" size={18} />
            Copy Creator ID
          </button>
          {copy && <small role="status">{copy}</small>}
        </div>
      </div>
      {isExternalSession() && <div className="compact-panel creator-social-editor">
        <h3>Social profiles</h3>
        {socialMessage && <p className={socialState === 'success' ? 'success-note' : 'friendly-error'} role={socialState === 'error' ? 'alert' : 'status'}>{socialMessage}</p>}
        <div className="social-profile-list">{socialDrafts.map((row, index) => <div className="social-profile-row" key={row.platform}>
          <label>Platform<select value={row.platform} onChange={event => updateSocial(index, { platform: event.target.value as SocialPlatform })}>{socialPlatforms.map(platform => <option key={platform} value={platform} disabled={socialDrafts.some((current, currentIndex) => currentIndex !== index && current.platform === platform)}>{platform}</option>)}</select></label>
          <label>{row.platform === 'YouTube' ? 'Channel URL' : row.platform === 'Facebook' ? 'Profile/Page URL' : 'Profile URL'}<input type="url" required value={row.profileUrl} onChange={event => updateSocial(index, { profileUrl: event.target.value })} /></label>
          <label>{row.platform === 'YouTube' ? 'Subscriber count' : 'Follower count'}<input type="number" min="0" step="1" required value={row.audienceCount} onChange={event => updateSocial(index, { audienceCount: event.target.value })} /></label>
          <button type="button" className="quiet remove-social" disabled={busy || socialDrafts.length === 1} onClick={() => setSocialDrafts(socialDrafts.filter((_, rowIndex) => rowIndex !== index))}>Remove {row.platform}</button>
        </div>)}</div>
        <div className="actions creator-social-actions"><button type="button" disabled={busy || socialDrafts.length === socialPlatforms.length} className="quiet" onClick={addSocial}>Add social platform</button><button type="button" disabled={busy || socialDrafts.length === 0} onClick={() => void saveSocials()}>{busy ? 'Saving…' : 'Save social profiles'}</button></div>
      </div>}
    </section>
  )
}

export function CreatorDashboard({ onSignOut }: { onSignOut: () => void }) {
  const [tab, setTab] = useState<Tab>('home')
  const [profile, setProfile] = useState<Profile>()
  const [profileError, setProfileError] = useState('')
  const [earnings, setEarnings] = useState<Earnings>()
  const [payouts, setPayouts] = useState<CreatorPayout[]>([])
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
      api<CreatorPayout[]>('/api/v1/creator/payouts'),
      api<AdvertisingRequest[]>('/api/v1/creator/partnerships'),
    ]).then((results) => {
      const [e, p, a] = results
      if (e.status === 'fulfilled') {
        setEarnings(e.value)
        setEarningsError('')
      } else {
        setEarningsError("We couldn't load payout information right now.")
      }
      if (p.status === 'fulfilled') {
        setPayouts(p.value)
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

  const currentRequests = currentPartnerships(requests, (request) => request.merchantId)
  const pending = currentRequests.filter((request) => request.status === 'Pending').length
  const activeAds = currentRequests
    .filter((request) => request.status === 'Approved' && relationshipState(request).label === 'Active')
    .slice(0, 3)
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
      onHelp={() => location.assign('/help?category=Content%20Creators')}
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
                <strong>{earnings?.confirmedSalesCount ?? 0}</strong>
              </button>
              <button type="button" className="creator-home-stat creator-home-stat--accent" onClick={() => setTab('payout')}>
                <span>Upcoming Payout</span>
                <strong>{formatAmount(earnings?.upcomingPayoutAmount)}</strong>
              </button>
            </div>
            <button type="button" className="creator-next-payout" onClick={() => setTab('payout')}>
              <span>
                <span>Next Payout Date</span>
                <strong>{formatDate(earnings?.nextEstimatedPayoutAtUtc)}</strong>
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
                          <span>Activated {formatDate(ad.activatedAtUtc)}</span>
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
            <h2>Payout Overview</h2>
            <div className="summary-grid creator-payout-overview">
              <article className="summary-card">
                <span>Upcoming Payout</span>
                <strong>{formatAmount(earnings?.upcomingPayoutAmount)}</strong>
              </article>
              <article className="summary-card">
                <span>Next Payout Date</span>
                <strong>{formatDate(earnings?.nextEstimatedPayoutAtUtc)}</strong>
              </article>
            </div>
            <div className="creator-payout-history">
              <h3>Payout History</h3>
              {payouts.length === 0 ? (
                <p className="compact-empty">No payout history yet.</p>
              ) : (
                <div className="creator-payout-history-list">
                  {payouts.map((payout) => (
                    <article className="creator-payout-history-item" key={payout.id}>
                      <span>
                        <time dateTime={payout.paidAtUtc ?? payout.scheduledAtUtc}>{formatDate(payout.paidAtUtc ?? payout.scheduledAtUtc)}</time>
                        <small>{payout.publicPayoutId}</small>
                      </span>
                      <strong>{formatAmount(payout.amount)}</strong>
                      <span className={`creator-payout-status creator-payout-status--${payout.status.toLowerCase()}`}>{payout.status}</span>
                    </article>
                  ))}
                </div>
              )}
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
