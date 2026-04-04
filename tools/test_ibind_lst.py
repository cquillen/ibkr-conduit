#!/usr/bin/env python3
"""Quick test: use ibind's OAuth code directly to get an LST."""
import sys
sys.path.insert(0, '/workspace/ibind')

from ibind.oauth.oauth1a import (
    prepare_oauth, generate_oauth_headers, calculate_live_session_token,
    validate_live_session_token, OAuth1aConfig
)
import requests

# Load secrets (trimmed)
consumer_key = open('/workspace/ibkr-conduit/tools/cusumer_key').read().strip()
access_token = open('/workspace/ibkr-conduit/tools/private_access_token').read().strip()
access_token_secret = open('/workspace/ibkr-conduit/tools/private_access_token_secret').read().strip()
sig_key = open('/workspace/ibkr-conduit/tools/private_signature.pem').read()
enc_key = open('/workspace/ibkr-conduit/tools/private_encryption.pem').read()

# Extract DH prime hex from dhparam.pem using openssl
import subprocess
result = subprocess.run(
    ['openssl', 'dhparam', '-in', '/workspace/ibkr-conduit/tools/dhparam.pem', '-text', '-noout'],
    capture_output=True, text=True
)
lines = result.stdout.split('\n')
hex_lines = []
in_prime = False
for line in lines:
    if 'P:' in line:
        in_prime = True
        continue
    if 'G:' in line:
        break
    if in_prime:
        hex_lines.append(line.strip().replace(':', ''))
dh_prime = ''.join(hex_lines)
# Remove leading 00 if present (two's complement padding)
if dh_prime.startswith('00'):
    dh_prime = dh_prime[2:]

print(f"Consumer key: {consumer_key}")
print(f"Access token: {access_token}")
print(f"DH prime (first 20): {dh_prime[:20]}...")
print(f"Access token secret (first 20): {access_token_secret[:20]}...")

config = OAuth1aConfig(
    consumer_key=consumer_key,
    access_token=access_token,
    access_token_secret=access_token_secret,
    signature_key=sig_key,
    encryption_key=enc_key,
    dh_prime=dh_prime,
)

base_url = 'https://api.ibkr.com/v1/api/'
endpoint = config.live_session_token_endpoint

prepend, extra_headers, dh_random = prepare_oauth(config)
print(f"\nPrepend (first 20): {prepend[:20]}...")
print(f"DH challenge (first 20): {extra_headers['diffie_hellman_challenge'][:20]}...")

headers = generate_oauth_headers(
    oauth_config=config,
    request_method='POST',
    request_url=f'{base_url}{endpoint}',
    extra_headers=extra_headers,
    signature_method='RSA-SHA256',
    prepend=prepend,
)

print(f"\n=== REQUEST ===")
print(f"URL: {base_url}{endpoint}")
for k, v in headers.items():
    display = v[:100] + '...' if len(v) > 100 else v
    print(f"  {k}: {display}")

try:
    resp = requests.post(
        f'{base_url}{endpoint}',
        headers=headers,
        proxies={'https': 'http://127.0.0.1:3128'},
    )
    print(f"\n=== RESPONSE ===")
    print(f"Status: {resp.status_code}")
    print(f"Body: {resp.text}")

    if resp.status_code == 200:
        data = resp.json()
        lst = calculate_live_session_token(
            dh_prime=dh_prime,
            dh_random_value=dh_random,
            dh_response=data['diffie_hellman_response'],
            prepend=prepend,
        )
        print(f"\nLST: {lst[:20]}...")
        valid = validate_live_session_token(lst, data['live_session_token_signature'], consumer_key)
        print(f"Valid: {valid}")
except Exception as e:
    print(f"Error: {e}")
