using System.Globalization;
using System.Numerics;

namespace TrKarekod.Model;

/// <summary>Static (reusable) or dynamic (single transaction) code: ID 01, "Karekod Türü".</summary>
public enum PointOfInitiation
{
    /// <summary>"11": the same code is used for many transactions (a printed sticker).</summary>
    Static = 11,

    /// <summary>"12": a new code per transaction, with amount and expiry.</summary>
    Dynamic = 12,
}

/// <summary>FAST flow type, ID 30.02 ("Karekod Akış Türü").</summary>
public enum FastFlowType
{
    /// <summary>"01": merchant payment verified online by the merchant's bank (dynamic verification).</summary>
    DynamicVerification = 1,

    /// <summary>"02": merchant payment verified against data registered for a static code.</summary>
    StaticVerification = 2,

    /// <summary>"04": refund of an earlier FAST merchant payment.</summary>
    Refund = 4,
}

/// <summary>Device that produced the code, ID 51.04.</summary>
public enum TerminalType
{
    /// <summary>"01": printed static code.</summary>
    StaticCode = 1,

    /// <summary>"02": POS, mPOS, cash register, kiosk.</summary>
    Pos = 2,

    /// <summary>"03": ATM.</summary>
    Atm = 3,

    /// <summary>"04": merchant application.</summary>
    MerchantApp = 4,

    /// <summary>"05": web.</summary>
    Web = 5,

    /// <summary>"06": payment service provider's merchant application.</summary>
    PspMerchantApp = 6,
}

/// <summary>Turkish IBAN checks (ISO 13616): "TR" + 2 check digits + 5-digit bank code + reserve digit + 16-digit account.</summary>
public static class TurkishIban
{
    /// <summary>Length of a Turkish IBAN.</summary>
    public const int Length = 26;

    /// <summary>True for "TR" followed by 24 digits.</summary>
    public static bool IsWellFormed(string? iban)
    {
        if (iban is not { Length: Length } || !iban.StartsWith("TR", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 2; i < Length; i++)
        {
            if (!char.IsAsciiDigit(iban[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when a well-formed IBAN passes the mod-97 check.</summary>
    public static bool HasValidChecksum(string iban)
    {
        if (!IsWellFormed(iban))
        {
            return false;
        }

        // Move country code and check digits to the end, letters to numbers (T=29, R=27), then mod 97 must be 1.
        var digits = string.Concat(iban.AsSpan(4), "2927", iban.AsSpan(2, 2));
        return BigInteger.Parse(digits, CultureInfo.InvariantCulture) % 97 == 1;
    }

    /// <summary>Computes the check digits for a bank code (5 digits), reserve digit and account number (16 digits).</summary>
    public static string Create(string bankCode, string accountNumber)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankCode);
        ArgumentException.ThrowIfNullOrEmpty(accountNumber);
        var bban = bankCode.PadLeft(5, '0') + "0" + accountNumber.PadLeft(16, '0');
        if (bban.Length != 22 || !bban.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Bank code must be up to 5 digits and the account number up to 16 digits.");
        }

        var remainder = BigInteger.Parse(bban + "292700", CultureInfo.InvariantCulture) % 97;
        return $"TR{98 - (int)remainder:D2}{bban}";
    }
}

/// <summary>
/// Timestamps in the "YYAAGGSSDDss" form (yyMMddHHmmss). The guides give no time zone: values are Turkey local time,
/// returned as <see cref="DateTimeKind.Unspecified"/>.
/// </summary>
public static class KarekodTimestamp
{
    private const string Format = "yyMMddHHmmss";

    /// <summary>Parses a 12-digit timestamp; false when it is not a real date and time.</summary>
    public static bool TryParse(string? value, out DateTime timestamp) =>
        DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);

    /// <summary>Formats a timestamp; years outside 2000-2099 cannot be represented.</summary>
    public static string Format12(DateTime timestamp)
    {
        if (timestamp.Year is < 2000 or > 2099)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "Two-digit years cover 2000-2099 only.");
        }

        return timestamp.ToString(Format, CultureInfo.InvariantCulture);
    }
}

/// <summary>Amounts are 12 digits of kuruş (minor units), zero-padded: 150.50 is "000000015050".</summary>
public static class KarekodAmount
{
    /// <summary>Largest representable amount.</summary>
    public const decimal Max = 9_999_999_999.99m;

