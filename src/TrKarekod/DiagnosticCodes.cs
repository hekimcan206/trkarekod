namespace TrKarekod;

/// <summary>
/// Stable diagnostic codes. Codes are never reused or renumbered, so callers can map them to their own messages.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>The data ended inside an ID, a length or a value.</summary>
    public const string Truncated = "TRK001";

    /// <summary>An ID or a length is not two decimal digits.</summary>
    public const string MalformedHeader = "TRK002";

    /// <summary>A data object declares length 00.</summary>
    public const string ZeroLength = "TRK003";

    /// <summary>The same ID appears twice at one level.</summary>
    public const string DuplicateId = "TRK004";

    /// <summary>The payload contains a lone UTF-16 surrogate and cannot be encoded as UTF-8.</summary>
    public const string InvalidText = "TRK005";

    /// <summary>The CRC data object (ID 63) is missing or is not the last object.</summary>
    public const string CrcMissing = "TRK010";

    /// <summary>The CRC value does not match the payload.</summary>
    public const string CrcMismatch = "TRK011";

    /// <summary>The CRC value is not four upper-case hexadecimal digits.</summary>
    public const string CrcFormat = "TRK012";

    /// <summary>The payload does not start with a known format indicator.</summary>
    public const string UnknownFormat = "TRK020";

    /// <summary>The format indicator value is not the supported version.</summary>
    public const string UnsupportedVersion = "TRK021";

    /// <summary>A mandatory field is missing.</summary>
    public const string MissingField = "TRK030";

    /// <summary>A value is shorter or longer than the field allows.</summary>
    public const string LengthOutOfRange = "TRK031";

    /// <summary>A value contains characters its data type does not allow.</summary>
    public const string InvalidCharacters = "TRK032";

    /// <summary>A value is outside the field's allowed set.</summary>
    public const string InvalidValue = "TRK033";

    /// <summary>A field the scheme marks as not used is present.</summary>
    public const string NotUsedInScheme = "TRK034";

    /// <summary>An ID the scheme does not define is present.</summary>
    public const string UnknownField = "TRK035";

    /// <summary>A dynamic code has no amount, or the amount is zero.</summary>
    public const string DynamicWithoutAmount = "TRK040";

    /// <summary>A dynamic code has no expiry time (51.07).</summary>
    public const string DynamicWithoutExpiry = "TRK041";

    /// <summary>The expiry time is not after the creation time.</summary>
    public const string ExpiryBeforeCreation = "TRK042";

    /// <summary>A refund (flow type 04) breaks the refund rules: missing 31.01 or a payment purpose other than 00.</summary>
    public const string RefundRules = "TRK043";

    /// <summary>The IBAN is not a 26-character Turkish IBAN.</summary>
    public const string IbanFormat = "TRK044";

    /// <summary>The IBAN fails the ISO 13616 mod-97 check (typical for placeholder IBANs in samples).</summary>
    public const string IbanChecksum = "TRK045";

    /// <summary>A timestamp is not a real calendar date and time.</summary>
    public const string InvalidTimestamp = "TRK046";

    /// <summary>The location (ID 50) is malformed or out of range.</summary>
    public const string InvalidLocation = "TRK047";

    /// <summary>No merchant account information template (IDs 02-51) is present.</summary>
    public const string NoMerchantAccount = "TRK048";

    /// <summary>The refund reference (31.01) is malformed.</summary>
    public const string InvalidRefundReference = "TRK049";

    /// <summary>The payload exceeds the 512-character EMVCo limit.</summary>
    public const string PayloadTooLong = "TRK050";

    /// <summary>A short code does not have the fixed 54-character layout.</summary>
    public const string ShortCodeLayout = "TRK060";

    /// <summary>All codes with a one-line description, for documentation and tooling.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Truncated] = "Data ended inside an ID, a length or a value.",
        [MalformedHeader] = "ID or length is not two decimal digits.",
        [ZeroLength] = "Data object declares length 00.",
        [DuplicateId] = "Same ID appears twice at one level.",
        [InvalidText] = "Lone UTF-16 surrogate; cannot be encoded as UTF-8.",
        [CrcMissing] = "CRC (ID 63) missing or not the last object.",
        [CrcMismatch] = "CRC value does not match the payload.",
        [CrcFormat] = "CRC value is not four upper-case hex digits.",
        [UnknownFormat] = "Unknown format indicator.",
        [UnsupportedVersion] = "Unsupported format version.",
        [MissingField] = "Mandatory field missing.",
        [LengthOutOfRange] = "Value length outside the field's range.",
        [InvalidCharacters] = "Characters not allowed by the data type.",
        [InvalidValue] = "Value outside the allowed set.",
        [NotUsedInScheme] = "Field marked as not used by the scheme is present.",
        [UnknownField] = "ID not defined by the scheme.",
        [DynamicWithoutAmount] = "Dynamic code without a non-zero amount.",
        [DynamicWithoutExpiry] = "Dynamic code without an expiry time (51.07).",
        [ExpiryBeforeCreation] = "Expiry time not after creation time.",
        [RefundRules] = "Refund flow without 31.01 or with a payment purpose other than 00.",
        [IbanFormat] = "Not a 26-character Turkish IBAN.",
        [IbanChecksum] = "IBAN fails the mod-97 check.",
        [InvalidTimestamp] = "Timestamp is not a real date and time.",
        [InvalidLocation] = "Location malformed or out of range.",
        [NoMerchantAccount] = "No merchant account template (02-51).",
        [InvalidRefundReference] = "Refund reference (31.01) malformed.",
        [PayloadTooLong] = "Payload longer than 512 characters.",
        [ShortCodeLayout] = "Short code is not the fixed 54-character layout.",
    };
}
