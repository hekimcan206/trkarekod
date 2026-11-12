# 4. QR symbols: byte mode, UTF-8, no ECI designator, error correction level M

- Status: accepted
- Date: 2026-11-12

## Context and problem statement

The payload is text, but a QR symbol carries bytes. The QR standard's default interpretation of byte mode is
ISO-8859-1, and an ECI designator can declare UTF-8 (ECI 26). Payer apps hash the decoded text to check the CRC, which
ADR 0001 defines over UTF-8 bytes. If the symbol carried ISO-8859-9 bytes, or the reader decoded UTF-8 bytes as
Latin-1, a code with `İ` would fail its CRC in the payer's app even though the generator computed it correctly.

The TR guides call strings UTF-8 but say nothing about ECI. We found no statement on ECI in the TR guides or in the
public summaries of the EMVCo specification.

## Considered options

1. Let the QR library pick: ISO-8859-1 when possible, otherwise UTF-8.
2. Always UTF-8 bytes **with** an ECI 26 designator.
3. Always UTF-8 bytes **without** an ECI designator.

## Decision

Option 3, at error correction level M (QRCoder: `forceUtf8: true, utf8BOM: false, EciMode.Default`).

- For ASCII payloads, UTF-8 and ISO-8859-1 bytes are identical, so nothing changes for most codes.
- For payloads with Turkish letters, the symbol carries exactly the bytes the CRC was computed over. Common readers
  detect UTF-8 when no ECI is present.
- We avoided ECI because some older decoders mishandle the designator. We assert this as a risk, not a measured
  fact: we tested one independent decoder, ZXing.Net, and did not survey payer apps.
- Level M (about 15% recovery) keeps the full FAST sale sample at version 15 (77x77 modules) and a short code at
  version 4 (33x33).

The renderer refuses to draw an invalid payload.

## Consequences

- `QrRoundTripTests` renders all seven official samples and decodes them with ZXing.Net. Each decodes to the exact
  payload, including `İSTANBUL`, and validates.
- If a payer app turns out to require ECI 26, the change is one argument in `KarekodQr` plus a round-trip test, and it
  needs a new ADR.
- This is not verified against real banking apps (see README, Limitations).

## Sources

- ISO/IEC 18004 (QR Code): byte mode default interpretation and ECI.
- TCMB, *FAST - TR Karekod Teknik İlke ve Kuralları Rehberi*, v1.4, section 6.1 (K data type: UTF-8).
- QRCoder and ZXing.Net documentation.
