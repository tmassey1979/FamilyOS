#!/usr/bin/env bash
# One-shot local launch for Family OS developers.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Family OS dev stack"
echo "    Root: $ROOT"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required. Install Docker Desktop or Engine, then retry."
  exit 1
fi

export VERSION="${VERSION:-0.1.0}"

echo "==> Building and starting Postgres, RabbitMQ, Keycloak, API (v$VERSION)"
docker compose up --build -d

echo "==> Waiting for API health..."
for i in $(seq 1 60); do
  if curl -fsS "http://localhost:5080/health" >/dev/null 2>&1; then
    echo "API is healthy."
    break
  fi
  if [ "$i" -eq 60 ]; then
    echo "API did not become healthy in time. Logs:"
    docker compose logs --tail=80 api
    exit 1
  fi
  sleep 2
done

echo ""
echo "Stack is up:"
echo "  API / Swagger : http://localhost:5080/swagger"
echo "  Health        : http://localhost:5080/health"
echo "  Keycloak      : http://localhost:8080  (admin / admin)"
echo "  RabbitMQ UI   : http://localhost:15672 (familyos / familyos_dev_password)"
echo "  Postgres      : localhost:5432         (familyos / familyos_dev_password)"
echo ""
echo "Mobile: set EXPO_PUBLIC_API_URL to http://<your-lan-ip>:5080"
echo "Stop:   docker compose down"
echo "Wipe:   docker compose down -v"
