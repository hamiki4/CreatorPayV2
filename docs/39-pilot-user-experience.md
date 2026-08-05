# Pilot user experience

Milestone 21 keeps the 10% merchant commission split at creator 4%, customer 3%, and platform 3%. All deposits and payouts are manual. Checkout always needs connectivity and explicit customer approval.

The responsive web entry screen supports sign-in and customer registration. Customers can search by merchant and zone without GPS, follow `/m/{publicQrId}` discovery links, select featured or active promotions, save promotions, create a three-minute single-use checkout token, approve/reject a presented bill, track progress to the 1,000 ETB payout threshold, request payout, and review history. Five-second refresh is the durable fallback if real-time delivery reconnects.

Cashiers verify their assigned locations, submit only `creatorpay:checkout:` tokens, enter the bill amount, review the 10/4/3/3 preview, and wait for a customer decision. Campaign and merchant QR tokens fail token validation. Submit is locked while a request is in flight; retries show explicit recovery guidance. Posting happens only in the customer approval transaction.

Existing role workspaces provide merchant locations/staff/campaigns/featured creator/store QR/trial/wallet/deposits/reporting, creator partnerships/campaigns/QR/earnings/payouts/notifications, and admin approvals/deposits/payouts/risk/reversals/reporting/audit operations. Protected customer information is not returned in creator attribution.

Low-bandwidth rules: text-first cards, no mandatory images/GPS, bounded lists, cached non-financial shell, visible loading/empty/retry states, and no offline financial queue. Existing English/Amharic localization remains the supported translation boundary.

