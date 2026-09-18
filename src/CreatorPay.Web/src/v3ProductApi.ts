export class V3ProductError extends Error {
  constructor(message: string, readonly status: number, readonly code?: string) {
    super(message)
  }
}

export async function v3Request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const profileKey = typeof window === 'undefined' ? null : sessionStorage.getItem('weymela.profile-key')
  const response = await fetch(path, {
    ...init,
    credentials: 'same-origin',
    cache: 'no-store',
    headers: {
      'Content-Type': 'application/json',
      'X-Weymela-Request': '1',
      ...(profileKey ? { 'X-Weymela-Profile': profileKey } : {}),
      ...init.headers,
    },
  })
  if (response.status === 204) return undefined as T
  const body = await response.json().catch(() => ({})) as { message?: string; detail?: string; code?: string }
  if (!response.ok) {
    const message = response.status >= 500
      ? 'This section is temporarily unavailable.'
      : body.message ?? body.detail ?? 'We could not complete that request.'
    throw new V3ProductError(message, response.status, body.code)
  }
  return body as T
}

export function v3Post<T = { id: string }>(path: string, body?: unknown, key = crypto.randomUUID()) {
  return v3Request<T>(path, {
    method: 'POST',
    headers: { 'Idempotency-Key': key, 'X-Weymela-Activity': '1' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

export const amount = (value?: number | null) => new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 2,
}).format(value ?? 0)

export const friendlyStatus = (value: string) => {
  const mapped: Record<string, string> = {
    ViewPlusCommission: 'View & Sale',
    ViewOnly: 'View Only',
    Funded: 'Draft',
    Published: 'Open',
    BudgetExhausted: 'Ended',
    PendingApproval: 'Pending approval',
  }
  return mapped[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
