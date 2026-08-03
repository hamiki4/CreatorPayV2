# Implementation Roadmap

## Milestone 1 — Foundation (complete)

Clean-architecture projects, dependency-injection seams, health endpoint, OpenAPI, test projects, frontend shell, PostgreSQL Docker configuration, and initial documentation.

## Milestone 2 — Core domain and persistence (complete)

Core entities and enums, audited UTC base entity, domain transitions, EF Core/Npgsql context and fluent mappings, initial migration, metadata tests, and database/domain documentation.

## Recommended Milestone 3 — Identity onboarding and approval

Implement creator and merchant registration use cases, password hashing, email/phone verification boundaries, Platform Admin approval workflows, DTOs that never expose password hashes, authorization policies, and PostgreSQL-backed integration tests. JWT/login behavior should be designed and threat-modeled as part of that milestone rather than added implicitly here.

## Later milestones

1. Merchant creator discovery, promoter requests, and partnership approval workflows.
2. Permanent creator QR codes and cashier validation of active partnerships.
3. Customer phone verification, merchant prepaid wallets, and configurable commissions/splits.
4. Immediate earning notifications, weekly payouts, fraud controls, and repeat-use approval.
5. Offline cashier workflows and English/Amharic localization.
6. Security, observability, deployment, and operational hardening.
