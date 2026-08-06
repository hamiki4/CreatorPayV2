# Milestone 29: Help, support, legal, and pilot readiness

## Existing foundations reused

Weymela already provides public Shopper, Creator, and Business registration; English and Amharic locale selection; separate Shopper, Creator, Merchant Admin, Cashier, Supervisor, and Platform Admin workspaces; masked phone processing; password reset; public offer discovery; notification/outbox delivery; admin system, alert, audit, fraud, dispute, reversal, and reconciliation views; and deployment, backup, rollback, incident, support, and role guides. Milestone 29 reuses these controls and routes instead of introducing a parallel support or notification channel.

## Pilot role procedures

- Creator onboarding: register, verify email and phone, complete a truthful social profile, obtain platform approval, request Business partnership approval, confirm that the active Offer QR appears, and run a non-financial QR display check.
- Business onboarding: register and verify contacts, complete platform review, configure locations and staff, fund the pilot wallet through the documented manual process, approve pilot Creators, publish a bounded Offer, and review its reuse rule.
- Cashier training: sign in only to the assigned account/location, distinguish discovery QR from the short-lived checkout token, enter only the displayed checkout information, never request an OTP/password, and stop on an expired, altered, or rejected QR.
- Shopper checkout: discover an active Offer, sign in or register, present the supported QR flow, review the amount, explicitly approve or reject, and verify the resulting cashback/history state.
- Supervisor override: independently review the masked repeat-use request and context; approve or deny with the supported workflow. A Cashier never approves their own exception.
- Wallet funding: submit and verify funding through the existing audited manual deposit process. Reconcile the wallet and journal; never edit a balance directly.
- Support escalation: collect the public support/correlation reference, role, UTC time, and public transaction reference. Do not collect passwords, OTPs, complete phone numbers, QR tokens, or payment credentials. Triage through support requests, notifications/outbox, system status, and audit records.
- Dispute handling: preserve evidence, create or locate the authenticated dispute, separate investigation from financial action, and use the approved reversal workflow. Never rewrite posted ledger history.
- Incident response: assign severity, owner, next-update time, and containment; protect personal data; check readiness, logs, outbox, reconciliation, and backups; follow `docs/49-pilot-operations-runbook.md`.
- Daily checklist: live/ready health; worker/outbox/dead letters; open operational alerts; wallet and journal reconciliation; pending funding/payout/override/dispute queues; backup status; support queue age; provider delivery failures; and pilot-metric snapshot.
- Success metrics: completed and rejected checkout counts, time to approval, duplicate-post count, journal imbalance count, support volume/first-response/resolution time, provider failure rate, QR validation failures, active approved Creators/Businesses, funded wallets, and confirmed Shopper cashback. Thresholds remain private operational configuration.

## Pilot launch checklist

- [ ] Environment name, public origins, API URL, HTTPS/proxy, and production configuration validated.
- [ ] All database migrations reviewed, backed up, and applied to the intended database.
- [ ] JWT, phone/OTP protection, database, provider, and observability secrets supplied outside source control.
- [ ] `Support__Email` is a monitored non-personal support address.
- [ ] SMS and email providers, sender identities, retry behavior, and dead-letter alerts tested.
- [ ] Each participating Merchant wallet is funded and reconciled.
- [ ] Pilot Creators and Businesses are verified, reviewed, approved, and scoped to expected partnerships/offers.
- [ ] Cashier and Supervisor accounts, locations, least privilege, and training are verified.
- [ ] Happy-path, reject, expiry, repeat-use override, low-wallet, and reversal test transactions pass.
- [ ] Health, structured logs, metrics, alerts, outbox, support notifications, and on-call routing are monitored.
- [ ] Backup completed and isolated restore evidence is current.
- [ ] Candidate-specific rollback procedure and owner are confirmed.
- [ ] English and Amharic Terms and Privacy drafts are reviewed and approved by qualified counsel before launch.

This checklist supplements `docs/41-pilot-deployment-guide.md`, `docs/44-pilot-support-runbook.md`, `docs/48-pilot-metrics-and-acceptance-criteria.md`, and `docs/50-pilot-go-live-checklist.md`.
