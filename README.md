# Weymela

Public users may register as a Customer, Creator, or Merchant; Platform Admin has no public registration. Creator earnings (4%) are paid manually, creators never receive customer purchase totals, and campaign QR/deep links only enter Weymela—they cannot directly post financial transactions. See [creator onboarding](docs/55-creator-registration-and-onboarding.md) and the [campaign guide](docs/58-creator-partnership-and-campaign-guide.md).

**ወይ መላ! — The smarter way to shop, promote, and earn.**

Weymela is the user-facing identity for this creator, customer, and merchant pilot. Internal projects, namespaces, database objects, Docker images, telemetry, and migration history intentionally retain the CreatorPay name for now.

## Milestone 23

Milestone 23 adds bilingual sign-in and customer/merchant registration, verified `PendingReview` merchant onboarding, audited Platform Admin review, and merchant status/dashboard presentation. No banking credential, payment-provider, automatic transfer, or wallet-balance editing integration is included.

Milestone 22 adds the guarded pilot reset/reseed workflow, executable health/migration/seed/scenario verifier, admin pilot metrics report, expanded read-only reconciliation pack, usability/accessibility evidence, operational runbook, and measurable go/no-go checklist. Start with [pilot seeding](docs/45-pilot-seeding-and-demo-accounts.md) and the [test harness](docs/46-pilot-test-harness.md). The 4% creator / 3% customer / 3% platform split and manual-money boundary are unchanged.

Milestone 21 adds the pilot entry experience, customer discovery/checkout/cashback journey, cashier split preview and recovery guidance, real-time-with-polling confirmation behavior, reporting SQL, and pilot deployment/support documentation. Start with [the pilot scenario](docs/40-end-to-end-pilot-scenario.md) and [deployment guide](docs/41-pilot-deployment-guide.md). Deposits and creator/customer payouts remain manual; no payment provider is integrated.

Milestone 20.1 adds merchant discovery, discovery-only campaign QRs, and three-minute single-use checkout QRs. Only an authenticated customer's approval of a cashier-presented checkout session can post a purchase. Deposits and payouts remain manually verified external-money operations backed by immutable entries, balanced journals, audits, and reconciliation. See [campaign lifecycle and manual finance](docs/34-campaign-lifecycle-and-manual-finance.md).

Milestone 20 adds security and deployment-readiness controls without changing APIs or the database: stricter production configuration and response headers, SBOM/container scanning, immutable environment promotion gates, checksum-verified backup/restore, HTTPS readiness and load-test scripts, SLO/UAT gates, cloud prerequisites, and a final evidence-based checklist. Production remains blocked until external security, provider, restore, load, UAT, legal, and operational evidence is approved. See [security and production readiness](docs/33-security-and-production-readiness.md).

Milestone 16 adds configurable fraud alerts, creator/merchant disputes, Platform Admin review queues, and idempotent append-only commission reversals. Original purchases, snapshots, earnings, payouts, wallet history, and journals are preserved; paid earnings create a manual recovery receivable. See [disputes and reversals](docs/29-disputes-and-reversals.md).

Milestone 15 adds separate API/Worker/Web/PostgreSQL containers; startup validation; JSON structured logs and correlation IDs; `/health/live` and `/health/ready`; security headers, explicit CORS and partitioned rate limiting; PostgreSQL-safe worker locks and execution history; Docker-backed integration tests; GitHub Actions; and database/operations runbooks. Copy `.env.example` to ignored `.env`, replace placeholders, start PostgreSQL, and apply migrations separately. Production never auto-migrates and must use an external secret store. See [deployment](docs/20-deployment-and-environments.md), [operations](docs/24-operational-runbook.md), [observability](docs/25-observability-and-monitoring.md), [security](docs/26-security-hardening.md), [database operations](docs/27-database-operations.md), and [CI/CD](docs/28-ci-cd.md).

Milestone 13 adds privacy-preserving Ethiopian customer-phone verification and supervised same-day repeat-use approval. Configure independent `CustomerVerification__HmacSecret` and `CustomerVerification__EncryptionKey` secrets of at least 32 characters. Real SMS is intentionally not integrated.

Milestone 11 adds the merchant prepaid ETB wallet, manual deposit approval, and atomic cashier purchase confirmation. Configure `MerchantWallet:LowBalanceThreshold` and see [the wallet workflow](docs/11-merchant-wallet-and-deposits.md).

Milestone 8 adds merchant-specific creator approval, audited partnership lifecycles, location/date eligibility, scoped APIs, and role-aware partnership screens. See [Merchant–Creator Partnerships](docs/08-merchant-creator-partnerships.md).

Milestones 1–7 are implemented. Milestone 7 adds merchant locations, Supervisor/Cashier management, secure staff invitations, strict merchant/location authorization, organization audit events, and responsive role-aware React screens. See [Merchant Organization and Staff](docs/23-merchant-organization-and-staff.md) for APIs, security controls, limitations, and validation commands.

CreatorPay is a creator-to-merchant affiliate-commerce platform. Milestones 1–5 provide the clean-architecture foundation, domain/PostgreSQL persistence, requirements blueprint, authentication boundary, and creator registration/verification/platform-approval workflow.

