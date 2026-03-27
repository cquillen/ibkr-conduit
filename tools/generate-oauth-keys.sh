#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-.}"

# Validate OpenSSL is available
if ! command -v openssl &>/dev/null; then
    echo "ERROR: OpenSSL is required but not found on PATH." >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo "Generating IBKR OAuth key material..."

# Signature key pair (RSA 2048)
openssl genrsa -out "$OUTPUT_DIR/private_signature.pem" 2048
openssl rsa -in "$OUTPUT_DIR/private_signature.pem" -pubout -out "$OUTPUT_DIR/public_signature.pem"

# Encryption key pair (RSA 2048)
openssl genrsa -out "$OUTPUT_DIR/private_encryption.pem" 2048
openssl rsa -in "$OUTPUT_DIR/private_encryption.pem" -pubout -out "$OUTPUT_DIR/public_encryption.pem"

# Diffie-Hellman parameters (2048-bit)
openssl dhparam -out "$OUTPUT_DIR/dhparam.pem" 2048

echo ""
echo "=== Key generation complete ==="
echo ""
echo "Upload these 3 files to the IBKR Self-Service Portal:"
echo "  - $OUTPUT_DIR/public_signature.pem"
echo "  - $OUTPUT_DIR/public_encryption.pem"
echo "  - $OUTPUT_DIR/dhparam.pem"
echo ""
echo "Keep these 2 files PRIVATE (never commit them):"
echo "  - $OUTPUT_DIR/private_signature.pem"
echo "  - $OUTPUT_DIR/private_encryption.pem"
