#!/usr/bin/env python3
"""Test ibind's WebSocket implementation with our paper credentials."""
import sys
import os
import time

sys.path.insert(0, '/workspace/ibind')

# Set environment variables for ibind
consumer_key = open('/workspace/ibkr-conduit/tools/cusumer_key').read().strip()
access_token = open('/workspace/ibkr-conduit/tools/private_access_token').read().strip()
access_token_secret = open('/workspace/ibkr-conduit/tools/private_access_token_secret').read().strip()
sig_key_path = '/workspace/ibkr-conduit/tools/private_signature.pem'
enc_key_path = '/workspace/ibkr-conduit/tools/private_encryption.pem'

# Extract DH prime
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
if dh_prime.startswith('00'):
    dh_prime = dh_prime[2:]

os.environ['IBIND_USE_OAUTH'] = 'True'
os.environ['IBIND_OAUTH1A_ACCESS_TOKEN'] = access_token
os.environ['IBIND_OAUTH1A_ACCESS_TOKEN_SECRET'] = access_token_secret
os.environ['IBIND_OAUTH1A_CONSUMER_KEY'] = consumer_key
os.environ['IBIND_OAUTH1A_DH_PRIME'] = dh_prime
os.environ['IBIND_OAUTH1A_SIGNATURE_KEY_FP'] = sig_key_path
os.environ['IBIND_OAUTH1A_ENCRYPTION_KEY_FP'] = enc_key_path

# Configure proxy
os.environ['HTTPS_PROXY'] = 'http://127.0.0.1:3128'
os.environ['https_proxy'] = 'http://127.0.0.1:3128'

from ibind import IbkrClient, IbkrWsClient, ibind_logs_initialize

ibind_logs_initialize(log_to_console=True)

print("=== Creating IbkrClient (REST) ===")
client = IbkrClient(use_oauth=True)

print("\n=== Authenticating ===")
client.check_health()

print(f"\n=== LST acquired: {client.live_session_token[:20]}... ===")

print("\n=== Creating WebSocket client ===")
try:
    ws_client = IbkrWsClient(
        ibkr_client=client,
        use_oauth=True,
        access_token=access_token,
        start=True,
    )

    print("=== WebSocket started, waiting 10 seconds for messages ===")
    time.sleep(10)

    print("\n=== Subscribing to account summary (ssd) ===")
    ws_client.subscribe(channel='ssd', data={})

    print("=== Waiting 15 seconds for data ===")
    time.sleep(15)

    print("\n=== Checking queues ===")
    from ibind.client.ibkr_ws_client import IbkrWsKey
    try:
        data = ws_client.new_queue_accessor().get(IbkrWsKey.ACCOUNT_SUMMARY, timeout=5)
        print(f"Account summary data: {data}")
    except Exception as e:
        print(f"No data in queue: {e}")

    print("\n=== Stopping WebSocket ===")
    ws_client.shutdown()

except Exception as e:
    print(f"WebSocket error: {type(e).__name__}: {e}")
    import traceback
    traceback.print_exc()

print("\nDone.")
