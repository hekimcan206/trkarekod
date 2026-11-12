using System.Security.Cryptography;
using System.Text;

namespace TrKarekod.Model;

/// <summary>
/// One way to fill the producer-defined security value (FAST 30.20, 32 characters): an HMAC-SHA256 over the fields a
/// fraudster would want to change, truncated to 128 bits. The guides leave the algorithm to the producer; this is a
/// convention of this library, not of the standard. The CRC only detects accidents; this detects forgery, but only
/// the holder of the key (the merchant's bank) can check it.
/// </summary>
public static class ProducerHash
{
    /// <summary>Length of the value in characters.</summary>
    public const int Length = 32;

    /// <summary>Computes the value from the signed fields.</summary>
    /// <param name="key">Producer secret, at least 16 bytes.</param>
    /// <param name="iban">Merchant IBAN (30.01).</param>
    /// <param name="amount12">Amount as written in 54, or null.</param>
    /// <param name="reference">Karekod reference (51.03).</param>
    /// <param name="expires12">Expiry as written in 51.07, or null.</param>
    public static string Compute(ReadOnlySpan<byte> key, string iban, string? amount12, string reference, string? expires12)
    {
        if (key.Length < 16)
        {
            throw new ArgumentException("Use a key of at least 16 bytes.", nameof(key));
        }

        var message = Encoding.UTF8.GetBytes($"trkarekod-v1|{iban}|{amount12}|{reference}|{expires12}");
        Span<byte> mac = stackalloc byte[32];
        HMACSHA256.HashData(key, message, mac);
        return Convert.ToHexString(mac[..16]);
    }

    /// <summary>Checks the value of a parsed FAST merchant code in constant time.</summary>
    public static bool Verify(ReadOnlySpan<byte> key, MerchantKarekod code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Fast is null || code.Identity is null)
        {
            return false;
        }

        var amount = code.Amount is > 0 ? KarekodAmount.Format12(code.Amount.Value) : null;
        var expires = code.Identity.ExpiresAt is { } e ? KarekodTimestamp.Format12(e) : null;
        var expected = Compute(key, code.Fast.Iban, amount, code.Identity.Reference, expires);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code.Fast.Hash));
    }
}
