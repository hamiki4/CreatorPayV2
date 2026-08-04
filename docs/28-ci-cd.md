# CI/CD

`ci.yml` runs on pull requests and main pushes: restore, release build, all tests (including Docker-backed PostgreSQL Testcontainers), vulnerability listing, formatting verification, frontend build, secret scan and test artifact upload. `container-build.yml` builds API, Worker and Web images without publishing. Both use read-only repository permissions and no production secrets.

Future GHCR publishing should add least-privilege `packages: write`, immutable SHA tags, provenance/signing, environment approval and an explicit push condition. Deployment promotes the same digest Development → Test → UAT → Production, runs reviewed migrations as a separate approved step, checks readiness, and rolls the application image back on failure.
