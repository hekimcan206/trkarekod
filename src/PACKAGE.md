# TrKarekod

Parser, validator and generator for **TR Karekod**, the Turkish payment QR code standard (based on EMVCo
Merchant-Presented Mode) used by FAST instant payments, BKM card payments and mobile payments.

- Rune-aware TLV parsing (lengths count characters, CRC covers UTF-8 bytes, as the official samples require).
- CRC-16/CCITT-FALSE validation and generation.
- Typed models for FAST merchant codes (static and dynamic, sale and refund), person-to-person codes and short codes.
- Diagnostics with stable codes instead of exceptions; builders that refuse to emit an invalid code.

```csharp
var result = Karekod.Parse(payload);
if (result.IsValid && result.Merchant is { } merchant)
{
    Console.WriteLine($"{merchant.MerchantName} {merchant.Amount} TRY, IBAN {merchant.Fast?.Iban}");
}

string code = new FastMerchantKarekodBuilder()
    .Dynamic(amount: 150.50m, reference: "23451017", producerCode: "0010",
             createdAt: now, expiresAt: now.AddMinutes(10))
    .Iban("TR330006100519786457841326")
    .Merchant("ABC GIDA", "İSTANBUL", mcc: "5499")
    .Build();
```

Documentation, design decisions, official test vectors and measured results: https://github.com/hekimcan206/trkarekod
