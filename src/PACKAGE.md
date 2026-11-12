# TrKarekod

Parser, validator and generator for **TR Karekod**, the Turkish payment QR code standard (based on EMVCo
Merchant-Presented Mode) used by FAST instant payments, BKM card payments and mobile payments.

- Rune-aware TLV parsing (lengths count characters, CRC covers UTF-8 bytes, as the official samples require).
- CRC-16/CCITT-FALSE validation and generation.
- Typed models for FAST merchant codes (static and dynamic, sale and refund), person-to-person codes and short codes.
- Diagnostics with stable codes instead of exceptions; builders that refuse to emit an invalid code.

```csharp
using TrKarekod;
using TrKarekod.Building;

var result = Karekod.Parse(payload);
if (result.IsValid && result.Merchant is { } merchant)
{
    Console.WriteLine($"{merchant.MerchantName} {merchant.Amount} TRY, IBAN {merchant.Fast?.Iban}");
}

string code = new FastMerchantKarekodBuilder()
    .Dynamic(amount: 150.50m, expiresAt: now.AddMinutes(10))
    .Iban("TR330006100519786457841326")
    .SignWith(producerKey)                       // 30.20 = HMAC over IBAN, amount, reference, expiry
    .Producer("0010", reference: "23451017", createdAt: now)
    .Merchant("ABC GIDA", "İSTANBUL", mcc: "5499")
    .PaymentPurpose("01")
    .Build();                                    // validated by Karekod.Parse before it is returned
```

Documentation, design decisions, official test vectors and measured results: https://github.com/hekimcan206/trkarekod
