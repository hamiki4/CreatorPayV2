# Merchant Organization and Staff

Milestone 7 introduces the merchant organization boundary: one approved Merchant Admin manages locations, supervisors, and cashiers belonging to the merchant in the authenticated `merchant_id` claim.

## Locations

Locations require a name, address line 1, city, two-letter country code, and valid system time-zone ID. Active names are unique per merchant. Locations are activated/deactivated, never hard-deleted; assignments remain for history. Only active same-merchant locations may be selected for a new or changed assignment.

## Staff and permissions

Supervisors and cashiers belong to exactly one merchant and may have multiple location assignments. Assignment rows are retained and marked inactive when removed. A cashier can have at most one active primary assignment. Merchant Admins manage staff; supervisors and cashiers can only read their own profile and assigned locations. Cashier location responses exclude inactive locations. Platform Admin support access remains available through the database/audit surface; a full support UI is deferred.

## Invitation lifecycle

The Merchant Admin creates a disabled staff profile and a Supervisor or Cashier invitation. Only a SHA-256 token hash is stored. The raw opaque token is returned once when the development setting is enabled and is never audited. Invitations expire after 48 hours by default, are single-use, and can be revoked. Acceptance validates the existing password policy and atomically creates an active correctly-linked `UserAccount`, activates the staff profile, and consumes the invitation. Email is normalized and globally unique. Merchant Admins never choose or see the staff password.

Invalid, expired, used, and revoked invitation attempts return distinct safe messages. No password, password hash, raw invitation token, or token hash appears in DTOs or audit events.

## API

- `/api/v1/merchant/locations` and `/{locationId}` provide list/create/read/update/status operations.
- `/api/v1/merchant/supervisors` and `/cashiers` provide list/read/update, invitation, assignment, and status operations.
- `POST /api/v1/staff-invitations/accept` accepts an invitation; `POST /api/v1/merchant/staff-invitations/{id}/revoke` revokes one.
- `/api/v1/supervisor/me`, `/supervisor/locations`, `/cashier/me`, and `/cashier/locations` are read-only self-service endpoints.

All management routes require `MerchantAdmin`. Merchant scope comes from authenticated claims, not request bodies. Staff routes require the exact staff role and linked staff ID.

## UI and limitations

The responsive React organization workspace includes location list/create/edit/status, staff list/invite/details/assignments/status, invitation acceptance, loading/empty/error states, and staff profile/location views. English strings are centralized as a localization foundation so Amharic resources can be added without changing routing.

This milestone deliberately excludes partnerships, QR scanning, commissions, wallets, transactions, customer verification, earnings, payouts, real email delivery, offline synchronization, and a full Platform Admin organization UI.

## Manual validation

```powershell
dotnet restore CreatorPay.slnx
dotnet build CreatorPay.slnx
dotnet test CreatorPay.slnx
npm run build --prefix src/CreatorPay.Web
dotnet ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
git diff --check
git status --short
```
