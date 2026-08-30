const amountFormatter = new Intl.NumberFormat('en-ET', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  hour: 'numeric',
  minute: '2-digit',
  hour12: true,
})

export const formatAmount = (value?: number) => amountFormatter.format(value ?? 0)

export const formatDate = (value?: string | Date | null) =>
  value ? dateFormatter.format(value instanceof Date ? value : new Date(value)) : '—'

export const formatTime = (value?: string | Date | null) =>
  value ? timeFormatter.format(value instanceof Date ? value : new Date(value)) : '—'

export const formatDateTime = (value?: string | Date | null) =>
  value ? `${formatDate(value)} · ${formatTime(value)}` : '—'
