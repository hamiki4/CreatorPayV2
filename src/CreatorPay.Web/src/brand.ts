export const brand = {
  productName: 'Weymela',
  shortName: 'Weymela',
  tagline: 'The smarter way to shop, promote, and earn.',
  supportEmail: 'support@weymela.com',
  currency: 'ETB',
  defaultLocale: 'en',
  supportedLocales: ['en'] as const,
  logoText: 'Weymela',
} as const

export type Locale = typeof brand.supportedLocales[number]
