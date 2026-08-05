#!/bin/sh
# Picks plain-HTTP or HTTPS+redirect nginx config based on whether a Tailscale
# cert has been issued on the host (see deploy/tailscale-https-setup.sh) —
# nothing to configure by hand either way, `docker compose up` just works.
set -e

CERT_FILE=/etc/nginx/certs/tls.crt
KEY_FILE=/etc/nginx/certs/tls.key

if [ -f "$CERT_FILE" ] && [ -f "$KEY_FILE" ]; then
  echo "[hydraforge] TLS cert found — serving HTTPS on 443 (+ redirect from 80)."
  TEMPLATE=/etc/nginx/hydraforge-templates/https.conf.template
else
  echo "[hydraforge] No TLS cert at $CERT_FILE — serving plain HTTP on 80 only."
  echo "[hydraforge] Run deploy/tailscale-https-setup.sh on the host to enable HTTPS."
  TEMPLATE=/etc/nginx/hydraforge-templates/http.conf.template
fi

# Match the official nginx image's own envsubst-on-templates behavior: only
# substitute names that are actually set as environment variables, so nginx's
# own runtime variables ($host, $scheme, $http_upgrade, etc.) in the template
# pass through untouched instead of being (incorrectly) treated as unset env
# vars and blanked out.
DEFINED_ENVS=$(printf '${%s} ' $(env | cut -d= -f1))
envsubst "$DEFINED_ENVS" < "$TEMPLATE" > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
