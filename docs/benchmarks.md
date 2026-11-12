# Benchmarks

All numbers below were measured on the machine described here, with BenchmarkDotNet. None are estimated. Each row
is the **median** of 20 iterations after 5 warm-up iterations. The full BenchmarkDotNet tables (mean, error, standard
deviation, quartiles, GC counts) were kept from each run.

## Environment

| | |
|---|---|
| CPU | 13th Gen Intel Core i7-13620H, 10 cores / 16 threads (laptop) |
| RAM | 16 GB, 1.7-2.1 GB free during the runs |
| OS | Windows 11 25H2 (10.0.26200) |
| .NET | SDK 10.0.401, runtime 10.0.12, X64 RyuJIT x86-64-v3, concurrent workstation GC |
| BenchmarkDotNet | 0.15.8, `WarmupCount=5, IterationCount=20`, `MemoryDiagnoser` |
| Host load | **Not idle.** CPU load read at 61% before the first run and 33% after the last. 8 other containers were running, and another build agent shared the machine. |

The host was busy, so treat absolute times as an upper bound on this hardware and compare rows **within one run**.
Allocation figures are deterministic and do not depend on load.

## Results

Inputs are the official TCMB samples: the FAST dynamic sale (394 characters, 391 UTF-8 bytes before the CRC,
`İSTANBUL` included) and the FAST short code (54 characters).

| Operation | Median | Allocated |
|---|---|---|
| CRC-16, table-driven, 391 bytes | 1.03 µs | 0 B |
| CRC-16, bitwise reference, 391 bytes | 2.96 µs | 0 B |
| CRC-16 including UTF-8 encoding of the string | 1.22 µs | 0 B |
| `Karekod.Parse` FAST sale: CRC + TLV + tables + rules + typed model | 9.48 µs | 12,856 B |
| `Karekod.Parse` tampered FAST sale (CRC mismatch, all other checks still run) | 7.16 µs | 11,072 B |
| `Karekod.Parse` FAST short code | 0.38 µs | 648 B |
| Build a signed dynamic FAST code (HMAC + serialise + self-validating parse) | 13.77 µs | 23.03 KB |
| Render the FAST sale as PNG, 8 px/module (QRCoder, version 15, 77x77) | 4.27 ms | 41.47 KB |

Takeaways:

- Parsing costs microseconds. A backend validating every scanned code is not CPU-bound on this: 9.5 µs is about
  100,000 codes per second per core, minus host noise.
- The builder's self-validation (ADR 0003) is affordable. A build costs about 1.5x a parse, because it includes one
  parse plus the HMAC and serialisation.
- Rendering the PNG dominates code generation by roughly 300x (4.27 ms against 13.8 µs). The QR encoder (Reed-Solomon
  and mask evaluation) and PNG compression are QRCoder's, not this library's. A POS that shows codes on screen should
  cache rendered symbols rather than worry about the parser.
- The table-driven CRC is 2.9x faster than the bitwise reference. Both allocate nothing, and the stack buffer covers
  the 512-character maximum.

## The one optimisation, measured A/B

The first profile showed `Karekod.Parse` allocating **17,416 B** for the FAST sale. Most of it came from the business
rules looking up about 70 dotted paths (`"62.08"`) with `string.Split`, and from `Substring` creating a new string for
every two-digit ID. The change (commit `perf: ...`):

- `KarekodParseResult.Find` walks the path as a span instead of splitting it;
- the TLV parser reuses 100 interned ID strings;
- allowed-value checks loop instead of using LINQ `Contains`.

To separate the effect from host noise, both builds were benchmarked back to back, alternating before/after twice:

| Run | Parse FAST sale (median) | Tampered (median) | Build signed (median) | Short code (median, unchanged code) | Allocated (parse) |
|---|---|---|---|---|---|
| before, round 1 | 12.22 µs | 11.33 µs | 16.90 µs | 534 ns | 17,416 B |
| after, round 1 | 12.79 µs | 9.93 µs | 17.72 µs | 503 ns | **12,856 B** |
| before, round 2 | 10.75 µs | 7.68 µs | 15.78 µs | 387 ns | 17,416 B |
| after, round 2 | **9.48 µs** | **7.16 µs** | **13.77 µs** | 377 ns | **12,856 B** |

Honest reading:

- **Allocation fell 26%** (17,416 → 12,856 B per parse; build 27.14 → 23.03 KB). This is deterministic.
- **Time**: round 2 shows −12% for parse and −13% for build. Round 1 is inconclusive: the "after" parse median was
  higher. The short-code path, whose code did not change, moved by 28% between rounds (534 → 387 ns), which puts
  the host noise at the same size as the effect. We claim the allocation win and report the time change only
  as "about 10%, within host noise".
- The Results table above uses round 2 for parse and build, the first full run for CRC and render (code unchanged
  by the optimisation), all from the same session.

## Correctness numbers (not timing)

| What | Result |
|---|---|
| Official samples parsed and valid | 7 / 7 (FAST sale, refund, P2P, short; BKM card, short; consumer-presented) |
| Official samples reproduced byte for byte by the builders | 5 / 5 that have a builder (FAST sale, refund, P2P, FAST short, BKM short) |
| Official samples round-tripped through a rendered QR symbol and ZXing | 7 / 7 |
| Published vectors behaving as documented (`trkarekod vectors`) | 37 / 37 |
| Single-character substitutions in the FAST sale detected (FsCheck, 200 random cases per run) | 200 / 200 |
| QR version at level M | FAST sale: 15 (77x77); short code: 4 (33x33) |

## Reproduce

```bash
dotnet run -c Release --project benchmarks/TrKarekod.Benchmarks -- --filter '*'
# or one class:
dotnet run -c Release --project benchmarks/TrKarekod.Benchmarks -- --filter '*ParseBenchmarks*'
```

Close other workloads first; on a shared machine, compare variants back to back as above rather than across sessions.
