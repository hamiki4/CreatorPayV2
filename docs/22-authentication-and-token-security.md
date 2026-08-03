# Authentication and token security

Milestone 4 adds API authentication only; registration, approval, MFA, OTP, social login, and business workflows remain out of scope.

## Login and passwords

Only `Active` accounts can authenticate. Pending, suspended, rejected, closed, and draft accounts receive the same generic failure as invalid credentials. Five consecutive failures lock an account for 15 minutes by default; a successful login clears the counters. These values are configurable under `Authentication:Lockout`.

Passwords use ASP.NET Core `PasswordHasher<UserAccount>` and are never returned or logged. The configurable default policy is 10–128 characters with uppercase, lowercase, number, and non-alphanumeric requirements plus a small common-password deny list. Hashes are upgraded on successful verification when the framework requests rehashing.

## Tokens

Access tokens are signed JWTs validated for issuer, audience, signature, and lifetime. The default lifetime is 15 minutes. Claims are limited to user ID, email, role, `jti`, issued-at/expiry, and applicable creator/merchant/supervisor/cashier IDs.

Refresh tokens are 64 random bytes, returned once, and stored only as SHA-256 hashes. They default to 30 days. Refresh is performed in a serializable PostgreSQL transaction; the old token is consumed and replaced in the same family. Use of an expired, used, or revoked known token revokes the entire family. Logout is idempotent; logout-all, password change, and password reset revoke all sessions.

Password-reset tokens are also random, hashed at rest, single-use, and expire after 30 minutes by default. Forgot-password always returns the same response. The current notifier is a safe placeholder that logs only the user ID and never the token; no delivery is performed.

## Endpoints and authorization

All endpoints live under `/api/v1/auth`: `login`, `refresh`, `logout`, `logout-all`, `me`, `change-password`, `forgot-password`, and `reset-password`. Sensitive anonymous endpoints use per-IP fixed-window rate limiting. JWT policies are `AuthenticatedUser`, one `<Role>Only` policy for every role, and `MerchantOperations` for merchant admin, supervisor, or cashier.

`ICurrentUserService` exposes typed identity and scope IDs. Future application services must treat these claims as a starting point, load current assignments from persistence, and enforce merchant/location ownership server-side. Claims alone must not authorize access to a mutable assignment.

## Configuration and risks

Set `Authentication__Jwt__SigningKey` to at least 32 random characters through environment/secret configuration. Never use the development placeholder outside local development. JSON token delivery supports the current SPA architecture but browser storage is vulnerable to script injection; evaluate an `HttpOnly`, `Secure`, `SameSite` refresh-token cookie before production.

OpenAPI is development-only. No production passwords are seeded. PostgreSQL integration tests are not yet containerized; EF metadata is tested separately, so transaction and unique-index behavior should also be validated against PostgreSQL in CI.

## Manual checks

```powershell
dotnet restore CreatorPay.slnx
dotnet build CreatorPay.slnx
dotnet test CreatorPay.slnx
npm run build --prefix src/CreatorPay.Web
dotnet ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
```
