# Milestone 19 — localization, accessibility, reporting, and production polish

Milestone 19 adds role-scoped reporting without adding financial authority. Platform Admin, Merchant Admin, and Creator users receive aggregate transaction and earning metrics with bounded date filters and daily trends. Merchant and Creator queries are constrained from JWT scope claims at the database query boundary.

## Localization and accessibility

The reporting workspace ships English (`en`) and Amharic (`am`) catalogues and persists the language preference locally. Dates use the selected locale. The UI uses landmarks, labelled controls, table captions and column scopes, live/error announcements, visible high-contrast focus indicators, keyboard-scrollable tables, and skip links.

## Saved views and exports

Saved views are private to their owning account. Filter payloads must be JSON objects. Exports support UTF-8 CSV and compact PDF for `transactions`, `earnings`, and Admin-only `operations`. Policy limits exports to 366 days and 10,000 rows. Every successful export records requester, role, normalized filters, format, row count, correlation ID, and decision. Export projections exclude contact, customer, idempotency, payout-destination, and secret fields.

## Alert-threshold governance

Thresholds are immutable versioned drafts. Approval requires a different Platform Admin from the author and supersedes the prior approved version. Threshold, window, cooldown, severity, reviewer, time, and reason are persisted. No second scheduler or new financial mutation is introduced.

## API summary

- `GET /api/v1/reports/dashboard`
- `GET|POST /api/v1/reports/saved-views`; `DELETE /api/v1/reports/saved-views/{id}`
- `GET /api/v1/reports/export`
- `GET|POST /api/v1/admin/alert-thresholds`
- `POST /api/v1/admin/alert-thresholds/{id}/approve`

Migration: `AddReportingLocalizationAndPolicyGovernance`.

## Known limitations

- PDF is a compact single-page extract and displays at most 250 rows; CSV carries the full bounded extract.
- Localization covers the new reporting experience. Older screens retain their existing English localization foundations.
- Approved thresholds are ready for the existing worker, but this milestone does not add producer cadence.
- The repository has no browser-test harness, so automated accessibility scans remain future work.

## Recommended Milestone 20

Security hardening and deployment readiness: threat/pentest remediation, CI/CD promotion controls, backup/restore and disaster-recovery exercises, production SLO monitoring, provider certification, load testing, and UAT.
