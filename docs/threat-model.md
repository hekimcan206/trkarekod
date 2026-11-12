# Threat model

TrKarekod parses payloads that come from a camera pointed at something anyone could have printed, and it generates
payloads that move money. This document lists what is protected, who attacks it, and what the library does about each
threat. The library does not move money and does not authenticate anyone; banks do. It decides what a payload says,
whether it is well-formed, and whether it is what a producer issued.

## Assets

| Asset | Why it matters |
|---|---|
| Payee identity in a code (IBAN 30.01/61.01, merchant name 59, recipient name 61.07) | Swapping it diverts the payment |
| Amount (54) and expiry (51.07) of a dynamic code | Changing them alters what the payer approves |
| Producer key (30.20 HMAC, ADR 0005) | Lets an attacker issue codes that pass the bank's authenticity check |
| Availability of the parser in payer apps and backends | A crash or hang on a crafted code is a denial of service |
| Payer and payee personal data in the payload (P2P name and IBAN, 62.02 mobile number) | Personal data under KVKK (Law No. 6698 on the Protection of Personal Data) |

## Actors

- **Sticker swapper** (quishing): prints a static code with their own IBAN over the merchant's.
- **Tamperer**: edits a dynamic code shown on a screen or sent by link (amount, IBAN) and recomputes the CRC.
- **Fuzzer**: feeds malformed codes to crash or slow down parsers in payer apps or acquirer backends.
- **Insider or compromised POS**: holds or extracts the producer key.

## Threats and mitigations (STRIDE)

| # | Threat | STRIDE | Mitigation in this repository | Residual risk |
|---|---|---|---|---|
| T1 | Static sticker replaced by one with the attacker's IBAN | Spoofing | Out of the library's reach: a valid code with a real IBAN is valid. `ProducerHash.Verify` lets the merchant's bank reject codes it did not issue. The payer app shows name 59 next to the bank-resolved account holder name. | Payers who ignore the displayed name. Physical controls on stickers. |
| T2 | Amount or IBAN changed and CRC recomputed | Tampering | The CRC is **not** treated as a security control (it is public and takes a few lines of code). 30.20 HMAC over IBAN, amount, reference and expiry, verified in constant time. CI forges a code this way and asserts `producerHashValid=false`. | Producers that fill 30.20 with random data (allowed by the guide) cannot detect it. |
| T3 | Expired dynamic code replayed | Tampering / Repudiation | Expiry is parsed and typed. `/verify` reports `expired`. The bank also matches the reference (51.03) against its own record of issued codes (guide section 5). | Clock skew between producer and verifier. Times carry no zone (Türkiye local, UTC+3 fallback). |
| T4 | Oversized or deeply nested input exhausts CPU or memory | Denial of service | Hard cap of 512 characters (EMVCo maximum), checked before any work proportional to input size (TRK050). Templates nest only where the tables say so, so recursion depth is bounded by the schema, not the input. The sample API caps request bodies at 16 KB. | None known. Parse cost is measured in docs/benchmarks.md. |
| T5 | Malformed input crashes the parser | Denial of service | Parsing never throws for bad input (ADR 0003). FsCheck runs arbitrary strings, mutated samples (insert, delete, replace with digits, Turkish letters, NUL, lone surrogates, emoji) and every prefix of every sample. | Bugs outside the tested space. The property tests use a fixed number of cases per run. |
| T6 | Lone surrogate or invalid UTF-8 makes the CRC undefined, so two implementations disagree | Tampering | Rejected up front (TRK005). Lengths are counted in characters and the CRC over UTF-8 by one shared helper (ADR 0001). | None known. |
| T7 | Homoglyph merchant name (Latin `I` against Turkish `İ`, Cyrillic look-alikes) | Spoofing | Not mitigated by the library: ÖAN accepts letters, as the official samples require. Display policy belongs to the payer app. | Documented in Limitations. |
| T8 | Producer key leaked | Elevation of privilege | Key is configuration, never in the repository. The sample refuses to start without one and rejects keys under 16 bytes. The `v1` prefix allows rotation to a new format. | Key storage and rotation are the integrator's job (HSM or KMS recommended). |
| T9 | Personal data in payloads written to logs | Information disclosure | The library does not log. The sample API logs request metadata only (method, path, status, timing), not bodies. | Integrators who log payloads. P2P codes hold a name and an IBAN by design. |
| T10 | Supply-chain compromise of a dependency | Tampering | The core library has **no** package dependencies. Rendering uses QRCoder and tests use ZXing.Net. All versions are pinned with central package management, CI fails on any known-vulnerable package (`dotnet list package --vulnerable --include-transitive`), and CodeQL runs on every push. | Zero-days in pinned versions. |

## What is out of scope

- Authentication of payers or payees, account-holder name matching and fraud scoring (bank side).
- The Karekod routing system (KYS) that resolves short codes. This library parses short codes; it does not resolve
  them.
- Physical security of printed codes.
