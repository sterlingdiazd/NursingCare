#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CA_DIR="$ROOT_DIR/nginx/ca"
CERT_DIR="$ROOT_DIR/nginx/certs"
HOSTNAME="${1:-}"
PUBLIC_PORT="${2:-5050}"

if [[ -z "$HOSTNAME" ]]; then
  echo "Usage: $0 <public-host-or-ip> [public-port]" >&2
  exit 1
fi

is_ipv4=false
if [[ "$HOSTNAME" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
  is_ipv4=true
fi

mkdir -p "$CA_DIR" "$CERT_DIR"

CA_KEY="$CA_DIR/rootCA.key"
CA_CERT="$CA_DIR/rootCA.pem"
SERVER_KEY="$CERT_DIR/server.key"
SERVER_CERT="$CERT_DIR/server.crt"
SERVER_CSR="$CERT_DIR/server.csr"
OPENSSL_CONFIG="$CERT_DIR/openssl.cnf"

if [[ ! -f "$CA_KEY" || ! -f "$CA_CERT" ]]; then
  openssl genrsa -out "$CA_KEY" 4096 >/dev/null 2>&1
  openssl req -x509 -new -nodes -key "$CA_KEY" -sha256 -days 3650 \
    -out "$CA_CERT" \
    -subj "/CN=NursingCare Local Dev Root CA" >/dev/null 2>&1
fi

alt_names=$'[alt_names]\nDNS.1 = localhost\nIP.1 = 127.0.0.1\nIP.2 = ::1'

if [[ "$is_ipv4" == true ]]; then
  alt_names+=$'\nIP.3 = '"${HOSTNAME}"
else
  alt_names+=$'\nDNS.2 = '"${HOSTNAME}"
fi

cat > "$OPENSSL_CONFIG" <<EOF
[req]
distinguished_name = req_distinguished_name
prompt = no

[req_distinguished_name]
CN = ${HOSTNAME}

[v3_req]
keyUsage = critical,digitalSignature,keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

${alt_names}
EOF

openssl genrsa -out "$SERVER_KEY" 2048 >/dev/null 2>&1
openssl req -new -key "$SERVER_KEY" -out "$SERVER_CSR" -config "$OPENSSL_CONFIG" >/dev/null 2>&1
openssl x509 -req -in "$SERVER_CSR" -CA "$CA_CERT" -CAkey "$CA_KEY" -CAcreateserial \
  -out "$SERVER_CERT" -days 365 -sha256 -extensions v3_req -extfile "$OPENSSL_CONFIG" >/dev/null 2>&1

rm -f "$SERVER_CSR" "$OPENSSL_CONFIG"

cat <<EOF
Generated dev TLS assets:
  Root CA:     $CA_CERT
  Server cert: $SERVER_CERT
  Server key:  $SERVER_KEY

Trust the root CA on your Mac and iPhone, then use:
  https://localhost:${PUBLIC_PORT}
  https://${HOSTNAME}:${PUBLIC_PORT}
EOF
