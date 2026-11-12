using System.Globalization;
using TrKarekod.Model;

namespace TrKarekod.Building;

/// <summary>Builds fixed-width short codes (indicator 97, 96 or 99).</summary>
public static class ShortKarekodBuilder
{
    /// <summary>Serialises and validates a short code.</summary>
    /// <param name="producerCode">Up to 4 digits, left-padded.</param>
    /// <param name="reference">Exactly 12 characters, unique and non-sequential per acquirer.</param>
    /// <param name="hash">Exactly 32 characters.</param>
    /// <exception cref="KarekodBuildException">The code would not validate.</exception>
    public static string Build(ShortKarekodScheme scheme, string producerCode, string reference, string hash)
    {
        ArgumentNullException.ThrowIfNull(producerCode);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hash);
        if (!Enum.IsDefined(scheme))
        {
            throw new KarekodBuildException($"Unknown short code scheme {(int)scheme}.");
        }

        if (reference.Length != 12 || hash.Length != 32 || producerCode.Length > 4)
        {
            throw new KarekodBuildException("Producer code is at most 4 digits, the reference exactly 12 and the hash exactly 32 characters.");
        }

        var body = ((int)scheme).ToString(CultureInfo.InvariantCulture) + producerCode.PadLeft(4, '0') + reference + hash;
        var payload = body + Crc16Ccitt.ToHex(Crc16Ccitt.ComputeUtf8(body));
        var result = Karekod.Parse(payload);
        return result.IsValid ? payload : throw new KarekodBuildException([.. result.Errors]);
    }
}
