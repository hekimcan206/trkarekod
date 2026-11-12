# 2. Encode the guides' data-organisation tables as data, rules as code

- Status: accepted
- Date: 2026-11-12

## Context and problem statement

TR Karekod is defined by tables in several TCMB documents: one row per ID, with name, data type (N, ÖAN, K), length,
presence (Z mandatory, İ optional, K conditional) and allowed values. The FAST, BKM card and mobile payment guides
share the EMVCo root (IDs 00-63) but each defines its own merchant account template (30/31, 26, 32). The
person-to-person code (format indicator 75) and the consumer-presented code (85) have their own roots. The guides are
revised: the FAST guide alone has versions 1.0 to 1.6, and v1.4 notes that it corrected wrong CRC values in earlier
samples.

The validator has to follow these tables exactly and stay reviewable against them.

## Considered options

1. Hand-written validation code per field (`if (merchantName.Length > 25) ...`).
2. A generic EMVCo library with TR rules bolted on.
3. The tables as data (`FieldSpec` rows in a `FieldSchema`), one generic schema validator, and code only for the
   cross-field "K" (conditional) rules the tables cannot express.

## Decision

Option 3. `KarekodSchemas` holds the rows, each with the Turkish name as printed in the guide, so a reviewer can put
the code next to the PDF and compare line by line. `SchemaValidator` checks unknown and not-used IDs, mandatory rows,
length ranges, data types and closed value sets. `BusinessRules` holds the conditional rules: a dynamic code needs an
amount and an expiry, a FAST code is in TRY (949), carries 51 and 62.08, a refund (flow 04) needs 31.01 and payment
purpose 00, a BKM refund needs the RRN, plus IBAN, timestamp and location checks.

The tables also drive layout and output. `ITlvLayout` tells the TLV parser which IDs are templates, and builders
emit fields in **table order**. The guides' samples are in table order (the person-to-person sample puts root ID 20
after template 61), so the builders reproduce the official samples byte for byte. This is the strongest test that the
tables were transcribed correctly.

Interpretation choices, each visible in one place:

- ÖAN ("special alphanumeric") accepts any non-control character. The official sample puts `İSTANBUL` in an ÖAN field,
  so a strict ASCII reading would reject the guide's own example.
- Amounts are the fixed 12-digit kuruş form all three TR guides use (`000000015050` = 150.50), not EMVCo's
  variable-length decimal string.
- IDs a guide marks as not used ("FAST için kullanılmayacaktır", 55-57, 64, 65-99) are warnings (TRK034), not errors.
  A payer app ignores them, and rejecting would be stricter than the guide.
- An IBAN mod-97 failure is a warning (TRK045). Every official sample uses a placeholder IBAN, and the payer's bank
  rejects a non-existent account anyway. Builders treat warnings as errors unless the caller explicitly allows them.
- 47/48 (merchant free templates) may hold a primitive or a template at the merchant's discretion, so they are kept
  raw rather than parsed.

## Consequences

- Adding a scheme or a guide revision is mostly a table change, reviewed as data.
- Conditional rules live in one file and each has a negative vector.
- The consumer-presented (85) and mobile-payment (32) sub-fields are only structurally checked. Typing them would
  mean transcribing more tables, which is listed under Limitations.
- One published sample (the mobile payments guide's merchant-presented example) did not reproduce its CRC from the
  PDF text. We could not rule out text-extraction loss, so it is excluded from the vectors rather than "fixed".

## Sources

- TCMB, *FAST - TR Karekod Teknik İlke ve Kuralları Rehberi*, v1.4 (May 2024), Tables 1-3.
- TCMB/BKM, *Kartlı Ödemeler TR Karekod Teknik İlke ve Kuralları Rehberi*, v1.2 (January 2024), Tables 1-2.
- TCMB, *Mobil Ödemeler TR Karekod İlke ve Kuralları Rehberi*, v1.0 (December 2020).
- *Ödeme Hizmetlerinde TR Karekodun Üretilmesi ve Kullanılmasına İlişkin Yönetmelik* and its annex *TR Karekod İlke ve
  Kuralları*, Resmî Gazete no. 31220, 21 August 2020.
