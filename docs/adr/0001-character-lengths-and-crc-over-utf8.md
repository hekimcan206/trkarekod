# 1. Count lengths in characters and compute the CRC over UTF-8 bytes

- Status: accepted
- Date: 2026-11-12

## Context and problem statement

Every TR Karekod data object is `ID (2 digits) + length (2 digits) + value`, and the long code ends with
`6304` + a CRC-16. Two details decide whether a payload with Turkish letters is read correctly:

1. Is the length the number of **characters**, UTF-16 code units, or **bytes**?
2. Over which **bytes** is the CRC computed when the text contains a letter such as `İ`, `ş` or `ğ`?

The guides say the length is "the number of characters of the value" ("karakter cinsinden uzunluğu") and that string
values are UTF-8 ("UTF-8 karakter kodlamasına uygun"). They do not spell out the CRC input encoding. A wrong choice is
invisible on ASCII-only payloads and breaks on the first merchant in İSTANBUL or IĞDIR.

## Considered options

1. Lengths in bytes, CRC over UTF-8 bytes.
2. Lengths in UTF-16 code units (`string.Length`), CRC over a single-byte code page (ISO-8859-9 / Latin-1).
3. Lengths in Unicode characters (scalar values), CRC over UTF-8 bytes.

## Decision

Option 3. Lengths count Unicode scalar values (a surrogate pair is one character), and the CRC is computed over the
UTF-8 bytes of everything before the four CRC digits, including the `6304` tag and length.

The evidence is the official FAST sample in the TCMB FAST guide v1.4 (Table 1). It contains `6008İSTANBUL`: the length
is 08 although `İSTANBUL` is 9 bytes in UTF-8, so lengths are characters. Its published CRC is `3F2E`; the same text
hashed as Latin-1 bytes gives `513B`. Only UTF-8 reproduces the published value
(`OfficialVectorTests.Fast_sale_crc_only_matches_over_utf8`). All seven samples transcribed from the three guides
reproduce their published CRCs under this rule.

Short codes (indicator 97/96/99) are the exception to the tag rule: their CRC covers the first 50 characters with no
`6304` prefix, which is the only reading under which both official short samples (`5BFD`, `80BE`) reproduce. The BKM
guide's prose says the tag is included. We follow the samples and record the conflict here.

## Consequences

- Turkish characters are handled correctly. Surrogate pairs (for example an emoji in a merchant name, which
  payer apps would display oddly) count as one character, which matches "character" in the guide and in EMVCo.
- `string.Length` must not be used for TLV lengths anywhere. `RuneText` is the single place that counts, and the TLV
  writer uses it, so builders cannot drift from the parser.
- A lone UTF-16 surrogate cannot be encoded as UTF-8, so its CRC is undefined. The parser rejects such input up front
  (TRK005) instead of hashing a replacement character.
- Two negative vectors protect the decision: `fast-crc-over-latin1` and `fast-length-in-bytes` in
  `vectors/trkarekod-vectors.json`.

## Sources

- TCMB, *FAST - TR Karekod Teknik İlke ve Kuralları Rehberi*, v1.4, May 2024, section 6.1 and Table 1 (published on
  tcmb.gov.tr).
- TCMB/BKM, *Kartlı Ödemeler TR Karekod Teknik İlke ve Kuralları Rehberi*, v1.2, January 2024, section 6.1.3 (CRC:
  ISO/IEC 13239, polynomial 1021, initial value FFFF).
- EMVCo, *QR Code Specification for Payment Systems: Merchant-Presented Mode*, v1.1 (CRC data object ID 63).
- CRC RevEng catalogue, CRC-16/IBM-3740 (also known as CRC-16/CCITT-FALSE): check value 0x29B1.
