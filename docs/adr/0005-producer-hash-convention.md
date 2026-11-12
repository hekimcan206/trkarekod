# 5. Producer security value (30.20): optional HMAC-SHA256 convention, not part of the standard

- Status: accepted
- Date: 2026-11-12

## Context and problem statement

The CRC detects accidental damage, not forgery: anyone can change the amount or the IBAN on a static sticker and
recompute the CRC in a few lines of code (the CI compose job does exactly that). The FAST template has a mandatory
32-character "security hash" (30.20, "Özet Güvenlik Değeri"). The guide leaves its content to the producer: a value of
the producer's choosing, a digest mixed with customer data, or random. The BKM guide suggests signing with a
producer-held key and checking the value before authorisation. Only the producer (normally the merchant's bank) can
check it, when the payment message comes back.

## Considered options

1. Fill 30.20 with random hex and leave verification to integrators.
2. Define a library convention: HMAC-SHA256 over the fields a fraudster wants to change, truncated to 128 bits (32
   hex characters), with constant-time verification.
3. A digital signature (ECDSA) so third parties can verify. That does not fit in 32 characters and nobody but the
   producer is expected to verify.

## Decision

Option 2, opt-in. `FastMerchantKarekodBuilder.SignWith(key)` computes
`HMAC-SHA256(key, "trkarekod-v1|IBAN|amount12|reference|expires12")` and writes the first 16 bytes as 32 upper-case
hex characters. `ProducerHash.Verify(key, merchant)` recomputes it and compares in constant time. Keys shorter than
16 bytes are refused. `Hash(string)` remains available for producers with their own scheme, and the CLI fills 30.20
with random bytes when no key is given.

## Consequences

- The sample API's `/verify` endpoint shows the bank-side check: integrity (CRC and rules), authenticity (30.20) and
  freshness (51.07). Tests show that changing the amount or IBAN and re-signing the CRC passes integrity but fails
  authenticity.
- 128-bit truncation keeps forgery infeasible for this use (online guessing against the bank), and the `v1` prefix
  lets the format change without ambiguity.
- This is a convention of this library. Another producer's codes will (correctly) fail verification under our key.
- Key management (storage, rotation) is out of scope. The sample takes the key from configuration and refuses to start
  without one.
