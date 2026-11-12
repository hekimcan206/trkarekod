namespace TrKarekod.Model;

/// <summary>FAST merchant account information, template 30.</summary>
/// <param name="Hash">Producer-defined security value (30.20); only the producer can verify it.</param>
public sealed record FastAccount(string Iban, FastFlowType FlowType, string Hash);

/// <summary>BKM card payment account information, template 26.</summary>
/// <param name="TransactionType">1 sale, 2 installment sale, 3 void, 4 refund.</param>
/// <param name="Schemes">Supported card schemes, for example "TDVMAUJ000".</param>
/// <param name="Brand">Loyalty brand letter, "N" for none.</param>
/// <param name="OriginalRrn">RRN of the original sale, for refunds.</param>
public sealed record BkmAccount(string TransactionType, string Hash, string Schemes, string Brand, int? Installments, string? OriginalRrn);

/// <summary>Karekod identity template, 51.</summary>
/// <param name="Version">TR Karekod version without the dot, "10" for 1.0.</param>
/// <param name="ProducerCode">Producer code; banks use their EFT code.</param>
/// <param name="Reference">Unique reference the payer's bank echoes back to the merchant's bank.</param>
public sealed record KarekodIdentity(
    string Version,
    string ProducerCode,
    string Reference,
    TerminalType? TerminalType,
    string? TerminalSerial,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

/// <summary>Additional data template, 62.</summary>
/// <param name="PaymentPurpose">62.08, FAST payment purpose code; "00" for refunds.</param>
public sealed record AdditionalData(
    string? BillNumber,
    string? MobileNumber,
    string? StoreLabel,
    string? LoyaltyNumber,
    string? ReferenceLabel,
    string? CustomerLabel,
    string? TerminalLabel,
    string? PaymentPurpose);

/// <summary>A valid merchant-presented long code.</summary>
/// <param name="Amount">Amount in TRY (or <see cref="Currency"/>); null or zero means the payer types the amount.</param>
public sealed record MerchantKarekod(
    PointOfInitiation PointOfInitiation,
    FastAccount? Fast,
    RefundReference? Refund,
    BkmAccount? Bkm,
    bool HasMobileAccount,
    string? MerchantCode,
    GeoLocation? Location,
    KarekodIdentity? Identity,
    string MerchantCategoryCode,
    string Currency,
    decimal? Amount,
    string CountryCode,
    string MerchantName,
    string MerchantCity,
    string? PostalCode,
    AdditionalData? AdditionalData)
{
    /// <summary>True for a single-transaction code.</summary>
    public bool IsDynamic => PointOfInitiation == PointOfInitiation.Dynamic;

    /// <summary>True when the code is a FAST refund (flow type 04).</summary>
    public bool IsRefund => Fast?.FlowType == FastFlowType.Refund;
}

/// <summary>A valid FAST person-to-person code (format indicator 75).</summary>
public sealed record PersonToPersonKarekod(
    PointOfInitiation PointOfInitiation,
    string ProducerCode,
    string? Reference,
    DateTime? CreatedAt,
    DateTime? ExpiresAt,
    decimal? Amount,
    string Iban,
    string RecipientName,
    string? Hash,
    GeoLocation? Location);

/// <summary>Which payment systems a short code is valid for.</summary>
public enum ShortKarekodScheme
{
    /// <summary>"96": FAST or BKM card payment, payer chooses.</summary>
    FastOrBkm = 96,

    /// <summary>"97": FAST.</summary>
    Fast = 97,

    /// <summary>"99": BKM card payment.</summary>
    Bkm = 99,
}

/// <summary>
/// A valid short code: a fixed-width pointer (scheme, producer, reference, hash) that the payer's bank resolves through
/// the Karekod routing system (KYS) to the full transaction details.
/// </summary>
public sealed record ShortKarekod(ShortKarekodScheme Scheme, string ProducerCode, string Reference, string Hash);
