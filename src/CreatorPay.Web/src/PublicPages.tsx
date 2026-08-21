import { FormEvent, ReactNode, useMemo, useState } from 'react'
import { brand } from './brand'

const apiBase = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

const faqs = [
  ['General', 'What is Weymela?', 'Weymela connects shoppers, content creators, and Businesses through trackable advertising, cashback, and creator earnings.'],
  ['Content Creators', 'How do I advertise for a Business?', 'Open Find Businesses, search for the Business, and send an Advertising Request. After approval, the Business must select Activate Ad.'],
  ['Content Creators', 'When are Creator earnings recorded?', 'Earnings are recorded after a valid shopper-confirmed purchase is successfully posted.'],
  ['Content Creators', 'How do I use My QR?', 'Show or share your Creator QR when promoting an approved Business. Do not alter the QR.'],
  ['Businesses', 'How are Creators approved?', 'The Business reviews Creator advertising requests and approves or rejects them.'],
  ['Shoppers', 'How does a Shopper earn cashback?', 'Use active Creator advertising at a participating Business and confirm the checkout.'],
  ['Account and Security', 'How can suspicious activity be reported?', 'Stop the transaction, retain the public reference, and contact support. Never send passwords, one-time codes, or full QR tokens.'],
] as const

type LegalSection = [string, ReactNode]

const legal = {
  terms: {
    title: 'Terms of Service',
    eyebrow: 'Version 1.0',
    intro: 'These terms govern your use of Weymela.',
    effectiveDate: 'August 6, 2026',
    sections: [
      ['Platform role', 'Weymela connects users and records eligible activity; it is not the seller of a Business’s goods.'],
      ['Acceptable use', 'Do not commit fraud, misrepresent advertising, alter QR codes, scrape private data, or share passwords and one-time codes.'],
      ['Business funding', 'Businesses must maintain sufficient funds for eligible transactions.'],
      ['Records', 'Authenticated transaction and financial ledgers remain the operational record.'],
      ['Contact', 'Questions may be submitted through Contact Support.'],
    ] as LegalSection[],
  },
  privacy: {
    title: 'Privacy Policy',
    eyebrow: 'Version 1.0',
    intro: 'This privacy policy explains how Weymela handles personal data.',
    effectiveDate: 'August 6, 2026',
    sections: [
      ['Information collected', 'We collect registration, profile, contact, support, consent, transaction, and security information needed to operate Weymela.'],
      ['Phone numbers', 'Phone numbers support verification, checkout matching, duplicate-use controls, and account recovery.'],
      ['Data sharing', 'Data is shared only as needed with relevant participants, authorized staff, service providers, or where legally required.'],
      ['Security', 'We use role-based access, masking, protected secrets, audit records, validation, and monitoring.'],
      ['Contact', 'Use Contact Support for privacy and account requests.'],
      ['Account deletion', <>
        To request deletion of your Weymela account, use the{' '}
        <a href="/delete-account">Delete Account</a>{' '}
        page or contact{' '}
        <a href={`mailto:${brand.supportEmail}`}>{brand.supportEmail}</a>{' '}
        from the email address associated with your account when possible.
      </>],
    ] as LegalSection[],
  },
  deleteAccount: {
    title: 'Delete Your Weymela Account',
    eyebrow: 'Weymela',
    intro: 'Request account deletion',
    effectiveDate: undefined,
    sections: [
      ['How to request', <>
        You can request deletion of your Weymela account and associated personal data by contacting Weymela Support at{' '}
        <a href={`mailto:${brand.supportEmail}?subject=Weymela%20Account%20Deletion%20Request`}>{brand.supportEmail}</a>.
      </>],
      ['Use your account email', 'Please contact support from the email address associated with your Weymela account when possible.'],
      ['Verification', 'Weymela may verify account ownership before processing the request.'],
      ['What is deleted', 'After verification, the account and associated personal information will be deleted except for information that Weymela is required or permitted to retain.'],
      ['Records retained', 'Certain transaction, accounting, fraud-prevention, security, dispute, or legal records may be retained when necessary. Retained information will be limited to what is required for those purposes.'],
    ] as LegalSection[],
  },
} as const

const publicNavLinks = [
  ['Help Center', '/help'],
  ['Contact Support', '/contact'],
  ['Terms', '/terms'],
  ['Privacy', '/privacy'],
  ['Delete Account', '/delete-account'],
] as const

