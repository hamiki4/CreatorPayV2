# Domain Model

## Entity purposes

- `UserAccount` stores credentials, one MVP primary role, verification flags, and links to the corresponding role record. Password hashes are persistence-only and are not exposed by API models.
- `Creator` and `Merchant` represent independently platform-approved participants with public IDs and contact details.
- `MerchantLocation` represents a merchant site and its IANA/compatible time-zone identifier.
- `Supervisor` and `Cashier` are merchant personnel; assignment entities allow each to operate at multiple locations.
- `MerchantCreatorPartnership` is the merchant-specific creator approval record.
- `PartnershipLocation` optionally limits an approved partnership to selected locations.

## Approval boundaries

Platform approval activates a creator globally but does not authorize transactions at any merchant. Each merchant must separately approve exactly one partnership record for that creator. Transaction eligibility requires partnership status `Approved`, a start date absent or reached, and an end date absent or strictly in the future. Creator, merchant, and account eligibility will later be checked by an application service.

## Status behavior

Partnership statuses mean:

- `Pending`: awaiting merchant decision.
- `Approved`: eligible during its configured date window.
- `Rejected`: declined with a reason.
- `Suspended`: temporarily disabled with a reason.
- `Revoked`: approval withdrawn.
- `Expired`: date/administrative expiration state.
- `Blocked`: explicitly prohibited.

The current domain supports creator and merchant approval/suspension plus partnership approval, rejection, suspension, revocation, and time-supplied eligibility checks. Invalid transitions, non-UTC timestamps, missing reasons, and invalid date ranges are rejected. The complete future partnership graph (including renewal, blocking, expiry and reinstatement) is specified in [merchant-creator partnerships](08-merchant-creator-partnerships.md); it is not yet implemented.

## Current limitations

Milestone 2 contains no registration, authentication/JWT, business controllers, public-ID generator, transaction processing, QR codes, commissions, wallets, deposits, notifications, or payouts. Full PostgreSQL integration tests and application-level aggregate eligibility checks remain future work.
