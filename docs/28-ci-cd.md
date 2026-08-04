# CI/CD

`ci.yml` runs on pull requests and main pushes: restore, release build, all tests (including Docker-backed PostgreSQL Testcontainers), .NET/npm vulnerability checks, formatting and EF migration discovery, frontend build, secret scan and test artifact upload. `container-build.yml` builds API, Worker and non-root Web images without publishing, emits an SBOM, and fails on fixable Critical/High container findings. Both use read-only repository permissions and no production secrets.

Registry publishing remains provider work and must add least-privilege `packages: write`, immutable SHA tags/digests, provenance/signing, and an explicit protected push condition. `promote.yml` validates an immutable digest, serializes environment changes, requires a change/UAT evidence ID, and uses protected GitHub environments. Configure required reviewers and prevent self-review. A cloud adapter must promote that same digest Development → Test → UAT → Production, run reviewed migrations as a separate approved identity, check readiness, and roll the application image back on failure.

The repository contains no cloud credentials or automatic production deployment. See [Milestone 20 readiness](33-security-and-production-readiness.md) for evidence gates.
