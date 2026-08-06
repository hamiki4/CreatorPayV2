# Hetzner Cloud single-server private Pilot

Use Nuremberg (`nbg1`) preferably, or Falkenstein (`fsn1`), with current Ubuntu LTS. Start with a shared/general-purpose instance around 4 vCPU, 8 GB RAM and 80 GB SSD; validate against measured Pilot usage and scale before sustained CPU, memory or I/O exceeds 70%. A small Pilot should expect low tens of participants, single-digit steady requests/second, under 20 concurrent users and modest database growth; these are planning assumptions, not measured capacity. Hetzner pricing changes, so confirm the current monthly server, IPv4, volume, snapshot and traffic prices before approval.

## Provisioning and network

Create a dedicated project, server, SSH key and private network. Attach a Hetzner firewall allowing TCP 80/443 globally and TCP 22 only from named administrator IPs; allow required outbound DNS/NTP/HTTPS. Never expose 5432, 8080, 8081 or Docker daemon ports. Run `scripts/deployment/prepare-hetzner-ubuntu.sh`, add the operator SSH key to `weymela`, verify a second key-only session, then set `PermitRootLogin no`, `PasswordAuthentication no`, `KbdInteractiveAuthentication no` and reload sshd. Retain console/rescue access before changing SSH.

Clone the reviewed candidate into `/opt/weymela/app` as the non-root deployment user. Put mode-0600 `.env.pilot` at `/opt/weymela/.env.pilot`; never keep it in Git or shell history. The Pilot override binds PostgreSQL, API and Web to loopback only. Install a host reverse proxy (Caddy or nginx) for `pilot.weymela.com` to `127.0.0.1:8081` and `api-pilot.weymela.com` to `127.0.0.1:8080`. Set `ReverseProxy__KnownProxies__0` to the exact Docker bridge peer observed by the API and verify forwarded HTTPS without a redirect loop.

## Storage and operations

The named PostgreSQL volume is persistent but not a backup. Keep checksum dumps in `/opt/weymela/backups` on restricted storage, encrypt and copy them to an independent EU off-host location with retention and restore tests. Set Docker `json-file` rotation (for example 10 MB, five files), journal retention and disk alerts. Enable unattended security upgrades with reboot coordination. Monitor host reachability, CPU, memory, disk/inodes, I/O, TLS expiry, container state, API readiness, Worker health-file freshness, PostgreSQL and backup age; page a real contact.

Deploy from the repository root with PowerShell 7 using `scripts/deployment/deploy-pilot.ps1`. It validates placeholders/Compose, backs up an existing database, migrates, starts API/Worker/Web and checks health. It does not delete data or restore automatically. Afterward complete the authenticated financial smoke test and record the candidate SHA/image identifiers.

## Recovery and replacement

For application failure, enable checkout maintenance, preserve logs/evidence, stop Worker if financial integrity is uncertain, deploy the previous reviewed code/image against the compatible schema and reconcile before reopening. For host loss, create a new hardened server in `nbg1`/`fsn1`, restore only a checksum-verified backup into an isolated database, apply compatible migrations, reconcile all ledgers/outbox/audits, update the Cloudflare origin, verify TLS/health/smoke checks, then approve traffic. Never destroy the failed server or only backup until evidence and recovery approval are complete. Measure and record RPO/RTO during the drill.