    /// <summary>Parses a 12-digit amount.</summary>
    public static bool TryParse(string? value, out decimal amount)
    {
        amount = 0;
        if (value is not { Length: 12 } || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        amount = long.Parse(value, CultureInfo.InvariantCulture) / 100m;
        return true;
    }

    /// <summary>Formats an amount; it must be positive, at most <see cref="Max"/> and have at most two decimals.</summary>
    public static string Format12(decimal amount)
    {
        if (amount <= 0 || amount > Max || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive, at most 9,999,999,999.99 and have at most two decimals.");
        }

        return ((long)(amount * 100)).ToString("D12", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Location, ID 50: latitude and longitude in decimal degrees written as "EEeeeeeeBBbbbbbb" (two integer digits, 6-15
/// decimals each, no separator). The format has no sign and two integer digits, so it covers positive coordinates
/// below 100 degrees: all of Türkiye, but not the whole globe.
/// </summary>
public sealed record GeoLocation(decimal Latitude, decimal Longitude)
{
    /// <summary>Parses the ID 50 value.</summary>
    public static bool TryParse(string? value, out GeoLocation? location)
    {
        location = null;
        if (value is null || value.Length is < 16 or > 34 || value.Length % 2 != 0 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var half = value.Length / 2;
        var latitude = decimal.Parse(value.AsSpan(0, 2), CultureInfo.InvariantCulture) + Fraction(value.AsSpan(2, half - 2));
        var longitude = decimal.Parse(value.AsSpan(half, 2), CultureInfo.InvariantCulture) + Fraction(value.AsSpan(half + 2, half - 2));
        if (latitude > 90)
        {
            return false;
        }

        location = new GeoLocation(latitude, longitude);
        return true;
    }

    /// <summary>Formats with <paramref name="decimals"/> digits after the point (6-15).</summary>
    public string Format(int decimals = 6)
    {
        if (decimals is < 6 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Precision must be 6-15 decimals.");
        }

        if (Latitude is < 0 or > 90 || Longitude is < 0 or >= 100)
        {
            throw new InvalidOperationException("The TR Karekod location format only holds positive coordinates below 100 degrees.");
        }

        return Part(Latitude, decimals) + Part(Longitude, decimals);
    }

    private static string Part(decimal degrees, int decimals)
    {
        var scaled = decimal.Round(degrees, decimals, MidpointRounding.AwayFromZero);
        var text = scaled.ToString("00." + new string('0', decimals), CultureInfo.InvariantCulture);
        return text.Replace(".", string.Empty, StringComparison.Ordinal);
    }

    private static decimal Fraction(ReadOnlySpan<char> digits) =>
        decimal.Parse(digits, CultureInfo.InvariantCulture) / (decimal)Math.Pow(10, digits.Length);
}

/// <summary>
/// Reference to the FAST payment being refunded, ID 31.01: message date (YYAAGG), sender participant code (4) and
/// FAST query number (18, zero-padded).
/// </summary>
public sealed record RefundReference(DateOnly MessageDate, string SenderParticipantCode, string QueryNumber)
{
    /// <summary>Parses the 28-character value.</summary>
    public static bool TryParse(string? value, out RefundReference? reference)
    {
        reference = null;
        if (value is not { Length: 28 } || !value.All(char.IsAsciiDigit)
            || !DateOnly.TryParseExact(value.AsSpan(0, 6), "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        reference = new RefundReference(date, value.Substring(6, 4), value.Substring(10, 18));
        return true;
    }

    /// <summary>Formats the 28-character value.</summary>
    public string Format()
    {
        if (SenderParticipantCode is not { Length: 4 } || !SenderParticipantCode.All(char.IsAsciiDigit)
            || QueryNumber.Length is < 1 or > 18 || !QueryNumber.All(char.IsAsciiDigit))
        {
            throw new InvalidOperationException("Participant code must be 4 digits and the query number 1-18 digits.");
        }

        return MessageDate.ToString("yyMMdd", CultureInfo.InvariantCulture) + SenderParticipantCode + QueryNumber.PadLeft(18, '0');
    }
}
