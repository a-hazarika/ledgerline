#!/usr/bin/env bash
#
# Drops the Postgres volume and rebuilds it from db/init, then clears Mailpit.
# Everything created since the last reset is lost.
#
set -euo pipefail

cd "$(dirname "$0")/.."

echo "==> stopping stack and removing the database volume"
docker compose down -v

echo "==> starting postgres and mailpit"
docker compose up -d postgres mailpit

echo "==> waiting for postgres"
until docker compose exec -T postgres pg_isready -U ledgerline -d ledgerline >/dev/null 2>&1; do
  sleep 1
done

echo "==> starting api and web"
docker compose up -d api web

echo "done. app: http://localhost:5173   mail: http://localhost:8025"
