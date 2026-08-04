# Platform Approval Workflows

Platform approval establishes platform eligibility only. It never creates or approves a `MerchantCreatorPartnership`.

## Review procedure

1. A verified Creator or Merchant enters `PendingApproval`; a queue snapshot records submitted data/documents.
2. An authorized Platform Admin claims/reviews it, recording checks without downloading unnecessary personal data.
3. Approve sets entity/account `Active`, reviewer and UTC timestamp. Reject requires a category and user-safe reason. A material document problem may instead request resubmission.
4. The outbox receives the result notification in the same transaction; the audit history stores actor, decision, reason and version.

Suspension requires reason/evidence and immediately blocks new eligible transactions while preserving sign-in needed for remediation where policy allows. Reactivation reruns applicable checks. Closure revokes sessions and preserves immutable financial/audit history. A rejected or resubmission-required applicant may submit a new document version; prior versions and decisions remain linked and immutable.

| Decision | Preconditions | Result | Important failure |
|---|---|---|---|
| Approve creator | PendingApproval; checks complete | Creator/account Active; zero merchant approvals | stale review version, missing check |
| Approve merchant | PendingApproval; checks complete | Merchant/account Active | invalid/expired document |
| Reject | PendingApproval | Rejected with reason | blank reason |
| Suspend | Active or merchant LowBalanceRestricted | Suspended | unresolved in-flight operation is safely completed/rejected |
| Reactivate | Suspended; remediation complete | Active (wallet restriction may be recalculated) | unresolved compliance issue |
| Close | authorized request; obligations handled | Closed | unsettled funds require controlled closure workflow |

Acceptance: decisions are idempotent, concurrency-safe, notified, filterable in audit history, and authorization-tested; no decision endpoint can silently create a partnership.

## Milestone 5 creator transitions

The implemented graph is `Draft → PendingVerification → PendingApproval → Active`, with `PendingApproval → Rejected`, `Active → Suspended`, and `Suspended → Active`. Approval requires both verification flags. Rejection and suspension require a reason. Every decision updates the linked account and writes an audit event with actor, UTC time, type, and safe reason. Only `PlatformAdminOnly` reaches queue, detail, and decision endpoints. No decision creates a partnership.

Decision retries currently return validation failure outside the required source state; optimistic ETag/version handling and a durable notification outbox remain future hardening work.
