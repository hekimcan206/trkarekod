# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private vulnerability reporting instead:
**Security → Report a vulnerability** on this repository. You can expect an acknowledgement within 5 working days. If
the report is confirmed, a fix or mitigation plan follows within 30 days.

Please include the affected version or commit, the payload (with any real personal data replaced) and the behaviour
you observed. Any of the following is treated as a security issue:

- a payload that makes `Karekod.Parse` throw, hang or allocate out of proportion to its length;
- a payload the parser accepts although it breaks a mandatory rule in the guides;
- a builder that emits a payload the parser rejects;
- a way to pass `ProducerHash.Verify` without the key.

## Security-relevant design

See [docs/threat-model.md](docs/threat-model.md) for assets, threats and mitigations. In short:

| Area | Default |
|---|---|
| Untrusted input | Parsing never throws for bad input; 512-character cap enforced before any work; lone surrogates rejected |
| Integrity | CRC-16/CCITT-FALSE checked independently of structure; treated as accident detection, not security |
| Authenticity | Optional HMAC-SHA256 producer value in 30.20 (ADR 0005), verified in constant time; keys under 16 bytes refused |
| Output | Builders validate their own output; warnings (such as a placeholder IBAN) block unless explicitly allowed |
| Dependencies | Core library has none; pinned versions, CI audit for vulnerable packages, CodeQL |
| Personal data | The library does not log; the sample API logs request metadata only |
| Secrets | None in the repository; the compose file carries a labelled demo-only key so the demo runs without setup |
