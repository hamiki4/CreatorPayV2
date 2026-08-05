# Pilot seeding and demo accounts

Pilot data is allowed only when `ASPNETCORE_ENVIRONMENT` is `Development` or `Pilot`. Production startup never seeds. Use an isolated database and generated passwords; do not commit `scripts/pilot/seed.sql`, `.env`, tokens, QR secrets, or result files.

Create the manifest through the existing audited APIs in this order: one Platform Admin; merchant registration/verification/approval; one location; two cashier invitations; three creator registrations/verifications/platform approvals; three partnerships and campaigns; campaign approvals and starts; featured creator; three customer registrations; trial credit; notifications; then exercise the normal deposit and payout APIs to create representative history. Use these stable emails: `admin`, `merchant`, `cashier1`, `cashier2`, `creator1`–`creator3`, and `customer1`–`customer3`, all at `pilot.creatorpay.test`. Generate a unique 20+ character password per account into a local password manager.

Export the resulting database rows as idempotent `INSERT ... ON CONFLICT DO NOTHING` statements to the ignored `scripts/pilot/seed.sql`; hashes and QR token hashes must be generated locally. The file must include a `PilotSeed:v22` audit marker. Run `scripts/pilot/reset-and-reseed.ps1 -Environment Pilot -ConfirmReset`, twice, and confirm identical counts. Verify roles with a read-only query over `UserAccounts` selecting only email, role, status and verification flags. Never update balance columns directly: ledger, wallet, trial, deposit and payout samples must be produced by application services/APIs.

The committed reset wrapper deliberately refuses Production, requires explicit destructive confirmation, reapplies migrations, and fails when the environment-local seed artifact is absent.
