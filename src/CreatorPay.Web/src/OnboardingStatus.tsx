import { useEffect, useState } from 'react'
import { AccountStatusBadge } from './AccountChrome'
import { getExternalSession } from './externalSession'
import { getAccessToken } from './sessionStore'

type Status = {
  accountStatus: string
  effectiveStatus?: string
  effectiveStatusReason?: string
  creatorStatus?: string
  merchantStatus?: string
  nextStep: string
  displayName?: string
  tradingName?: string
}

const base = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

export function OnboardingStatus({
  role,
}: {
  role: 'Customer' | 'Creator' | 'MerchantAdmin'
}) {
  const [data, setData] = useState<Status>()
  const [error, setError] = useState('')

  const path =
    role === 'Creator'
      ? '/api/v1/creators/me'
      : role === 'MerchantAdmin'
        ? '/api/v1/merchants/me'
        : '/api/v1/auth/me'

  useEffect(() => {
    fetch(`${base}${path}`, {
      credentials: 'include',
      headers: {
        Authorization: `Bearer ${getAccessToken() ?? ''}`,
      },
    })
      .then(async response => {
        const body = await response.json().catch(() => ({}))

        if (!response.ok && (response.status === 401 || response.status === 403) && getExternalSession()?.isOnboarding) {
          location.replace('/onboarding')
          return null
        }

        if (!response.ok) {
          throw Error(body.detail ?? `Request failed (${response.status})`)
        }

        return body as Status
      })
      .then(body => { if (body) setData(body) })
      .catch(error => setError(error.message))
  }, [path])

  if (error) {
    return (
      <section className="panel" role="alert">
        <h2>Onboarding status</h2>
        <p className="error">{error}</p>
      </section>
    )
  }

  if (!data) {
    return <p>Loading onboarding status...</p>
  }

  const status =
    data.effectiveStatus ??
    data.creatorStatus ??
    data.merchantStatus ??
    data.accountStatus

  const displayName =
    data.displayName ??
    data.tradingName ??
    role

  const message =
    role === 'Creator'
      ? data.creatorStatus === 'CorrectionRequested'
        ? 'Review the requested corrections and update your Creator profile.'
        : data.creatorStatus === 'Rejected'
          ? data.nextStep
          : 'Your creator account is under review. You will be able to access the approved creator tools after activation.'
      : data.merchantStatus === 'CorrectionRequested' || data.merchantStatus === 'Rejected'
        ? data.nextStep
        : data.accountStatus === 'PendingApproval'
        ? 'Your account is under review. You will be notified after the Platform Admin approves your account.'
        : data.nextStep

  return (
    <section className="panel" aria-labelledby="onboarding-status">
      <p className="eyebrow">Account onboarding</p>

      <h2 id="onboarding-status">{displayName}</h2>

      <AccountStatusBadge status={status} />

      <p>{message}</p>
    </section>
  )
}
