# 3. Report diagnostics instead of throwing; builders validate their own output

- Status: accepted
- Date: 2026-11-12

## Context and problem statement

A parser in a payer app reads text a camera took from a sticker that anyone could have printed. Input is hostile or
damaged by default: truncated scans, wrong lengths, a tampered amount with a CRC that no longer matches. A generator in
a POS or e-commerce backend has the opposite problem: it must never print a code that payer apps reject, because the
failure only shows up at the till.

## Considered options

1. Throw on the first problem (`FormatException`).
2. `TryParse` returning a bool.
3. Always return a result with a list of diagnostics (stable code, severity, dotted path, character offset, message),
   plus typed models only when the payload is valid.

## Decision

Option 3 for parsing:

- `Karekod.Parse` never throws for bad input. It reports every independent problem it can find (CRC, structure, table
  and rule violations), errors first and then by position, and each problem carries a stable code (`TRK001`-`TRK060`,
  documented in `DiagnosticCodes.All` and the README) that callers can map to their own UI text.
- The CRC is checked **independently of the TLV parse**. A damaged print then still produces the most useful message
  ("CRC mismatch") even when the damage also broke the structure.
- `result.Merchant`, `PersonToPerson` and `ShortCode` are set only when there is no error. Code that uses the typed
  model cannot accidentally act on a half-valid payload, and the raw tree (`Nodes`, `Find("62.08")`) is still
  available for diagnostics screens.
- Input over the EMVCo maximum of 512 characters is rejected before any work proportional to its size.

For building: builders serialise, then run `Karekod.Parse` on their own output and throw `KarekodBuildException` with
the same diagnostics if anything is wrong. Warnings also block unless the caller passes `allowWarnings: true`, which
exists only to reproduce the official samples with their placeholder IBANs. A builder therefore cannot produce a code
its own parser would reject. Property tests over random inputs check this, and the cost (one extra parse per build)
is measured in docs/benchmarks.md.

## Consequences

- Callers check `IsValid` rather than catching exceptions. FsCheck feeds arbitrary strings, mutated samples and
  every prefix of every sample to the parser; none throw.
- Diagnostic codes are API. They are never renumbered, and a test fails if a code is added without a description.
- Building costs one parse more than a write-only generator. The trade is deliberate: the codes are printed and
  displayed, and an invalid one is found far from the code that made it.
