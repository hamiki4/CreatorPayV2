# Security, Fraud, and Privacy

## Baseline controls

- Hash passwords with an approved adaptive algorithm (Argon2id preferred, or configured bcrypt/PBKDF2), unique salts and upgrade-on-login. Never encrypt passwords.
- Short-lived signed JWT access tokens contain stable subject and minimal role/scope claims. Rotate one-time refresh tokens, store their hashes and token families, detect reuse, and revoke family/session on logout, password/status/security change.
- Enforce permission plus merchant/location resource scope on every server operation; deny by default. Prevent IDOR with scoped queries, not UI hiding.
- Rate-limit login, reset, OTP, QR validation, transaction and export routes by account/device/IP/risk. Add progressive lockout and safe recovery without account enumeration.
- Sign/version QR payloads with managed asymmetric keys, `kid`, purpose and identifier; support rotation/revocation and prohibit embedded PII.
- Protect phones as described in [customer phone](13-customer-phone-and-repeat-use.md): separate encrypted value and keyed HMAC. Encrypt secrets and sensitive fields at rest/in transit; use managed key rotation and least privilege.
- Apply HTTPS/HSTS, CSP, frame/content-type/referrer protections, strict production CORS allowlists, CSRF defenses where cookies are used, schema/size validation, parameterized persistence and output encoding.
- Validate uploads by allowed extension and detected MIME/signature, size/count, malware scan, random private object key and authorized download; never execute uploaded content.
- Mask sensitive UI/API/log data. Audit sensitive reads and all events listed in [roles](05-user-roles-and-permissions.md); make logs append-only and tamper-evident with restricted export.

## Fraud controls

Versioned, configurable rules flag rather than silently rewrite records: phone/creator/cashier/merchant velocity, repeated amounts, rapid/off-hours use, large purchase/commission, wallet churn, QR failures, device/IP/account sharing, location anomalies and payout-destination changes. Thresholds are Platform Admin configured. Alerts have severity, evidence, owner, status, resolution/reason and links; holds/manual review are auditable. Avoid using device/IP alone for adverse action and assess privacy/fairness.

Financial transactions and ledger/audit records are immutable; corrections are linked compensating events. Define retention schedules per data class, legal hold, secure deletion and backup expiry. Legal decisions remain open for Ethiopian data protection, identity consent, cross-border hosting/transfers, tax/financial retention, breach response, KYC/AML and customer marketing consent. Perform threat modeling, dependency/secret scans, SAST/DAST and penetration testing before production.
