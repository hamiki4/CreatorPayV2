# Testing Strategy

Tests follow the risk pyramid and use deterministic clock/ID/key/provider abstractions. Financial and authorization tests are release gates.

| Layer | Required coverage |
|---|---|
| Domain unit | every valid/invalid status transition; eligibility boundary (`start <= now < end`); commission precedence/rounding; earning/wallet invariants; phone date rule |
| Application | use-case authorization/scope, validation, idempotency, outbox and error mapping with mocked ports |
| Infrastructure | EF mappings/migrations, repositories, encryption/HMAC, provider adapters, job leases |
| PostgreSQL integration | real supported PostgreSQL via isolated database/container; constraints, transactions, locks, indexes, migration up/down/forward compatibility |
| API integration | auth policies, Problem Details, DTO masking, concurrency/version, idempotency replay/conflict, rate limits |
| Frontend | components/forms/localization/accessibility; API state, empty/loading/error; IndexedDB/service-worker behavior |
| End-to-end | creator/merchant onboarding and dual approval through transaction, notification, weekly payout; disputes/reversals and admin operations |
| Security | IDOR/cross-merchant/location, token rotation/reuse/revocation, injection/XSS/CSRF/CORS, upload, secret/PII logs, QR forgery/replay, dependency scans |
| Concurrency | simultaneous wallet spend, same phone/use, partnership/config change, payout batch, duplicate callback/offline sync |
| Ledger/property | conservation, no negative available balance, immutable entries, reversal linkage, randomized rate/rounding cases |
| Offline sync | order, restart, duplicate key, altered payload, expired approval, status changed offline, approval-required and rejected recovery |

Acceptance criteria in [complete acceptance criteria](21-complete-acceptance-criteria.md) map to automated test IDs. CI runs format/static analysis, unit/application/frontend tests, PostgreSQL/API integration, build and migration checks; nightly/pre-release adds E2E, concurrency, DAST and restore drills. Production-like test data is synthetic and secrets are never copied. Flaky tests are quarantined only with owner/deadline and cannot conceal financial/security failures.
# Milestone 4 testing note

Password policy, framework hashing, opaque-token hashing/randomness, JWT claims, and EF metadata are covered without substituting SQLite. PostgreSQL transaction/index integration should run against a dedicated PostgreSQL database or Testcontainers in CI.
