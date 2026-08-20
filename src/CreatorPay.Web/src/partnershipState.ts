export type PartnershipHistoryItem = {
  requestedAtUtc: string
  merchantId?: string
  creatorId?: string
}

export function currentPartnerships<T extends PartnershipHistoryItem>(
  items: readonly T[],
  keyOf: (item: T) => string | undefined | null,
): T[] {
  const current = new Map<string, { item: T; requestedAt: number }>()
  for (const item of items) {
    const key = keyOf(item)
    if (!key) continue
    const requestedAt = Date.parse(item.requestedAtUtc)
    const existing = current.get(key)
    if (!existing || requestedAt > existing.requestedAt) current.set(key, { item, requestedAt })
  }
  return [...current.values()].sort((a, b) => b.requestedAt - a.requestedAt).map(x => x.item)
}
