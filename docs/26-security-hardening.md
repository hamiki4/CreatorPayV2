# Security hardening

Production validates database, JWT, HMAC and encryption configuration and rejects development reveal flags. Secrets belong in user-secrets locally, environment/Docker secrets for controlled deployments, and Key Vault or an equivalent workload-identity store in managed environments. Required secrets include JWT signing key, PostgreSQL password, phone HMAC/encryption keys, email/SMS/push keys, payment credentials, and object-storage credentials. Never commit them.

The API applies HSTS outside Development, forwarded-header handling, `nosniff`, frame denial, strict referrer policy, permissions policy and API-safe CSP. Swagger is Development-only. CORS is an explicit origin allowlist. Reverse proxies must terminate TLS, replace client forwarding headers, cap request bodies, enforce edge rate limits, and pass the original scheme. CSP for the separate Web host should be tuned against its final API origin.

Endpoint authorization uses server-side identity/merchant claims. Financial and authentication endpoints use partitioned rate-limit policies; new sensitive endpoints must opt in. Development notification providers and reveal features must remain disabled in UAT/Production. Validate uploaded file content signatures, size and metadata before a real storage provider is enabled.

Dependency, secret and container builds run in CI. Perform periodic authorization/tenant-boundary tests, dependency review, restore drills and secret rotation. Current limitation: JWT signing has one active symmetric key; add key identifiers and overlap for zero-downtime rotation later.