The defining rule is dual approval: a creator must first be approved by the CreatorPay Platform Admin and then separately approved by each merchant through a transaction-eligible `Approved` `MerchantCreatorPartnership`. Platform approval alone never authorizes merchant commission transactions.

## Prerequisites and validation

- .NET 10 SDK and `dotnet-ef`
- Node.js and npm
- Docker Desktop for local PostgreSQL (optional for current metadata-only tests)

```powershell
dotnet build CreatorPay.slnx
dotnet test CreatorPay.slnx
npm run build --prefix src/CreatorPay.Web
```

Infrastructure tests currently validate Npgsql EF metadata without substituting SQLite. Migrations are deliberately not applied at API startup. See [database design](docs/03-database-design.md) for local PostgreSQL setup.

## Documentation map

- Foundation: [business](docs/01-business-overview.md), [architecture](docs/02-system-architecture.md), [database](docs/03-database-design.md), [domain](docs/04-domain-model.md)
- Participants: [roles](docs/05-user-roles-and-permissions.md), [registration](docs/06-registration-and-verification.md), [platform approval](docs/07-platform-approval-workflows.md), [partnerships](docs/08-merchant-creator-partnerships.md)
- Commerce: [cashier/QR](docs/09-qr-and-cashier-transaction-workflow.md), [commission](docs/10-commission-engine.md), [wallet](docs/11-merchant-wallet-and-deposits.md), [earnings/payouts](docs/12-creator-earnings-and-payouts.md), [customer phone](docs/13-customer-phone-and-repeat-use.md), [notifications](docs/14-notifications.md)
- Delivery: [security/privacy](docs/15-security-fraud-and-privacy.md), [API](docs/16-api-design.md), [UI](docs/17-ui-navigation-and-screens.md), [operations](docs/18-reporting-and-operations.md), [testing](docs/19-testing-strategy.md), [deployment](docs/20-deployment-and-environments.md), [MVP acceptance](docs/21-complete-acceptance-criteria.md)
- Milestone 19: [localization, accessibility, and governed reporting](docs/32-localization-accessibility-and-reporting.md)
- Milestone 20: [security hardening and production readiness](docs/33-security-and-production-readiness.md)
- Delivery sequence: [implementation roadmap](docs/implementation-roadmap.md)

Open legal, banking, tax, privacy, hosting and payment-provider questions are intentionally recorded as decision gates rather than invented implementation details.
# Milestone 4: authentication

CreatorPay now includes secure password hashing, JWT Bearer access tokens, rotating hashed refresh tokens, password reset foundations, account lockout, authentication audits, role policies, and `/api/v1/auth` endpoints. See [authentication and token security](docs/22-authentication-and-token-security.md) for configuration, risks, and manual commands.

## Milestone 5: creator onboarding

Creators register at `/api/v1/creators/register`, complete development-provider email and phone verification, and enter the Platform Admin approval queue. Creator profile access is owner-scoped; approval decisions require `PlatformAdmin`. Verification tokens are hashed at rest and onboarding events are audited. The React shell presents a creator status dashboard. No merchant, partnership, QR, financial, or payout behavior is included.
# Milestone 9: creator QR validation

CreatorPay now supports one permanent active creator QR, secure regeneration/revocation with retained history, on-demand PNG output, and authenticated merchant-side validation against merchant/staff location scope and approved partnership eligibility. This milestone stops before transactions or financial processing. See `docs/09-qr-and-cashier-transaction-workflow.md` for endpoints, security, UI, and limitations.
# Milestone 10: commission engine

CreatorPay now includes configurable commission plans/rules, immutable effective-dated versions, campaign/partnership/merchant/platform priority selection, decimal ETB calculation with configurable rounding, historical snapshot storage, Platform Admin management APIs, scoped Merchant/Creator views, audited non-financial previews, and a React commission workspace. Purchase and wallet processing remain intentionally deferred to Milestone 11.
# Milestone 12

CreatorPay now prepares weekly ETB creator payouts from immutable confirmed-purchase earnings. Creator views expose balances/history; Platform Admin views support idempotent batches and manual payout outcomes. No external payout rail is integrated. See `docs/12-creator-earnings-and-payouts.md`.
# Milestone 14: notifications

CreatorPay includes a PostgreSQL notification outbox, safe versioned templates, in-app notifications, development email/SMS/push providers, retry/dead-letter operations, user preferences, Platform Admin operations, and hash-only customer verification integration. See [docs/14-notifications.md](docs/14-notifications.md).

# Milestone 17: offline cashier PWA

Offline financial checkout is unsupported because customer approval and a live, single-use checkout session are mandatory. The authenticated legacy synchronization endpoint returns `410 Gone` and never posts money. See [offline design](docs/30-offline-pwa-and-synchronization.md).
# Platform administration

Milestone 18 adds the secure `/admin` operational workspace. See [docs/31-platform-admin-portal.md](docs/31-platform-admin-portal.md) for navigation, permissions, privacy controls, APIs, alert behavior, limitations, and the recommended next milestone.
