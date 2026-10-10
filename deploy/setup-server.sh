#!/usr/bin/env bash
# Family OS — one-shot Ubuntu server bootstrap
# Installs Docker, swap, Traefik, Portainer, and the Family OS stack.
#
# On the server (recommended):
#   sudo bash deploy/setup-server.sh --domain example.com --email you@example.com
#
# From your laptop over SSH:
#   bash deploy/setup-server.sh --remote ubuntu@1.2.3.4 --key ~/.ssh/key.pem \
#     --domain example.com --email you@example.com
#
# Optional:
#   --skip-familyos     only edge (Traefik + Portainer)
#   --skip-swap         do not create swap
#   --repo-dir PATH     existing clone (default: /opt/familyos)
#   --branch main
#   --dry-run

set -euo pipefail

DOMAIN=""
ACME_EMAIL=""
REMOTE=""
SSH_KEY=""
REPO_DIR="/opt/familyos"
BRANCH="main"
SKIP_FAMILYOS=0
SKIP_SWAP=0
DRY_RUN=0
REPO_URL="${REPO_URL:-https://github.com/tmassey1979/FamilyOS.git}"

log()  { printf '\n\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

usage() {
  sed -n '2,20p' "$0" | sed 's/^# \?//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --domain) DOMAIN="${2:-}"; shift 2 ;;
    --email) ACME_EMAIL="${2:-}"; shift 2 ;;
    --remote) REMOTE="${2:-}"; shift 2 ;;
    --key) SSH_KEY="${2:-}"; shift 2 ;;
    --repo-dir) REPO_DIR="${2:-}"; shift 2 ;;
    --branch) BRANCH="${2:-}"; shift 2 ;;
    --repo-url) REPO_URL="${2:-}"; shift 2 ;;
    --skip-familyos) SKIP_FAMILYOS=1; shift ;;
    --skip-swap) SKIP_SWAP=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) usage ;;
    *) die "Unknown arg: $1 (try --help)" ;;
  esac
done

run() {
  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf '[dry-run] %s\n' "$*"
  else
    eval "$@"
  fi
}

# ---------- remote mode: copy script + run on host ----------
if [[ -n "$REMOTE" ]]; then
  [[ -n "$DOMAIN" && -n "$ACME_EMAIL" ]] || die "--domain and --email required with --remote"
  SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ServerAliveInterval=30)
  if [[ -n "$SSH_KEY" ]]; then
    [[ -f "$SSH_KEY" ]] || die "SSH key not found: $SSH_KEY"
    SSH_OPTS+=(-i "$SSH_KEY")
  fi
  log "Uploading setup script to $REMOTE"
  SCRIPT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/setup-server.sh"
  remote_args=(--domain "$DOMAIN" --email "$ACME_EMAIL" --repo-dir "$REPO_DIR" --branch "$BRANCH")
  [[ "$SKIP_FAMILYOS" -eq 1 ]] && remote_args+=(--skip-familyos)
  [[ "$SKIP_SWAP" -eq 1 ]] && remote_args+=(--skip-swap)
  [[ "$DRY_RUN" -eq 1 ]] && remote_args+=(--dry-run)

  scp "${SSH_OPTS[@]}" "$SCRIPT_PATH" "${REMOTE}:/tmp/familyos-setup-server.sh"
  # shellcheck disable=SC2029
  ssh "${SSH_OPTS[@]}" "$REMOTE" "sudo bash /tmp/familyos-setup-server.sh $(printf '%q ' "${remote_args[@]}")"
  exit $?
fi

# ---------- local (on server) mode ----------
[[ -n "$DOMAIN" ]] || die "Missing --domain (e.g. family.example.com)"
[[ -n "$ACME_EMAIL" ]] || die "Missing --email (Let's Encrypt contact)"

if [[ "$(id -u)" -ne 0 ]]; then
  die "Run as root on the server: sudo bash $0 --domain ... --email ..."
fi

export DEBIAN_FRONTEND=noninteractive

log "1/8 System packages"
run "apt-get update -y"
run "apt-get upgrade -y"
run "apt-get install -y ca-certificates curl gnupg git openssl apache2-utils"

log "2/8 Docker Engine + Compose plugin"
if ! command -v docker >/dev/null 2>&1; then
  run "install -m 0755 -d /etc/apt/keyrings"
  run "curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg"
  run "chmod a+r /etc/apt/keyrings/docker.gpg"
  # shellcheck disable=SC1091
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
    > /etc/apt/sources.list.d/docker.list
  run "apt-get update -y"
  run "apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin docker-buildx-plugin"
  run "systemctl enable --now docker"
