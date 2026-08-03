# CreatorPay V2

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
- Delivery sequence: [implementation roadmap](docs/implementation-roadmap.md)

Open legal, banking, tax, privacy, hosting and payment-provider questions are intentionally recorded as decision gates rather than invented implementation details.
# Milestone 4: authentication

CreatorPay now includes secure password hashing, JWT Bearer access tokens, rotating hashed refresh tokens, password reset foundations, account lockout, authentication audits, role policies, and `/api/v1/auth` endpoints. See [authentication and token security](docs/22-authentication-and-token-security.md) for configuration, risks, and manual commands.

## Milestone 5: creator onboarding

Creators register at `/api/v1/creators/register`, complete development-provider email and phone verification, and enter the Platform Admin approval queue. Creator profile access is owner-scoped; approval decisions require `PlatformAdmin`. Verification tokens are hashed at rest and onboarding events are audited. The React shell presents a creator status dashboard. No merchant, partnership, QR, financial, or payout behavior is included.
