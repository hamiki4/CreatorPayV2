# API Design

Milestone 16 adds tenant-scoped creator/merchant dispute create/list/detail APIs and Platform Admin dispute, fraud-alert, and reversal management APIs. Reversal creation requires a unique idempotency key; processing is PlatformAdmin-only and atomic. See `docs/29-disputes-and-reversals.md`.

Milestone 13 adds cashier precheck/submit/request-detail; customer code send/verify; scoped supervisor and merchant-admin list/detail/approve/deny; and read-only platform-admin list/detail endpoints. Repeats return HTTP 202 approval data and failures use Problem Details.

Milestone 11 adds `/api/v1/merchant/wallet`, merchant/admin deposit endpoints, and cashier/merchant/admin purchase endpoints. Financial POSTs require `Idempotency-Key` and return DTOs or RFC Problem Details.

Milestone 8 partnership APIs are versioned under `/api/v1/creator`, `/api/v1/merchant`, and read-only `/api/v1/admin/partnerships`; they use claim-derived scope, DTOs, and Problem Details.

## Merchant organization endpoints

Milestone 7 endpoints and authorization rules are catalogued in [23-merchant-organization-and-staff.md](23-merchant-organization-and-staff.md). They use versioned `/api/v1` routes, request/response DTOs, claim-derived merchant scope, role policies, and Problem Details failures. EF entities and secret hashes are never serialized.

All future routes are under `/api/v1`, JSON over HTTPS (except health), use UTC ISO-8601 timestamps and decimal strings for money. IDs are opaque UUID/public IDs. Collection routes use bounded cursor pagination and filters. Errors use RFC 9457 Problem Details with stable `code`, `traceId` and field errors. Mutations use optimistic concurrency (`ETag`/version) where applicable. Financial, notification-triggering, offline and create routes marked **I** require `Idempotency-Key`; duplicate key/same canonical request returns the original response, while changed content returns `409`.

## Endpoint inventory

The request/response column names the main contract; all requests are schema/format/size validated, all responses are scope-filtered DTOs, and no DTO exposes hashes, secrets, full phone or internal provider credentials.

