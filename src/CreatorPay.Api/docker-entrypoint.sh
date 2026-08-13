#!/bin/sh
set -eu

proof_root="${DepositProofStorage__RootPath:-/app/data/deposit-proofs}"
app_uid="${APP_UID:-1654}"

if [ "$(id -u)" = "0" ]; then
  install -d -m 0750 -o "$app_uid" -g "$app_uid" "$proof_root"
  chown -R "$app_uid:$app_uid" "$proof_root"
  chmod 0750 "$proof_root"
  exec gosu "$app_uid:$app_uid" "$@"
fi

exec "$@"
