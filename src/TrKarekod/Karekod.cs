using TrKarekod.Schema;
using TrKarekod.Tlv;
using TrKarekod.Validation;

namespace TrKarekod;

/// <summary>Entry point for reading TR Karekod payloads.</summary>
public static class Karekod
{
    /// <summary>EMVCo limit for a whole payload, in characters.</summary>
    public const int MaxPayloadLength = 512;

    /// <summary>
    /// Parses and validates a payload: structure, CRC, the data-organisation table of its usage model and the
    /// cross-field business rules. Never throws for malformed input.
    /// </summary>
    /// <param name="payload">The text decoded from the QR symbol.</param>
    public static KarekodParseResult Parse(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var diagnostics = new List<KarekodDiagnostic>();

        // Refuse oversized input before doing any work proportional to it: the payload is attacker-controlled.
        if (payload.Length > MaxPayloadLength)
        {
            diagnostics.Add(Error(DiagnosticCodes.PayloadTooLong, string.Empty, -1,
                $"Payload is {payload.Length} characters; the maximum is {MaxPayloadLength}."));
            return Finish(payload, KarekodKind.Unknown, [], diagnostics, crcValid: false);
        }

        if (RuneText.HasLoneSurrogate(payload))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidText, string.Empty, -1, "Payload contains an unpaired surrogate and is not valid UTF-8 text."));
            return Finish(payload, KarekodKind.Unknown, [], diagnostics, crcValid: false);
        }

        var (kind, schema) = Detect(payload);
        if (schema is null)
        {
            diagnostics.Add(Error(DiagnosticCodes.UnknownFormat, string.Empty, 0,
                "Payload does not start with a TR Karekod format indicator (00 merchant, 75 person-to-person, 85 consumer)."));
            return Finish(payload, kind, [], diagnostics, crcValid: false);
        }

        var crcValid = CheckCrc(payload, diagnostics);
        var nodes = TlvParser.Parse(payload, schema, diagnostics);
        var structureOk = !diagnostics.Exists(d => d.Code is DiagnosticCodes.Truncated or DiagnosticCodes.MalformedHeader or DiagnosticCodes.ZeroLength);
        if (structureOk)
        {
            if (nodes.Count > 0 && nodes[^1].Id != "63")
            {
                diagnostics.Add(Error(DiagnosticCodes.CrcMissing, "63", -1, "The CRC (ID 63) must be the last data object."));
            }

            SchemaValidator.Validate(nodes, schema, string.Empty, diagnostics);
        }

        return Finish(payload, kind, nodes, diagnostics, crcValid);
    }

    private static (KarekodKind Kind, FieldSchema? Schema) Detect(string payload)
    {
        if (payload.Length < 2)
        {
            return (KarekodKind.Unknown, null);
        }

        return payload[..2] switch
        {
            "00" => (KarekodKind.MerchantPresented, KarekodSchemas.MerchantPresented),
            "75" => (KarekodKind.PersonToPerson, KarekodSchemas.PersonToPerson),
            "85" => (KarekodKind.ConsumerPresented, KarekodSchemas.ConsumerPresented),
            _ => (KarekodKind.Unknown, null),
        };
    }

    /// <summary>
    /// Verifies the CRC independently of the TLV parse, so a corrupted payload still reports "CRC mismatch" (the
    /// most useful message for a damaged print or a bad scan) even when its structure is also broken.
    /// </summary>
    private static bool CheckCrc(string payload, List<KarekodDiagnostic> diagnostics)
    {
        if (payload.Length < 8 || !payload.AsSpan(payload.Length - 8, 4).SequenceEqual("6304"))
        {
            diagnostics.Add(Error(DiagnosticCodes.CrcMissing, "63", -1, "Payload does not end with the CRC object \"6304XXXX\"."));
            return false;
        }

        var written = payload.AsSpan(payload.Length - 4);
        if (!ushort.TryParse(written, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var expected))
        {
            diagnostics.Add(Error(DiagnosticCodes.CrcFormat, "63", payload.Length - 8, $"CRC \"{written}\" is not four hexadecimal digits."));
            return false;
        }

        var actual = Crc16Ccitt.ComputeUtf8(payload.AsSpan(0, payload.Length - 4));
        if (actual != expected)
        {
            diagnostics.Add(Error(DiagnosticCodes.CrcMismatch, "63", payload.Length - 8,
                $"CRC is {written} but the payload hashes to {Crc16Ccitt.ToHex(actual)}: the code is damaged or was altered."));
            return false;
        }

        if (!written.SequenceEqual(Crc16Ccitt.ToHex(actual)))
        {
            diagnostics.Add(new(DiagnosticCodes.CrcFormat, DiagnosticSeverity.Warning, "63", payload.Length - 8,
                "CRC uses lower-case hexadecimal; the guides write it in upper case."));
        }

        return true;
    }

    private static KarekodParseResult Finish(string payload, KarekodKind kind, IReadOnlyList<TlvNode> nodes, List<KarekodDiagnostic> diagnostics, bool crcValid)
    {
        // Stable order for humans and golden files: errors first, then by position.
        var ordered = diagnostics
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Offset < 0 ? int.MaxValue : d.Offset)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ToList();
        return new KarekodParseResult(payload, kind, nodes, ordered, crcValid);
    }

    private static KarekodDiagnostic Error(string code, string path, int offset, string message) =>
        new(code, DiagnosticSeverity.Error, path, offset, message);
}