| Area | Method and route | Role | Request → response | Authorization / errors / audit / idem |
|---|---|---|---|---|
| Auth | `POST /auth/register/creator`, `/auth/register/merchant` | Public | registration → `202` application | uniqueness/password/consent; `400/409/429`; registration audit; **I** |
| Auth | `POST /auth/login`, `/auth/refresh`, `/auth/logout` | Public/authenticated | credentials/token → token/session result | active/eligible account, rotation; `401/423/429`; auth audit; refresh/logout **I** |
| Auth | `POST /auth/verify-email`, `/auth/verify-phone`, `/auth/password/forgot`, `/auth/password/reset` | Public | token/OTP → generic result | expiry/attempt rules; `400/410/429`; security audit; **I** |
| Creator | `GET/PATCH /creators/me`; `POST /creators/me/closure-requests` | Creator | profile/version or closure reason → own profile/request | own subject; `403/409/422`; profile/closure audit; mutations **I** |
| Creator | `GET /creators/me/qr`; `POST /creators/me/qr/rotate` | Creator | none/reason → signed QR metadata | Active creator; `403/409`; QR audit; rotate **I** |
| Merchant | `GET/PATCH /merchants/me` | Merchant Admin | version/profile → merchant DTO | own merchant; `409/422`; profile audit; PATCH **I** |
| Locations | `GET/POST /merchants/me/locations`; `GET/PATCH/DELETE /merchants/me/locations/{id}` | Merchant Admin | location/version → DTO | own merchant; DELETE deactivates; `404/409/422`; audit; mutations **I** |
| Staff | `GET/POST /merchants/me/supervisors`; `PATCH/DELETE /merchants/me/supervisors/{id}` | Merchant Admin | identity/assignments → staff DTO | own merchant/location; `404/409`; audit; mutations **I** |
| Staff | `GET/POST /merchants/me/cashiers`; `PATCH/DELETE /merchants/me/cashiers/{id}` | Merchant Admin | identity/assignments → staff DTO | same; audit; mutations **I** |
| Discovery | `GET /merchants`; `GET /merchants/{publicId}` | Active Creator | query/cursor → discoverable summary | public-safe active merchants only; `404/429`; no sensitive audit |
| Partnerships | `GET/POST /partnerships`; `GET /partnerships/{id}` | Creator/Merchant Admin | filters or target/source → partnership | participant scope; duplicate `409`; create audit; POST **I** |
| Partnerships | `POST /partnerships/{id}/approve|reject|suspend|reinstate|revoke|block|unblock|renew`; `PATCH /partnerships/{id}` | Merchant Admin (creator may revoke own) | reason/dates/locations/rule/campaign/version → state | merchant ownership/valid transition; `403/409/422`; audit; **I** |
| QR | `POST /cashier/qr/validate` | Cashier | QR, location → eligibility summary | active assignment and scopes; `403/422`; attempt audit; **I** |
| Transactions | `POST /cashier/transactions`; `GET /cashier/transactions/{id}` | Cashier | clientOperationId, QR, location, protected phone input, amount, repeatApprovalId? → receipt/status | all eligibility/wallet/reuse checks; `402/403/409/422`; financial audit; **I required** |
| Transactions | `POST /cashier/transactions/sync` | Cashier | bounded offline operation batch → per-item results | server revalidation; `207/409/422`; sync audit; per-item **I** |
| Repeat use | `POST /repeat-use-requests`; `GET /repeat-use-requests` | Cashier/Supervisor | operation/reason or filters → request(s) | assigned location; `409/410`; audit; create **I** |
| Repeat use | `POST /repeat-use-requests/{id}/approve|deny`; `POST /repeat-use-requests/{id}/otp/confirm` | Supervisor / Customer token | reason or OTP → state | assigned supervisor/customer challenge; `403/410/429`; audit; **I** |
| Wallet | `GET /merchants/me/wallet`; `GET /merchants/me/wallet/entries` | Merchant Admin | filters/cursor → balance/ledger | own merchant; exports masked; sensitive-read audit |
| Deposits | `POST /merchants/me/deposits`; `GET /merchants/me/deposits/{id}` | Merchant Admin | amount/reference/proof → pending deposit | active/remediation access; `409/422`; audit; POST **I** |
| Deposits | `GET /admin/deposits`; `POST /admin/deposits/{id}/approve|reject` | Platform Admin | filters or decision/evidence → deposit | separation/policy; `409/422`; financial audit; **I** |
| Commission | `GET/POST /admin/commission-rules`; `PATCH /admin/commission-rules/{id}/retire` | Platform Admin | versioned rule → rule | config permission/non-overlap; audit; mutations **I** |
| Commission | `GET/POST /merchants/me/commission-rules`; `PATCH /merchants/me/commission-rules/{id}/retire` | Merchant Admin | permitted merchant rule → rule | own merchant/platform bounds; audit; mutations **I** |
| Earnings | `GET /creators/me/earnings`; `GET /creators/me/statements` | Creator | filters/cursor → masked financial DTO | own creator; sensitive-read/export audit |
| Payouts | `GET /creators/me/payouts`; `GET /admin/payouts`; `POST /admin/payout-batches`; `POST /admin/payouts/{id}/retry|hold|release` | Creator / Platform Admin | filters/cutoff/decision → payout/batch | scope and finance permission; `409/422`; audit; mutations **I** |
| Disputes | `POST /disputes`; `GET /disputes`; `POST /disputes/{id}/resolve`; `POST /transactions/{id}/reverse` | scoped participant / Platform Admin | transaction, reason, evidence/decision → case/result | participant/reviewer separation; `409/422`; audit; mutations **I** |
| Notifications | `GET /notifications`; `PATCH /notifications/{id}/read`; `GET/PATCH /notification-preferences` | Authenticated | filters/state/preferences → own data | own subject; `409/422`; preference audit; mutations **I** |
| Admin | `GET /admin/approvals`; `POST /admin/creators/{id}/approve|reject|suspend|reactivate|close`; merchant equivalents | Platform Admin | evidence/reason/version → entity | approval permission/transition; `409/422`; audit; **I** |
| Admin | `GET/PATCH /admin/configuration`; `GET/POST/PATCH /admin/notification-templates` | Platform Admin | versioned settings/template → snapshot | specialized permission; `409/422`; config audit; mutations **I** |
| Reports | `GET /reports/creator-performance|merchant-performance|cashier-activity|platform-revenue|reconciliation|fraud` | role-dependent | time/scope filters → aggregate/paged rows | server scope; `403/422`; export/sensitive audit |
| Health | `GET /health`; `GET /health/ready` | Public/internal | none → coarse liveness/readiness | public response leaks no dependencies; readiness network-restricted; no idem |

