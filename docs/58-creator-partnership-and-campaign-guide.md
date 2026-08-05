# Creator partnership and campaign guide

Approved creators can discover approved merchants and request a partnership with an introductory proposal. Active duplicate requests are rejected. Merchant Admin review is merchant-scoped and can approve/reject the request and configure campaign duration, earliest start, and the versioned 4% creator / 3% customer / 3% platform rule.

Approval creates a campaign waiting for the creator; it does not activate it. Start Promotion records server UTC time, calculates expiry, activates its campaign-specific opaque QR/deep link, and audits/notifies the transition. QR resolution only redirects into Weymela and never posts money. Expired, cancelled, suspended, or revoked codes are rejected.

Renewal creates a new campaign period and QR linked to the expired campaign. An expired QR is never reactivated.
