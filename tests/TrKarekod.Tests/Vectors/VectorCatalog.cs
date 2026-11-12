using System.Text;
using static TrKarekod.Tests.Vectors.OfficialVectors;

namespace TrKarekod.Tests.Vectors;

/// <summary>One published test vector.</summary>
/// <param name="PrimaryCode">The rule the vector is designed to break (null for valid vectors).</param>
public sealed record VectorCase(string Name, string Source, string Description, string Payload, string? PrimaryCode);

/// <summary>
/// The vector set published in vectors/trkarekod-vectors.json: the official samples plus negative vectors derived
/// from them, each breaking one rule while keeping a correct CRC unless the CRC is the point.
/// </summary>
public static class VectorCatalog
{
    private const string Derived = "Derived from an official sample by this repository";
    private const string Synthetic = "Synthetic";

    public static IReadOnlyList<VectorCase> All { get; } =
    [
        new("fast-sale", FastGuide, "FAST dynamic merchant sale (long code); contains İSTANBUL, so the CRC is over UTF-8.", FastSale, null),
        new("fast-refund", FastGuide, "FAST merchant refund: flow type 04 with refund reference 31.01.", FastRefund, null),
        new("fast-p2p", FastGuide, "FAST person-to-person code (format indicator 75).", FastPersonToPerson, null),
        new("fast-short", FastGuide, "FAST short code (indicator 97).", FastShort, null),
        new("bkm-card", BkmGuide, "BKM card payment long code (template 26), static.", BkmCard, null),
        new("bkm-short", BkmGuide, "BKM short code (indicator 99).", BkmShort, null),
        new("mobile-cpm", MobileGuide, "Consumer-presented code (format indicator 85); structure and CRC only.", MobileConsumerPresented, null),
        new("fast-not-used-field", Derived, "Adds tip indicator 55, which TR Karekod does not use: valid with a warning.",
            PayloadEditor.Edit(FastSale, e => e.Set("55", "01")), null),

        new("fast-crc-mismatch", Derived, "Merchant name changed after signing; the CRC no longer matches.",
            FastSale.Replace("5908ABC GIDA", "5908ABD GIDA", StringComparison.Ordinal), DiagnosticCodes.CrcMismatch),
        new("fast-crc-over-latin1", Derived, "CRC computed over ISO-8859-9/Latin-1 bytes instead of UTF-8 (the İ encodes differently).",
            ResignLatin1(FastSale), DiagnosticCodes.CrcMismatch),
        new("fast-length-in-bytes", Derived, "City length written as UTF-8 bytes (09) instead of characters (08), then re-signed.",
            Sign(FastSale[..^8].Replace("6008İSTANBUL", "6009İSTANBUL", StringComparison.Ordinal)), DiagnosticCodes.MalformedHeader),
        new("fast-truncated", Derived, "Last six characters lost (a damaged print or partial scan).", FastSale[..^6], DiagnosticCodes.CrcMissing),
        new("fast-dynamic-without-amount", Derived, "Dynamic code with the amount (54) removed.",
            PayloadEditor.Edit(FastSale, e => e.Remove("54")), DiagnosticCodes.DynamicWithoutAmount),
        new("fast-dynamic-zero-amount", Derived, "Dynamic code with amount 0.",
            PayloadEditor.Edit(FastSale, e => e.Set("54", "000000000000")), DiagnosticCodes.DynamicWithoutAmount),
        new("fast-dynamic-without-expiry", Derived, "Dynamic code with the expiry (51.07) removed.",
            PayloadEditor.Edit(FastSale, e => e.Remove("51.07")), DiagnosticCodes.DynamicWithoutExpiry),
        new("fast-expiry-before-creation", Derived, "Expiry earlier than the creation time.",
            PayloadEditor.Edit(FastSale, e => e.Set("51.07", "200729153000")), DiagnosticCodes.ExpiryBeforeCreation),
        new("fast-invalid-timestamp", Derived, "Creation time in month 13.",
            PayloadEditor.Edit(FastSale, e => e.Set("51.06", "201329153059")), DiagnosticCodes.InvalidTimestamp),
        new("fast-wrong-currency", Derived, "FAST code in USD (840).",
            PayloadEditor.Edit(FastSale, e => e.Set("53", "840")), DiagnosticCodes.InvalidValue),
        new("fast-wrong-country", Derived, "Country code DE.",
            PayloadEditor.Edit(FastSale, e => e.Set("58", "DE")), DiagnosticCodes.InvalidValue),
        new("fast-missing-merchant-name", Derived, "Merchant name (59) removed.",
            PayloadEditor.Edit(FastSale, e => e.Remove("59")), DiagnosticCodes.MissingField),
        new("fast-name-too-long", Derived, "Merchant name of 26 characters (limit 25).",
            PayloadEditor.Edit(FastSale, e => e.Set("59", "ABCDEFGHIJKLMNOPQRSTUVWXYZ")), DiagnosticCodes.LengthOutOfRange),
        new("fast-amount-not-numeric", Derived, "Amount with a letter.",
            PayloadEditor.Edit(FastSale, e => e.Set("54", "00000001505A")), DiagnosticCodes.InvalidCharacters),
        new("fast-duplicate-field", Derived, "Country code appears twice.",
            PayloadEditor.Edit(FastSale, e => e.AppendRaw("58", "TR")), DiagnosticCodes.DuplicateId),
        new("fast-bad-location", Derived, "Latitude 99.9 degrees.",
            PayloadEditor.Edit(FastSale, e => e.Set("50", "9993942332851791")), DiagnosticCodes.InvalidLocation),
        new("fast-iban-malformed", Derived, "IBAN with a German country code.",
            PayloadEditor.Edit(FastSale, e => e.Set("30.01", "DE12345678901234567890123X")), DiagnosticCodes.IbanFormat),
        new("fast-wrong-guid", Derived, "FAST template with another scheme's GUID.",
            PayloadEditor.Edit(FastSale, e => e.Set("30.00", "TR.GOV.TCMB.XYZ1")), DiagnosticCodes.InvalidValue),
        new("fast-no-payment-purpose", Derived, "FAST code without payment purpose 62.08.",
            PayloadEditor.Edit(FastSale, e => e.Remove("62.08")), DiagnosticCodes.MissingField),
        new("fast-refund-wrong-purpose", Derived, "Refund with payment purpose 09 instead of 00.",
            PayloadEditor.Edit(FastRefund, e => e.Set("62.08", "09")), DiagnosticCodes.RefundRules),
        new("fast-refund-missing-reference", Derived, "Refund without the original payment reference (31).",
            PayloadEditor.Edit(FastRefund, e => e.Remove("31")), DiagnosticCodes.RefundRules),
        new("fast-refund-bad-reference", Derived, "Refund reference with month 13.",
            PayloadEditor.Edit(FastRefund, e => e.Set("31.01", "2013180960000000000000123456")), DiagnosticCodes.InvalidRefundReference),
        new("bkm-refund-without-rrn", Derived, "BKM refund (transaction type 4) without the original RRN 26.13.",
            PayloadEditor.Edit(BkmCard, e => e.Set("26.06", "4")), DiagnosticCodes.MissingField),
        new("p2p-dynamic-without-reference", Derived, "Dynamic person-to-person code without reference 03.",
            PayloadEditor.Edit(FastPersonToPerson, e => e.Remove("03")), DiagnosticCodes.MissingField),
        new("p2p-wrong-flow-type", Derived, "Person-to-person flow type 01 instead of 03.",
            PayloadEditor.Edit(FastPersonToPerson, e => e.Set("61.10", "01")), DiagnosticCodes.InvalidValue),
        new("short-wrong-length", Derived, "Short code with an extra character.", FastShort + "0", DiagnosticCodes.ShortCodeLayout),
        new("short-crc-mismatch", Derived, "Short code reference changed after signing.",
            FastShort.Replace("REF666", "REF667", StringComparison.Ordinal), DiagnosticCodes.CrcMismatch),
        new("unknown-format", Synthetic, "Not a TR Karekod at all (a URL).", "https://example.com/pay?id=42", DiagnosticCodes.UnknownFormat),
        new("oversized", Synthetic, "513 characters, over the EMVCo limit of 512.", new string('0', 513), DiagnosticCodes.PayloadTooLong),
    ];

    private static string Sign(string body)
    {
        var withTag = body + "6304";
        return withTag + Crc16Ccitt.ToHex(Crc16Ccitt.ComputeUtf8(withTag));
    }

    private static string ResignLatin1(string payload)
    {
        var body = payload[..^4];
        return body + Crc16Ccitt.ToHex(Crc16Ccitt.Compute(Encoding.Latin1.GetBytes(body)));
    }
}