Uploads use separate authenticated presign/finalize endpoints or streamed multipart with documented limits. Provider callbacks live at `/api/v1/integrations/{provider}/callbacks`, authenticate signatures, retain raw evidence securely and require idempotency. Exact DTO schemas and OpenAPI examples are implementation-milestone deliverables.
# Milestone 4 authentication API

Versioned authentication routes are under `/api/v1/auth`. They use request/response DTOs and RFC Problem Details failures. JWT Bearer is the API authentication scheme; OpenAPI exposure is limited to development.

## Milestone 5 creator API

Implemented routes under `/api/v1/creators` are public `POST /register`, `POST /verify-email`, and `POST /verify-phone`; Creator-only `GET /me` and `PUT /me`; and PlatformAdmin-only `GET /pending`, `GET /{creatorId}`, `POST /approve`, `POST /reject`, `POST /suspend`, and `POST /reactivate`. Decision DTOs carry `creatorId` and an optional reason (required for reject/suspend). No EF entity is serialized.
# Milestone 9 QR endpoints

Creator: `GET /api/v1/creator/qr`, `GET /api/v1/creator/qr/image`, `GET /api/v1/creator/qr/history`, `POST /api/v1/creator/qr/regenerate`, `POST /api/v1/creator/qr/revoke`. Merchant operations: `POST /api/v1/merchant/qr/validate` with `{ payload, locationId }`. Authentication derives creator and merchant/staff scope from claims. Errors use Problem Details; validation failures return a stable safe code in a successful validation response.
# Commission endpoints (Milestone 10)

Platform Admin manages `GET/POST/PUT /api/v1/admin/commission-plans`, `GET/POST /api/v1/admin/commission-rules`, version creation/history, rule status, platform/merchant/partnership assignments, and `POST /api/v1/admin/commission-preview`. Merchant Admin reads `/api/v1/merchant/commission-rules/effective` and scoped partnership rules and uses `/api/v1/merchant/commission-preview`. Creators read `/api/v1/creator/partnerships/{id}/commission-rule`. Validation errors use Problem Details; previews have no financial side effects.
# Milestone 12 API

Creator read APIs: `GET /api/v1/creator/earnings`, `/earnings/summary`, `/earnings/{id}`, `/payouts`, and `/payouts/{id}`. Platform Admin APIs cover payout-batch list/create/detail/process/cancel; payout list/detail/submit/mark-paid/mark-failed/retry/cancel; and `POST /api/v1/admin/earnings/mature`. Responses use DTOs and failures use Problem Details.
# Notification APIs

Milestone 14 adds `/api/v1/notifications`, `/notifications/unread-count`, notification detail/read/read-all, and notification preferences. Platform Admin operations are under `/api/v1/admin/notifications`, `/notification-outbox`, `/notification-dead-letters`, and `/notification-templates`. User routes require authentication; administrative routes require `PlatformAdminOnly`.

## Offline sync API

`POST /api/v1/cashier/offline-sync` is Cashier-only, rate/size/schema bounded, and returns ordered per-item Confirmed, ApprovalRequired, DuplicateConfirmed, Rejected, Conflict, Expired, or RetryLater results with safe errors and correlation IDs.
# Platform Admin API

Milestone 18 consolidates bounded support endpoints under `/api/v1/admin`, including dashboard summary/trends, creators, merchants, accounts, partnerships, purchases, offline sync, audit, global search, system status, and operational alerts. Existing domain endpoints remain the authority for financial and approval mutations.