export function PublicShell({ children }: { children: ReactNode }) {
  return (
    <main className="public">
      <a className="skip" href="#public-content">
        Skip to content
      </a>
      <header className="public-nav">
        <a href="/" className="brand-link">
          Weymela
        </a>
        <nav aria-label="Public navigation">
          {publicNavLinks.map(([label, href]) => (
            <a key={href} href={href}>
              {label}
            </a>
          ))}
          <a href="/">Sign In</a>
        </nav>
      </header>
      <div id="public-content">{children}</div>
      <footer>
        <small>© Weymela · <a href={`mailto:${brand.supportEmail}`}>{brand.supportEmail}</a></small>
      </footer>
    </main>
  )
}

export function HelpCenter() {
  const [category, setCategory] = useState('All')
  const [search, setSearch] = useState('')
  const categories = ['All', ...Array.from(new Set(faqs.map((x) => x[0])))]
  const shown = useMemo(
    () => faqs.filter((x) => (category === 'All' || x[0] === category) && `${x[1]} ${x[2]}`.toLowerCase().includes(search.toLowerCase())),
    [category, search],
  )
  return (
    <PublicShell>
      <section className="public-hero">
        <p className="eyebrow">Weymela</p>
        <h1>Help Center</h1>
        <p>Clear answers for using Weymela safely.</p>
        <label>
          Search help
          <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
        <div className="category-filter">
          {categories.map((x) => (
            <button className={category === x ? '' : 'quiet'} onClick={() => setCategory(x)} key={x}>
              {x}
            </button>
          ))}
        </div>
      </section>
      <section className="faq-list">
        {shown.map((x) => (
          <details key={x[1]}>
            <summary>{x[1]}</summary>
            <p>{x[2]}</p>
          </details>
        ))}
        {shown.length === 0 && <p>No matching help articles.</p>}
      </section>
      <section className="legal">
        <h2>Delete your account</h2>
        <p>
          You can request deletion of your Weymela account from the{' '}
          <a href="/delete-account">Delete Account</a> page.
        </p>
      </section>
    </PublicShell>
  )
}

export function ContactSupport() {
  const [f, setF] = useState({
    name: '',
    contact: '',
    userType: 'Shopper',
    subject: '',
    message: '',
    preferredLanguage: 'en',
    consentAcknowledged: false,
  })
  const [result, setResult] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setResult('')
    try {
      const r = await fetch(`${apiBase}/api/v1/support/requests`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(f),
      })
      const v = await r.json().catch(() => ({}))
      setResult(r.ok ? `${v.message} Reference: ${v.referenceNumber}` : 'Please check the form and try again.')
    } catch (error) {
      console.error(error)
      setResult('Support is temporarily unavailable.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <PublicShell>
      <form className="panel form support-form" onSubmit={submit}>
        <h1>Contact Support</h1>
        <p>Do not include passwords, one-time codes, full QR tokens, or payment credentials.</p>
        <label>
          Name
          <input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
        </label>
        <label>
          Email or phone
          <input required value={f.contact} onChange={(e) => setF({ ...f, contact: e.target.value })} />
        </label>
        <label>
          User type
          <select value={f.userType} onChange={(e) => setF({ ...f, userType: e.target.value })}>
            {['Shopper', 'Creator', 'Business', 'Cashier/Supervisor', 'Other'].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </label>
        <label className="full">
          Subject
          <input required value={f.subject} onChange={(e) => setF({ ...f, subject: e.target.value })} />
        </label>
        <label className="full">
          Message
          <textarea required minLength={10} value={f.message} onChange={(e) => setF({ ...f, message: e.target.value })} />
        </label>
        <label className="check full">
          <input
            type="checkbox"
            required
            checked={f.consentAcknowledged}
            onChange={(e) => setF({ ...f, consentAcknowledged: e.target.checked })}
          />
          I consent to Weymela using this information to respond.
        </label>
        <button disabled={busy}>{busy ? 'Sending…' : 'Send request'}</button>
        {result && <aside>{result}</aside>}
      </form>
    </PublicShell>
  )
}

export function LegalPage({ kind }: { kind: keyof typeof legal }) {
  const page = legal[kind]
  return (
    <PublicShell>
      <article className="legal">
        <p className="eyebrow">{page.eyebrow}</p>
        <h1>{page.title}</h1>
        {page.intro && <p>{page.intro}</p>}
        {page.effectiveDate && (
          <p>
            <strong>Effective date:</strong> {page.effectiveDate}
          </p>
        )}
        {page.sections.map(([heading, body]) => (
          <section key={heading}>
            <h2>{heading}</h2>
            <p>{body}</p>
          </section>
        ))}
      </article>
    </PublicShell>
  )
}

export const HelpLink = ({ category }: { category: string }) => (
  <a className="button-link quiet help-link" href={`/help?category=${encodeURIComponent(category)}`}>
    Help
  </a>
)
