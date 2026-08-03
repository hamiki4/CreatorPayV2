import { StrictMode, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import './styles.css'

type CreatorProfile = {
  publicCreatorId: string
  firstName: string
  lastName: string
  displayName: string
  email: string
  isEmailVerified: boolean
  isPhoneVerified: boolean
  creatorStatus: string
  nextStep: string
}

function Dashboard() {
  const [profile, setProfile] = useState<CreatorProfile | null>(null)
  const [message, setMessage] = useState('Sign in to load your creator onboarding status.')

  useEffect(() => {
    const accessToken = localStorage.getItem('creatorpay_access_token')
    if (!accessToken) return
    fetch('/api/v1/creators/me', { headers: { Authorization: `Bearer ${accessToken}` } })
      .then(async response => {
        if (!response.ok) throw new Error(response.status === 401 ? 'Your session has expired. Sign in again.' : 'Creator status is temporarily unavailable.')
        return response.json() as Promise<CreatorProfile>
      })
      .then(data => { setProfile(data); setMessage(data.nextStep) })
      .catch((error: Error) => setMessage(error.message))
  }, [])

  return (
    <main className="dashboard">
      <header>
        <p className="eyebrow">CreatorPay V2</p>
        <span className="stage">Creator onboarding</span>
        <h1>{profile?.displayName ?? 'Creator dashboard'}</h1>
        {profile && <p className="legal-name">{profile.firstName} {profile.lastName}</p>}
      </header>
      <section className="status-grid" aria-label="Creator onboarding status">
        <article><span>Creator ID</span><strong>{profile?.publicCreatorId ?? '—'}</strong></article>
        <article><span>Email</span><strong>{profile?.email ?? '—'}</strong><em className={profile?.isEmailVerified ? 'complete' : ''}>{profile?.isEmailVerified ? 'Verified' : 'Verification required'}</em></article>
        <article><span>Phone</span><strong>{profile?.isPhoneVerified ? 'Verified' : 'Verification required'}</strong><em className={profile?.isPhoneVerified ? 'complete' : ''}>{profile?.isPhoneVerified ? 'Complete' : 'Action needed'}</em></article>
        <article><span>Platform approval</span><strong>{profile?.creatorStatus ?? 'Unavailable'}</strong></article>
      </section>
      <aside><span>Next step</span><p>{message}</p></aside>
    </main>
  )
}

createRoot(document.getElementById('root')!).render(<StrictMode><Dashboard /></StrictMode>)
