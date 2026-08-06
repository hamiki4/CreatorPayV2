# Cloudflare Free configuration for the private Pilot

Create proxied `A`/`AAAA` records for `pilot.weymela.com` and `api-pilot.weymela.com` pointing to the Hetzner origin. Use a Cloudflare Origin CA certificate on the host proxy or a valid public certificate, set SSL/TLS to **Full (strict)**, enable Always Use HTTPS and Automatic HTTPS Rewrites, and test both hostnames. WebSockets require no application-specific rule for the checkout SignalR hub, but verify upgrade traffic end to end.

Use available Free-plan managed protections, bot controls and security level conservatively; validate legitimate sign-in/checkout traffic. Cloudflare controls supplement, not replace, application rate limits. Free-plan rate-limiting/WAF availability and quotas can change and are not sufficient for exact per-account financial controls.

Cache only versioned static Web assets. Create bypass/no-cache rules for `api-pilot.weymela.com/*`, `/api/*`, `/hubs/*`, authenticated/session responses, HTML application routes, Help form submissions and anything with `Authorization`, `Set-Cookie` or `Cache-Control: no-store`. Never cache health responses as evidence of origin readiness. Purge static content deliberately after deployment.

Restrict origin TCP 80/443 to current Cloudflare published IP ranges where operationally maintainable; retain a tested emergency administrator path. Authenticate origin traffic with strict TLS and, where available, an origin pull control. Do not proxy SSH, PostgreSQL, Docker or internal ports. Non-HTTP records must be DNS-only and separately firewalled; no database record should exist. Record DNS/SSL changes, owners and rollback values without credentials.

