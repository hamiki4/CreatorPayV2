# Milestone 26 sign-in, verification, and role dashboard pilot test log

All browser tests remain **FAIL / Open** until manually verified. Automated coverage does not change browser-test status.

## Sign-in and account status

| Test | Expected | Result | Status |
|---|---|---|---|
| Shopper sign-in | Shopper reaches only the Shopper workspace. | FAIL | Open |
| Content Creator sign-in | Creator reaches only the Creator workspace and can view onboarding while pending review. | FAIL | Open |
| Business Owner sign-in | Business Owner reaches only the Business Owner workspace and can view onboarding while pending review. | FAIL | Open |
| Platform Admin sign-in | Platform Admin reaches only the Platform Admin portal. | FAIL | Open |
| Merchant Admin sign-in | Merchant Admin reaches only the Business Owner workspace. | FAIL | Open |
| Supervisor sign-in | Supervisor reaches only the Supervisor workspace. | FAIL | Open |
| Cashier sign-in | Cashier reaches only the Cashier checkout workspace. | FAIL | Open |
| Invalid password | A generic invalid-credentials response is shown without account disclosure. | FAIL | Open |
| Unknown account | The same generic invalid-credentials response is shown. | FAIL | Open |
| Pending verification | Eligible public users can sign in only to onboarding/status functionality. | FAIL | Open |
| Pending review | Creators and Business Owners can view onboarding status but cannot use active operations. | FAIL | Open |
| Suspended, rejected, or locked | Sign-in and protected operations are denied safely. | FAIL | Open |
| Refresh and expiration | Refresh rotation works; expired or reused tokens are rejected. | FAIL | Open |
| Logout | Access and refresh authentication state is cleared. | FAIL | Open |

## Verification

| Test | Expected | Result | Status |
|---|---|---|---|
| Email verification success | A valid, unused token persists email verification. | FAIL | Open |
| Phone verification success | A valid, unused token persists phone verification. | FAIL | Open |
| Wrong verification token | A safe invalid-or-expired response is returned. | FAIL | Open |
| Expired verification token | The token is rejected. | FAIL | Open |
| Verification token reuse | A used token is rejected. | FAIL | Open |
| Verification rate limiting | Repeated requests are rate limited. | FAIL | Open |
| Verification resend | Resend behavior and limits are available and safe. | FAIL | Open |
| Development token reveal | Raw verification values are unavailable outside Development. | FAIL | Open |

## Authorization and dashboard isolation

| Test | Expected | Result | Status |
|---|---|---|---|
| Unauthenticated protected request | HTTP 401. | FAIL | Open |
| Wrong-role protected request | HTTP 403. | FAIL | Open |
| Manual cross-role URL navigation | The user is redirected to their own workspace and receives no cross-role data. | FAIL | Open |
| Shopper dashboard isolation | Only profile, verification, and supported confirmation data is displayed. | FAIL | Open |
| Creator dashboard isolation | Only creator profile, onboarding, QR, partnerships, campaigns, earnings, and notifications are displayed. | FAIL | Open |
| Business Owner dashboard isolation | Only organization, onboarding, locations, staff, wallet, campaigns, partnerships, transactions, and reports are displayed. | FAIL | Open |
| Supervisor dashboard isolation | Only assigned merchant/location supervisory data is displayed. | FAIL | Open |
| Cashier dashboard isolation | Only assigned cashier and checkout data is displayed. | FAIL | Open |
| Platform Admin dashboard isolation | Pending reviews, platform operations, risk, and audit data are displayed. | FAIL | Open |
| Shopper Notification Dashboard | Notification data loads from the backend API without JSON parsing errors. Actual: notification requests are sent to localhost:5173 and receive index.html. | FAIL | Open |

## Platform administration

| Test | Expected | Result | Status |
|---|---|---|---|
| No public Platform Admin registration | No public registration route or form exists. | FAIL | Open |
| Pending review queues | Platform Admin can view pending creators and businesses. | FAIL | Open |
| Approval decisions | Approve, reject-with-reason, and correction requests work where supported. | FAIL | Open |
| Suspension lifecycle | Supported creator and business suspension/reactivation works. | FAIL | Open |
| Audit history | Administrative decisions appear in audit history. | FAIL | Open |

## Existing behavior found before implementation

