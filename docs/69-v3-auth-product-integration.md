# V3-managed Weymela product sessions

This integration branch preserves the committed CreatorPayV2 product presentation while making Weymela V3 the authentication authority for externally managed principals. V3External users cannot use the legacy V2 password, password recovery, PIN, Firebase link, or refresh-token paths.

The browser begins at the V2-derived API, which creates an HttpOnly, Secure, SameSite Strict login-CSRF state cookie and redirects to V3 profile selection. V3 returns only a 30–60 second opaque one-time code. The V2-derived API redeems it server-to-server, maps the immutable V3 user and selected workspace, and issues an independent HttpOnly product session. Cookie-backed mutations enforce the configured product Web origin; integration onboarding, profile switching, and logout also require the explicit browser request marker.

Configuration is disabled by default. A separate integration Pilot must supply matching issuer, audience, environment, callback ID, client ID, and protected client secret, plus fixed HTTPS V3 API, V3 Web, and product Web origins. `PlatformAdminUserAccountId` is the reviewed V2 Admin principal mapped to the existing V3 Platform Admin; no email or phone auto-linking is permitted.

The source migration `AddV3ExternalIdentityIntegration` adds private external identity, profile-link, and application-session tables plus the `AuthenticationSource` discriminator. Existing accounts default to `Local`. It contains no Promotion, finance, QR, social, or payout changes. Rehearse it against a disposable restored database before any isolated Pilot authorization.

An integration Pilot requires V3 API/Web and this V2-derived API/Web with separate databases. No direct dependency on V2 Production is allowed. Preserve exact prior image digests and verified database backups. Previous application images can ignore the additive schema, but exact forward-schema compatibility must be rehearsed before image rollback. Do not run the destructive down migration or restore a live database without separate authorization.
