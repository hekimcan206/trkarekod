using System.Globalization;
using TrKarekod.Model;

namespace TrKarekod.Validation;

/// <summary>
/// Cross-field rules of the guides that a per-field table cannot express (the "K"/conditional rows), plus the typed
/// projection of a code that passed every check.
/// </summary>
internal static class BusinessRules
{
    public static MerchantKarekod? CheckMerchant(KarekodParseResult raw, List<KarekodDiagnostic> diagnostics)
    {
        var isDynamic = raw.Value("01") == "12";
        var fast = raw.Find("30");
        var bkm = raw.Find("26");

        if (!raw.Nodes.Any(n => IsMerchantAccountId(n.Id)))
        {
            diagnostics.Add(Error(DiagnosticCodes.NoMerchantAccount, string.Empty, -1,
                "At least one merchant account template (IDs 02-30, 32-46; FAST uses 30, BKM 26, mobile 32) is required."));
        }

        decimal? amount = KarekodAmount.TryParse(raw.Value("54"), out var parsedAmount) ? parsedAmount : null;
        if (isDynamic && amount is not > 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.DynamicWithoutAmount, "54", raw.Find("54")?.Offset ?? -1,
                "A dynamic code must carry a non-zero amount (54)."));
        }

        if (isDynamic && raw.Find("51.07") is null)
        {
            diagnostics.Add(Error(DiagnosticCodes.DynamicWithoutExpiry, "51.07", raw.Find("51")?.Offset ?? -1,
                "A dynamic code must carry an expiry time (51.07)."));
        }

        CheckTimes(raw, "51.06", "51.07", diagnostics);
        CheckLocation(raw, diagnostics);

        if (fast is not null)
        {
            CheckIban(raw, "30.01", diagnostics);
            if (raw.Value("53") is { } currency && currency != "949")
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidValue, "53", raw.Find("53")!.Offset, $"FAST payments are in TRY: currency must be 949, was {currency}."));
            }

            if (raw.Find("51") is null)
            {
                diagnostics.Add(Error(DiagnosticCodes.MissingField, "51", -1, "FAST codes carry the identity template (51) with the reference the payer's bank echoes back."));
            }

            if (raw.Find("62.08") is null)
            {
                diagnostics.Add(Error(DiagnosticCodes.MissingField, "62.08", -1, "FAST codes carry the payment purpose (62.08)."));
            }

            var refundText = raw.Value("31.01");
            if (refundText is not null && !RefundReference.TryParse(refundText, out _))
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidRefundReference, "31.01", raw.Find("31.01")!.Offset,
                    "Refund reference must be YYMMDD + 4-digit participant code + 18-digit query number."));
            }

            if (raw.Value("30.02") == "04")
            {
                if (refundText is null)
                {
                    diagnostics.Add(Error(DiagnosticCodes.RefundRules, "31.01", -1, "A refund (flow type 04) must reference the original payment in 31.01."));
                }

                if (raw.Value("62.08") is { } purpose && purpose != "00")
                {
                    diagnostics.Add(Error(DiagnosticCodes.RefundRules, "62.08", raw.Find("62.08")!.Offset, $"A refund must use payment purpose 00, was {purpose}."));
                }
            }
        }

        if (bkm is not null && raw.Value("26.06") == "4" && raw.Find("26.13") is null)
        {
            diagnostics.Add(Error(DiagnosticCodes.MissingField, "26.13", -1, "A BKM refund (transaction type 4) must carry the original RRN (26.13)."));
        }

        return HasErrors(diagnostics) ? null : ProjectMerchant(raw, isDynamic, amount);
    }

    public static PersonToPersonKarekod? CheckPersonToPerson(KarekodParseResult raw, List<KarekodDiagnostic> diagnostics)
    {
        var isDynamic = raw.Value("01") == "12";
        if (isDynamic && raw.Find("03") is null)
        {
            diagnostics.Add(Error(DiagnosticCodes.MissingField, "03", -1, "A dynamic person-to-person code must carry a reference number (03)."));
        }

        decimal? amount = KarekodAmount.TryParse(raw.Value("54"), out var parsedAmount) ? parsedAmount : null;
        CheckTimes(raw, "06", "07", diagnostics);
        CheckLocation(raw, diagnostics);
        CheckIban(raw, "61.01", diagnostics);
        if (HasErrors(diagnostics))
        {
            return null;
        }

        return new PersonToPersonKarekod(
            isDynamic ? PointOfInitiation.Dynamic : PointOfInitiation.Static,
            raw.Value("02")!,
            raw.Value("03"),
            Timestamp(raw.Value("06")),
            Timestamp(raw.Value("07")),
            amount,
            raw.Value("61.01")!,
            raw.Value("61.07")!,
            raw.Value("20"),
            Location(raw.Value("50")));
    }

    private static MerchantKarekod ProjectMerchant(KarekodParseResult raw, bool isDynamic, decimal? amount)
    {
        FastAccount? fast = raw.Find("30") is null
            ? null
            : new FastAccount(raw.Value("30.01")!, (FastFlowType)int.Parse(raw.Value("30.02")!, CultureInfo.InvariantCulture), raw.Value("30.20")!);

        BkmAccount? bkm = raw.Find("26") is null
            ? null
            : new BkmAccount(raw.Value("26.06")!, raw.Value("26.08")!, raw.Value("26.09")!, raw.Value("26.10")!,
                raw.Value("26.11") is { } n ? int.Parse(n, CultureInfo.InvariantCulture) : null, raw.Value("26.13"));

        KarekodIdentity? identity = raw.Find("51") is null
            ? null
            : new KarekodIdentity(raw.Value("51.00")!, raw.Value("51.02")!, raw.Value("51.03")!,
                raw.Value("51.04") is { } t ? (TerminalType)int.Parse(t, CultureInfo.InvariantCulture) : null,
                raw.Value("51.05"), Timestamp(raw.Value("51.06"))!.Value, Timestamp(raw.Value("51.07")));

        AdditionalData? additional = raw.Find("62") is null
            ? null
            : new AdditionalData(raw.Value("62.01"), raw.Value("62.02"), raw.Value("62.03"), raw.Value("62.04"),
                raw.Value("62.05"), raw.Value("62.06"), raw.Value("62.07"), raw.Value("62.08"));

        var refund = RefundReference.TryParse(raw.Value("31.01"), out var parsedRefund) ? parsedRefund : null;

        return new MerchantKarekod(
            isDynamic ? PointOfInitiation.Dynamic : PointOfInitiation.Static,
            fast,
            refund,
            bkm,
            raw.Find("32") is not null,
            raw.Value("49"),
            Location(raw.Value("50")),
            identity,
            raw.Value("52")!,
            raw.Value("53")!,
            amount,
            raw.Value("58")!,
            raw.Value("59")!,
            raw.Value("60")!,
            raw.Value("61"),
            additional);
    }

    private static void CheckIban(KarekodParseResult raw, string path, List<KarekodDiagnostic> diagnostics)
    {
        if (raw.Find(path) is not { } node)
        {
            return;
        }

        if (!TurkishIban.IsWellFormed(node.Value))
        {
            diagnostics.Add(Error(DiagnosticCodes.IbanFormat, path, node.Offset, "IBAN must be \"TR\" followed by 24 digits."));
        }
        else if (!TurkishIban.HasValidChecksum(node.Value))
        {
            // A warning, not an error: every official sample uses a placeholder IBAN such as TR1234...; the payer's
            // bank rejects a wrong account anyway, but a merchant integration should fix it before printing codes.
            diagnostics.Add(new(DiagnosticCodes.IbanChecksum, DiagnosticSeverity.Warning, path, node.Offset,
                "IBAN fails the mod-97 check; it looks like a placeholder or a typo."));
        }
    }

    private static void CheckTimes(KarekodParseResult raw, string createdPath, string expiresPath, List<KarekodDiagnostic> diagnostics)
    {
        DateTime? created = null, expires = null;
        foreach (var (path, isCreated) in new[] { (createdPath, true), (expiresPath, false) })
        {
            if (raw.Find(path) is not { } node)
            {
                continue;
            }

            if (!KarekodTimestamp.TryParse(node.Value, out var value))
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidTimestamp, path, node.Offset, $"\"{node.Value}\" is not a valid YYMMDDhhmmss timestamp."));
                continue;
            }

            if (isCreated)
            {
                created = value;
            }
            else
            {
                expires = value;
            }
        }

        if (created is not null && expires is not null && expires <= created)
        {
            diagnostics.Add(Error(DiagnosticCodes.ExpiryBeforeCreation, expiresPath, raw.Find(expiresPath)!.Offset,
                $"Expiry {expires:yyyy-MM-dd HH:mm:ss} is not after creation {created:yyyy-MM-dd HH:mm:ss}."));
        }
    }

    private static void CheckLocation(KarekodParseResult raw, List<KarekodDiagnostic> diagnostics)
    {
        if (raw.Find("50") is { } node && !GeoLocation.TryParse(node.Value, out _))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidLocation, "50", node.Offset,
                "Location must be an even number (16-34) of digits: latitude then longitude, 2 integer digits and 6-15 decimals each."));
        }
    }

    private static bool IsMerchantAccountId(string id)
    {
        var n = int.Parse(id, CultureInfo.InvariantCulture);
        return n is >= 2 and <= 46 && n != 31;
    }

    private static DateTime? Timestamp(string? value) => KarekodTimestamp.TryParse(value, out var t) ? t : null;

    private static GeoLocation? Location(string? value) => GeoLocation.TryParse(value, out var l) ? l : null;

    private static bool HasErrors(List<KarekodDiagnostic> diagnostics)
    {
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].Severity == DiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    private static KarekodDiagnostic Error(string code, string path, int offset, string message) =>
        new(code, DiagnosticSeverity.Error, path, offset, message);
}