- `AccountStatus` contains `Draft`, `PendingVerification`, `PendingApproval`, `Active`, `Suspended`, `Rejected`, and `Closed`.
- Email and phone verification are separate booleans; there are no separate `PendingEmailVerification` and `PendingPhoneVerification` enum values.
- Pilot `PendingReview` maps to `PendingApproval` on the user account and the corresponding creator or merchant review status.
- Lockout is represented by `LockoutEndUtc`, not a `Locked` enum value.
- Before Milestone 26 changes, login allowed only `Active` accounts, which prevented pending creators and businesses from viewing onboarding status.
- Verification uses opaque one-time tokens with expiration. Public resend endpoints are not currently implemented.

## Milestone 26/27 Shopper discovery and secure Offer entry

All browser tests below remain **FAIL / Open** until manually verified.

| Test | Expected | Result | Status |
|---|---|---|---|
| Shopper summary formatting | Available cashback and reserved payout labels and `0.00 ETB` values render separately in responsive cards. | FAIL | Open |
| Shopper notification simplification | Notification list, unread count, mark-one, mark-all, and empty state remain; preference rows, channel controls, push, and raw event IDs are absent. | FAIL | Open |
| Creator search by display name | Trimmed, case-insensitive, paginated search returns only Active approved creators with an Active Offer. | FAIL | Open |
| Public creator profile | Only photo/placeholder, display name, provided safe social links, and Active Offers appear. | FAIL | Open |
| Social-link display | Valid HTTPS links open in a new tab with `noopener noreferrer`; invalid or missing links are hidden. | FAIL | Open |
| Active Offer visibility | Creator, Business, partnership, dates, status, and required active location are enforced server-side. | FAIL | Open |
| One-month Offer expiration | Started Offers use the approved duration (30-day pilot default) and disappear after expiration. | FAIL | Open |
| Offer deep-link resolution | `/offers/{code}` and `/o/{code}` open the same canonical Weymela Offer detail; an expired code/QR shows unavailable. | FAIL | Open |
| Secure “Shop with this Creator” checkout entry | An authenticated Shopper receives only the existing temporary checkout QR; Cashier submission and Shopper approval remain required. | FAIL | Open |

Social media is a discovery channel only. Weymela owns the Offer and checkout flow. An Offer link or Offer QR opens Weymela and never posts a transaction or carries an unrestricted checkout token. The temporary, single-use checkout QR is generated only inside authenticated Weymela, remains valid for the configured three-minute pilot window, and financial posting still requires Cashier bill submission followed by Shopper approval.

Creator profile-image metadata already exists, but no approved public image-serving or upload-storage mechanism exists. Discovery therefore uses a safe initials placeholder. Public image URLs must remain unset until that storage/serving capability is implemented.

## Automated browser verification

### Automated PASS

- Playwright suite discovery: 10 tests (five scenarios across desktop Chrome and Pixel 5 profiles).
- Frontend production compilation, TypeScript checking, and Vite bundling.
- API compilation including the `E2E`-only deterministic fixture endpoint.
- Existing frontend unit checks for authentication, localization, notification simplification, safe social-link attributes, and ETB formatting.
- Runner cleanup path: verified to execute after a prerequisite failure without leaving API, Vite, or PostgreSQL processes running.

### Automated FAIL / blocked in this environment

- Full PostgreSQL-backed Playwright execution is blocked because Docker is not installed or available on this workstation. The runner exits before accessing any existing database and reports the missing Docker prerequisite. Browser result for this run: **0 passed, 0 failed, 10 not executed due to environment prerequisite**.

### Still requires final human UX review

- Visual quality, spacing, wording nuance, and touch comfort on representative physical phones.
- Real camera scanning of Offer and temporary checkout QR images.
- Installed-app universal/app-link handoff, because native association files and a native application are outside this web repository.
- Public creator image upload/serving, which remains a safe placeholder until approved storage exists.

### One-command execution

Prerequisites: Docker Desktop with a running Linux-container daemon, .NET 10 SDK, Node.js 18+, and the Playwright Chromium runtime (`npx playwright install chromium --prefix src/CreatorPay.Web` on first use).

Run from the repository root:

```powershell
npm run test:e2e --prefix src/CreatorPay.Web
```

The command generates ephemeral database, JWT, verification, and account credentials in memory; starts PostgreSQL in a disposable container; applies migrations; chooses free API/frontend ports; starts both applications; waits for readiness; seeds synthetic fixtures through an endpoint available only under `ASPNETCORE_ENVIRONMENT=E2E`; retains screenshot, trace, video, console, and HTML-report artifacts on failure; and shuts down all child processes and the container in a `finally` block. It does not use or print local PostgreSQL passwords.
