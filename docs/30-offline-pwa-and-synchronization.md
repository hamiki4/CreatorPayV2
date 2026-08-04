# Offline checkout status

Offline financial checkout is intentionally unsupported in Milestone 20.1.

The supported flow requires a live, opaque, three-minute, single-use checkout session, an authenticated cashier presentation, and approval from the authenticated customer before any financial posting. Campaign and merchant discovery QRs have no financial authority and cannot be queued for later purchase processing.

`POST /api/v1/cashier/offline-sync` remains authenticated for compatibility and returns `410 Gone` with instructions to use a temporary checkout QR. It does not resolve campaign QR payloads, create approval requests, debit wallets, create earnings or cashback, or post journals.
