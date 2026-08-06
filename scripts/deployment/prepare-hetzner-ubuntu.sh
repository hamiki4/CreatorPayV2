#!/usr/bin/env bash
set -euo pipefail
if [[ ${EUID} -ne 0 ]]; then echo 'Run as root on a new Ubuntu LTS server.' >&2; exit 1; fi
DEPLOY_USER=${DEPLOY_USER:-weymela}
[[ ${DEPLOY_USER} =~ ^[a-z_][a-z0-9_-]*$ ]] || { echo 'Invalid DEPLOY_USER.' >&2; exit 1; }
id "${DEPLOY_USER}" >/dev/null 2>&1 || adduser --disabled-password --gecos '' "${DEPLOY_USER}"
usermod -aG sudo "${DEPLOY_USER}"
install -d -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" -m 0750 /opt/weymela /opt/weymela/backups
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl fail2ban postgresql-client unattended-upgrades ufw
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
curl -fsSLo /tmp/packages-microsoft-prod.deb "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb"
dpkg -i /tmp/packages-microsoft-prod.deb
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" >/etc/apt/sources.list.d/docker.list
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin dotnet-sdk-10.0 powershell
usermod -aG docker "${DEPLOY_USER}"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
systemctl enable --now docker fail2ban unattended-upgrades
echo 'Server prepared. Add an SSH public key, test a second session, then disable password/root SSH login manually.'
