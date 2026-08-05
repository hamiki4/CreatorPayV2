# End-to-end pilot scenario

Use development-only accounts created by the normal registration/invitation/admin approval workflows. Suggested values are `admin@pilot.creatorpay.test`, `merchant@pilot.creatorpay.test`, `cashier@pilot.creatorpay.test`, `creator1@pilot.creatorpay.test`, `creator2@pilot.creatorpay.test`, and `customer@pilot.creatorpay.test`; use a locally generated password of at least 12 characters and never store it in Git.

1. Admin approves the merchant and trial credit. Merchant creates one Addis Ababa location, invites the cashier, approves both creator requests, activates both campaigns, and features creator 1.
2. Customer opens the store QR/deep link, selects a campaign, and generates a checkout QR. Record its three-minute countdown.
3. Cashier signs in, verifies merchant/location, scans the checkout QR, enters **2,000 ETB**, verifies the preview (200 / 80 / 60 / 60 ETB), and sends the request.
4. Customer verifies merchant, creator, and 2,000 ETB then approves. Cashier receives the decision through SignalR or polling fallback.
5. Verify one confirmed purchase, 80 ETB creator earning, 60 ETB cashback, 60 ETB platform revenue, 200 ETB trial usage, notifications/outbox records, and a balanced posted journal. Rescan must be rejected.

Negative passes: reject one fresh session; let one expire; scan campaign/store QR in cashier checkout; disconnect before approval and verify no posting; reconnect and reload pending state.

