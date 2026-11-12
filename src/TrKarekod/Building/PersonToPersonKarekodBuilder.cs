using TrKarekod.Model;
using TrKarekod.Schema;

namespace TrKarekod.Building;

/// <summary>Builds FAST person-to-person codes (format indicator 75): "scan to send me money".</summary>
public sealed class PersonToPersonKarekodBuilder
{
    private readonly FieldMap _root = new(KarekodSchemas.PersonToPerson);

    /// <summary>Starts a static code with TR Karekod version 1.0 and flow type 03.</summary>
    public PersonToPersonKarekodBuilder()
    {
        _root["75"] = "10";
        _root["01"] = "11";
        Application["10"] = "03";
    }

    private FieldMap Application => _root.Template("61");

    /// <summary>A reusable code; amount and times are optional.</summary>
    public PersonToPersonKarekodBuilder Static()
    {
        _root["01"] = "11";
        return this;
    }

    /// <summary>A single-use code; the reference becomes mandatory.</summary>
    public PersonToPersonKarekodBuilder Dynamic(string reference)
    {
        _root["01"] = "12";
        _root["03"] = reference;
        return this;
    }

    /// <summary>Producer code (02), left-padded to 4 digits; banks use their EFT code.</summary>
    public PersonToPersonKarekodBuilder Producer(string producerCode)
    {
        ArgumentNullException.ThrowIfNull(producerCode);
        _root["02"] = producerCode.PadLeft(4, '0');
        return this;
    }

    /// <summary>Recipient IBAN (61.01) and name (61.07, 2-26 characters).</summary>
    public PersonToPersonKarekodBuilder Recipient(string iban, string name)
    {
        Application["01"] = iban;
        Application["07"] = name;
        return this;
    }

    /// <summary>Requested amount (54).</summary>
    public PersonToPersonKarekodBuilder Amount(decimal amount)
    {
        _root["54"] = KarekodAmount.Format12(amount);
        return this;
    }

    /// <summary>Creation (06) and optional expiry (07) times.</summary>
    public PersonToPersonKarekodBuilder Validity(DateTime createdAt, DateTime? expiresAt = null)
    {
        _root["06"] = KarekodTimestamp.Format12(createdAt);
        _root["07"] = expiresAt is { } e ? KarekodTimestamp.Format12(e) : null;
        return this;
    }

    /// <summary>Application-defined security value (20, up to 32 characters).</summary>
    public PersonToPersonKarekodBuilder Hash(string hash)
    {
        _root["20"] = hash;
        return this;
    }

    /// <summary>Location (50) with 6-15 decimals.</summary>
    public PersonToPersonKarekodBuilder Location(GeoLocation location, int decimals = 6)
    {
        ArgumentNullException.ThrowIfNull(location);
        _root["50"] = location.Format(decimals);
        return this;
    }

    /// <summary>Serialises and validates the code.</summary>
    /// <exception cref="KarekodBuildException">The code would not validate.</exception>
    public string Build(bool allowWarnings = false) => _root.Build(allowWarnings);
}