else
  log "Docker already installed: $(docker --version)"
fi

if id ubuntu &>/dev/null; then
  run "usermod -aG docker ubuntu || true"
fi

log "3/8 Swap (2G) for small instances"
if [[ "$SKIP_SWAP" -eq 0 ]]; then
  if ! swapon --show | grep -q .; then
    if [[ ! -f /swapfile ]]; then
      run "fallocate -l 2G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=2048"
      run "chmod 600 /swapfile"
      run "mkswap /swapfile"
    fi
    run "swapon /swapfile || true"
    grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  else
    log "Swap already active"
  fi
  free -h || true
else
  warn "Skipping swap (--skip-swap)"
fi

log "4/8 Clone / update repo → $REPO_DIR"
if [[ -d "$REPO_DIR/.git" ]]; then
  run "git -C '$REPO_DIR' fetch origin"
  run "git -C '$REPO_DIR' checkout '$BRANCH'"
  run "git -C '$REPO_DIR' pull --ff-only origin '$BRANCH' || true"
else
  run "mkdir -p '$(dirname "$REPO_DIR")'"
  run "git clone --branch '$BRANCH' '$REPO_URL' '$REPO_DIR'"
fi
if [[ "$DRY_RUN" -eq 1 ]]; then
  log "Dry-run: skipping cd into $REPO_DIR (clone was simulated)"
else
  cd "$REPO_DIR"
fi

log "5/8 Configure Traefik ACME email + domain files"
TRAEFIK_YML="$REPO_DIR/deploy/traefik/traefik.yml"
if [[ "$DRY_RUN" -eq 1 ]]; then
  log "Dry-run: would set ACME email in traefik.yml"
elif [[ -f "$TRAEFIK_YML" ]]; then
  sed -i -E "s/email: .*/email: ${ACME_EMAIL}/" "$TRAEFIK_YML"
fi

log "6/8 Edge stack: proxy network + Traefik + Portainer"
run "docker network create proxy 2>/dev/null || true"
export DOMAIN
run "docker compose -f deploy/traefik/docker-compose.yml up -d"

log "7/8 Family OS stack"
if [[ "$SKIP_FAMILYOS" -eq 1 ]]; then
  warn "Skipping Family OS (--skip-familyos)"
else
  ENV_FILE="$REPO_DIR/deploy/familyos/.env"
  if [[ ! -f "$ENV_FILE" ]]; then
    cp "$REPO_DIR/deploy/familyos/.env.example" "$ENV_FILE"
    # Generate secrets
    PG_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 32)"
    RQ_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 32)"
    KC_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)"
    cat > "$ENV_FILE" <<EOF
DOMAIN=${DOMAIN}
ACME_EMAIL=${ACME_EMAIL}
POSTGRES_USER=familyos
POSTGRES_PASSWORD=${PG_PASS}
POSTGRES_DB=familyos
RABBITMQ_USER=familyos
RABBITMQ_PASSWORD=${RQ_PASS}
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=${KC_PASS}
VERSION=0.1.0
SEED_ENABLED=true
TZ=UTC
EOF
    chmod 600 "$ENV_FILE"
    log "Wrote $ENV_FILE (passwords auto-generated, mode 600)"
    log "Keycloak admin password saved in deploy/familyos/.env"
  else
    log "Using existing $ENV_FILE"
    # ensure DOMAIN matches
    sed -i -E "s/^DOMAIN=.*/DOMAIN=${DOMAIN}/" "$ENV_FILE"
  fi

  run "docker compose -f deploy/familyos/docker-compose.yml --env-file deploy/familyos/.env up -d --build"
fi

log "8/8 Status"
run "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"

cat <<EOF

────────────────────────────────────────────────────────
  Family OS edge setup finished

  Portainer:  https://portainer.${DOMAIN}
  Traefik:    https://traefik.${DOMAIN}
  API:        https://api.${DOMAIN}/health
  Keycloak:   https://auth.${DOMAIN}

  DNS A records must point to this server's public IP:
    portainer  api  auth  traefik  (and apex if used)

  Secrets file: ${REPO_DIR}/deploy/familyos/.env
  (download/backup securely; do not commit)

  First Portainer visit: create the admin user in the UI.
────────────────────────────────────────────────────────
EOF
