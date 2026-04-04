#!/bin/bash
# Source this file to set IBKR E2E environment variables
# Usage: source tools/set-e2e-env.sh

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CREDS_DIR="$REPO_ROOT/.creds"

export IBKR_CONSUMER_KEY="$(cat "$CREDS_DIR/cusumer_key" | tr -d '[:space:]')"
export IBKR_ACCESS_TOKEN="$(cat "$CREDS_DIR/private_access_token" | tr -d '[:space:]')"
export IBKR_ACCESS_TOKEN_SECRET="$(cat "$CREDS_DIR/private_access_token_secret" | tr -d '[:space:]')"
export IBKR_SIGNATURE_KEY="$(cat "$CREDS_DIR/private_signature.pem" | base64 -w 0)"
export IBKR_ENCRYPTION_KEY="$(cat "$CREDS_DIR/private_encryption.pem" | base64 -w 0)"
export IBKR_DH_PRIME="$(openssl dhparam -in "$CREDS_DIR/dhparam.pem" -text 2>/dev/null | sed -n '/P:/,/G:/p' | head -n -1 | tail -n +2 | tr -d ' :\n')"
export IBKR_FLEX_TOKEN="$(cat "$CREDS_DIR/flex_token" | tr -d '[:space:]')"
export IBKR_FLEX_QUERY_ID="$(cat "$CREDS_DIR/flex_query_id" | tr -d '[:space:]')"
