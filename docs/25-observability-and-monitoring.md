# Observability and monitoring

CreatorPay emits structured JSON console logs with scopes for correlation, request, user, merchant, endpoint, status, duration, background job, instance and provider. `X-Correlation-ID` accepts only 1–100 safe ASCII identifier characters and is returned in responses and Problem Details. Sensitive authentication, phone, payment, QR and cryptographic values are prohibited from logs.

`CreatorPayTelemetry` provides an OpenTelemetry-compatible `ActivitySource`, `Meter`, authentication/rate-limit counters and a database-duration histogram. Configuration reserves `Observability:EnableOtlpExporter` and `OtlpEndpoint`; an exporter is optional and no collector is needed locally. Instrument purchase, wallet, notification, payout, outbox and job counters at service boundaries as provider implementations mature.

Alert on readiness failures, sustained 5xx/429 rates, latency percentiles, outbox age/count, dead letters, failed jobs, failed payouts and PostgreSQL connection/lock/storage pressure. Correlate logs, traces, and metrics with correlation ID and deployment version.
# Milestone 18 operational alerts

Operational alerts use a cooldown key and unresolved-state uniqueness to prevent duplicate alert storms. State transitions preserve history and correlation IDs. Admin system responses expose readiness labels and counts, not secrets or connection strings.
