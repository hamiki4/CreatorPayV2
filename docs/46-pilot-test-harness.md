# Pilot test harness

Start PostgreSQL, apply migrations, start API/Worker/Web, prepare the local seed artifact described in document 45, then run:

```powershell
$env:ConnectionStrings__CreatorPay = '<local pilot connection>'
./scripts/pilot/verify-pilot.ps1 -ApiBaseUrl http://localhost:8080
```

The command checks liveness, database readiness, migration visibility, the seed manifest/account count, and focused four-party/pilot tests. It writes a timestamped, secret-free JSON result beneath `scripts/pilot/results/` and exits non-zero on failure. Exercise scenarios A–H through the normal APIs: trial purchase; verified deposit and funded purchase; rejection; forced expiry; campaign expiry; 1,000 ETB customer reserve/manual payout; weekly creator batch/manual payout; full and post-payment reversal. For each, retain API correlation IDs and verify the SQL pack. A checkout QR may complete once only and all journals must net to zero.

Slow-network pass: throttle the browser to Slow 3G, delay SignalR, confirm polling/retry restores state without a second post, and confirm approval never works offline. Normal approval target is under 10 seconds; Slow 3G is under 20 seconds, excluding deliberate human think time.
