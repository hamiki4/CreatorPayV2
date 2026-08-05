# Merchant onboarding

Registration collects business identity, primary contact, phone, email, zone, city, address, optional tax/registration numbers, locale, password, and terms acceptance. It never collects banking credentials or posts finances.

Submission creates a `PendingReview` merchant and a `PendingVerification` Merchant Admin atomically. Duplicate email/phone checks apply. Approval remains blocked until development-provider email and phone verification completes. Review outcomes are active, rejected, correction requested, suspended, and reactivated; decisions are audited.
