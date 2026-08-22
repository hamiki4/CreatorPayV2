#!/bin/sh
set -eu

proof_root="${DepositProofStorage__RootPath:-/app/data/deposit-proofs}"
profile_photo_root="${CreatorProfilePhotos__RootPath:-/app/data/profile-photos}"
app_uid="${APP_UID:-1654}"

if [ "$(id -u)" = "0" ]; then
  install -d -m 0750 -o "$app_uid" -g "$app_uid" "$proof_root"
  chown -R "$app_uid:$app_uid" "$proof_root"
  chmod 0750 "$proof_root"
  install -d -m 0750 -o "$app_uid" -g "$app_uid" "$profile_photo_root"
  chown -R "$app_uid:$app_uid" "$profile_photo_root"
  chmod 0750 "$profile_photo_root"
  exec gosu "$app_uid:$app_uid" "$@"
fi

exec "$@"
