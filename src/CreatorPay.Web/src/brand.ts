export const brand = {
  productName: 'Weymela',
  shortName: 'Weymela',
  tagline: 'ወይ መላ! — The smarter way to shop, promote, and earn.',
  supportEmail: 'support@example.com',
  currency: 'ETB',
  defaultLocale: 'en',
  supportedLocales: ['en', 'am'] as const,
  logoText: 'ወይ መላ!',
} as const

export type Locale = typeof brand.supportedLocales[number]
