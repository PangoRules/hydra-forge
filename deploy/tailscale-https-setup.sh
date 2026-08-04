#!/usr/bin/env bash
# Issues (or renews) a Tailscale-signed HTTPS cert for this machine's own tailnet
# hostname and reloads nginx to pick it up. Auto-detects the hostname — nothing
# in this script or the files it writes is specific to any one deployment, so any
# clone of this repo with Tailscale installed can run it as-is.
#
# First-time setup:  sudo ./deploy/tailscale-https-setup.sh
# Renewal (cron/systemd, see hydraforge-cert-renew.timer): same command, idempotent —
# tailscale cert is a no-op if the existing cert still has plenty of validity left.
set -euo pipefail

CERT_DIR=/etc/hydraforge/tailscale-certs
COMPOSE_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/docker-compose.yml"

if ! command -v tailscale >/dev/null 2>&1; then
  echo "tailscale CLI not found — install Tailscale on this host first: https://tailscale.com/download" >&2
  exit 1
fi

if ! tailscale status >/dev/null 2>&1; then
  echo "tailscale is installed but not connected — run 'tailscale up' first." >&2
  exit 1
fi

HOSTNAME="$(tailscale status --json | jq -r '.Self.DNSName' | sed 's/\.$//')"
if [ -z "$HOSTNAME" ] || [ "$HOSTNAME" = "null" ]; then
  echo "Could not auto-detect this machine's tailnet hostname from 'tailscale status --json'." >&2
  exit 1
fi

echo "Detected tailnet hostname: $HOSTNAME"

# Owned by the invoking user (not root) so 'tailscale cert' can write here without
# sudo once 'tailscale set --operator=$USER' has been run — this is the one sudo
# touchpoint in the whole script, and only needed the first time or if ownership
# ever drifts back to root.
if [ ! -d "$CERT_DIR" ] || [ ! -w "$CERT_DIR" ]; then
  sudo install -d -o "$(id -un)" -g "$(id -gn)" -m 755 "$CERT_DIR"
fi

if ! tailscale cert --cert-file "$CERT_DIR/tls.crt" --key-file "$CERT_DIR/tls.key" "$HOSTNAME"; then
  cat >&2 <<EOF

Cert issuance failed. If the error above mentions HTTPS certs not being
enabled, turn them on once for your tailnet in the admin console:
  https://login.tailscale.com/admin/dns  ->  "HTTPS Certificates" -> Enable

Then re-run this script.
EOF
  exit 1
fi

chmod 600 "$CERT_DIR/tls.key"
chmod 644 "$CERT_DIR/tls.crt"

echo "Cert written to $CERT_DIR (expires: $(openssl x509 -in "$CERT_DIR/tls.crt" -noout -enddate | cut -d= -f2))"

if docker compose -f "$COMPOSE_FILE" ps nginx --status running >/dev/null 2>&1 \
  && [ -n "$(docker compose -f "$COMPOSE_FILE" ps nginx --status running -q 2>/dev/null)" ]; then
  docker compose -f "$COMPOSE_FILE" exec -T nginx nginx -s reload
  echo "nginx reloaded."
else
  echo "nginx isn't running yet — it'll pick up the cert on next 'docker compose up'."
fi

echo
echo "Your HTTPS URL is: https://$HOSTNAME:8443"
echo "Set these in your .env before starting the stack (see .env.example):"
echo "  CORS_ALLOWED_ORIGINS=https://$HOSTNAME:8443"
echo "  NUXT_PUBLIC_AUTH_COOKIE_SECURE=true"
