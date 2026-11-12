# Contributing

Thanks for taking the time to improve trkarekod.

## Prerequisites

- .NET SDK 10.0.400 or later (`global.json` rolls forward to the latest feature band)
- Docker only for `docker compose up`; the tests need no containers

## Workflow

1. Open an issue first for anything larger than a small fix, so we can agree on the approach.
2. Branch from `main` and keep each pull request to one concern.
3. Before pushing:

   ```bash
   dotnet build                       # must be warning-free: warnings are errors
   dotnet test                        # unit, property, golden, QR round-trip, CLI and API tests
   dotnet format --verify-no-changes
   ```

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/) are required. They keep the history readable and make
the hand-written CHANGELOG easy to assemble at release time:

- `feat:` new capability (minor bump)
- `fix:` bug fix (patch bump)
- `feat!:` or a `BREAKING CHANGE:` footer (major bump)
- `docs:`, `test:`, `build:`, `ci:`, `refactor:`, `perf:`, `chore:` for everything else

## Changing a table or a rule

The data-organisation tables in `src/TrKarekod/Schema/KarekodSchemas.cs` are transcribed from the TCMB and BKM guides
(ADR 0002). A change to them:

1. cites the guide, its version and the table or section in the pull request;
2. keeps the official samples valid (`OfficialVectorTests`) and reproducible byte for byte by the builders
   (`BuilderTests`);
3. adds a negative vector to `tests/TrKarekod.Tests/Vectors/VectorCatalog.cs` for every new rule, built from an
   official sample with `PayloadEditor` so it breaks only that rule.

## Changing behaviour visible in the vector file

`vectors/trkarekod-vectors.json` is published test data and a golden file. If your change alters what the parser
reports, regenerate it and review the diff like code:

```bash
TRKAREKOD_UPDATE_GOLDEN=1 dotnet test
git diff vectors/
```

Diagnostic codes are API: never renumber or reuse one. A new code needs an entry in `DiagnosticCodes.All` (a test
enforces this) and a row in the README table.

## Architecture decisions

A change to a decision recorded in `docs/adr/` needs a new ADR that supersedes the old one; do not edit the old ADR in
place. Use the existing files as the format (MADR: context, options considered, decision, consequences).

## Benchmarks

Numbers in the README and `docs/benchmarks.md` are measured, never estimated. If a change affects performance, rerun
`dotnet run -c Release --project benchmarks/TrKarekod.Benchmarks -- --filter '*'` and update the docs with the
results, the environment and the host load.
