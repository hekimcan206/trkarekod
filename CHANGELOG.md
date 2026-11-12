# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-12

### Added

- `TrKarekod` package (no dependencies): `Karekod.Parse` for merchant-presented long codes (FAST template 30/31, BKM
  card template 26, mobile template 32), FAST person-to-person codes (format indicator 75), consumer-presented codes
  (85, structure and CRC only) and fixed-width short codes (97 FAST, 99 BKM, 96 either).
- Rune-aware TLV reader and writer: lengths count characters, and the CRC-16/CCITT-FALSE covers the UTF-8 bytes,
  both confirmed by the official samples (ADR 0001).
- The data-organisation tables of the TCMB FAST guide v1.4, the BKM card payment guide v1.2 and the mobile payments
  guide v1.0, encoded as data, plus conditional business rules: dynamic codes need an amount and an expiry after the
  creation time; FAST codes are in TRY and carry the identity template and payment purpose; refunds reference the
  original payment with purpose 00; BKM refunds carry the RRN; IBAN, timestamp, location and refund-reference
  formats (ADR 0002).
- Diagnostics with stable codes TRK001-TRK060, dotted paths and offsets instead of exceptions. Typed models are set
  only for valid payloads (ADR 0003).
- Builders for static, dynamic and refund FAST merchant codes, person-to-person codes and short codes. They emit
  fields in table order, validate their own output, and reproduce the official samples byte for byte.
- Optional producer security value for 30.20: HMAC-SHA256 over IBAN, amount, reference and expiry, verified in
  constant time (ADR 0005).
- `TrKarekod.Cli` .NET tool `trkarekod`: `parse` (report or `--json`), `build fast|p2p`, `render` to PNG,
  `vectors` self-check and `codes`.
- QR rendering in byte mode, UTF-8, no ECI, level M (ADR 0004). Every official sample round-trips through ZXing.
- `vectors/trkarekod-vectors.json`: 7 official samples and 30 vectors derived from them (29 invalid, 1 valid with a
  warning), kept in sync with the parser by a golden test.
- Sample HTTP API (issue signed codes, parse, verify, render) with Serilog, OpenTelemetry and health checks.
  Chiseled non-root image; `docker compose up` checks all published vectors.
- BenchmarkDotNet suite and measured results in `docs/benchmarks.md`.
- CI: format, build with warnings as errors, tests with coverage, vulnerable-package audit, NuGet pack and consume,
  tool install and self-check, compose demo with an end-to-end forgery check, and CodeQL.

[1.0.0]: https://github.com/hekimcan206/trkarekod/releases/tag/v1.0.0
