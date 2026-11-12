using System.Globalization;
using TrKarekod.Model;

namespace TrKarekod.Validation;

/// <summary>
/// Short codes are not TLV: they are fixed-width fields (indicator 2, producer code 4, reference 12, hash 32) followed
/// by a CRC over everything before it. Unlike the long code, the CRC is not preceded by "6304" (both official samples
/// confirm this: their CRCs only reproduce without the tag).
/// </summary>
internal static class ShortCodeParser
{
    public const int Length = 54;

    public static bool LooksLikeShortCode(string payload) =>
        payload.Length >= 2 && payload[..2] is "96" or "97" or "99";

    public static ShortKarekod? Parse(string payload, List<KarekodDiagnostic> diagnostics, out bool crcValid)
    {
        crcValid = false;
        if (payload.Length != Length)
        {
            diagnostics.Add(Error(DiagnosticCodes.ShortCodeLayout, string.Empty, 0,
                $"A short code is exactly {Length} characters (2+4+12+32+4), this one has {payload.Length}."));
            return null;
        }

        var written = payload.AsSpan(50, 4);
        if (!ushort.TryParse(written, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var expected))
        {
            diagnostics.Add(Error(DiagnosticCodes.CrcFormat, "crc", 50, $"CRC \"{written}\" is not four hexadecimal digits."));
        }
        else
        {
            var actual = Crc16Ccitt.ComputeUtf8(payload.AsSpan(0, 50));
            crcValid = actual == expected;
            if (!crcValid)
            {
                diagnostics.Add(Error(DiagnosticCodes.CrcMismatch, "crc", 50,
                    $"CRC is {written} but the code hashes to {Crc16Ccitt.ToHex(actual)}: the code is damaged or was altered."));
            }
        }

        var producer = payload.Substring(2, 4);
        if (!producer.All(char.IsAsciiDigit))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidCharacters, "producer", 2, "Producer code must be 4 digits."));
        }

        var reference = payload.Substring(6, 12);
        var hash = payload.Substring(18, 32);
        if (reference.Any(char.IsControl) || hash.Any(char.IsControl))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidCharacters, "reference", 6, "Reference and hash must not contain control characters."));
        }

        if (diagnostics.Exists(d => d.Severity == DiagnosticSeverity.Error))
        {
            return null;
        }

        var scheme = (ShortKarekodScheme)int.Parse(payload.AsSpan(0, 2), CultureInfo.InvariantCulture);
        return new ShortKarekod(scheme, producer, reference, hash);
    }

    private static KarekodDiagnostic Error(string code, string path, int offset, string message) =>
        new(code, DiagnosticSeverity.Error, path, offset, message);
}
