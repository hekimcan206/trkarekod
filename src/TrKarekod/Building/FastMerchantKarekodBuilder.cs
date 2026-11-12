using System.Globalization;
using TrKarekod.Model;
using TrKarekod.Schema;

namespace TrKarekod.Building;

/// <summary>
/// Builds FAST merchant-presented codes (template 30): static stickers, dynamic per-transaction codes and refunds.
/// Fields are written in the guide's table order, and <see cref="Build"/> validates the result with
/// <see cref="Karekod.Parse"/> before returning it.
/// </summary>
public sealed class FastMerchantKarekodBuilder
{
    private readonly FieldMap _root = new(KarekodSchemas.MerchantPresented);
    private byte[]? _signingKey;

    /// <summary>Starts a static code with TRY currency, country TR and TR Karekod version 1.0.</summary>
    public FastMerchantKarekodBuilder()
    {
        _root["00"] = "01";
        _root["01"] = "11";
        _root["53"] = "949";
        _root["58"] = "TR";
        _root["52"] = "0000";
        Account["00"] = KarekodSchemas.FastGuid;
        Account["02"] = "02";
        Identity["00"] = "10";
    }

    private FieldMap Account => _root.Template("30");

    private FieldMap Identity => _root.Template("51");

    private FieldMap Additional => _root.Template("62");

    /// <summary>A reusable code: flow type 02 (static verification), no expiry required.</summary>
    public FastMerchantKarekodBuilder Static()
    {
        _root["01"] = "11";
        Account["02"] = "02";
        return this;
    }

    /// <summary>A single-transaction code: flow type 01 (dynamic verification), fixed amount and expiry.</summary>
    public FastMerchantKarekodBuilder Dynamic(decimal amount, DateTime expiresAt)
    {
        _root["01"] = "12";
        Account["02"] = "01";
        Amount(amount);
        Identity["07"] = KarekodTimestamp.Format12(expiresAt);
        return this;
    }

    /// <summary>A refund of an earlier FAST payment: flow type 04, reference in 31.01, payment purpose 00.</summary>
    public FastMerchantKarekodBuilder Refund(RefundReference original, decimal amount, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(original);
        Dynamic(amount, expiresAt);
        Account["02"] = "04";
        _root.Template("31")["01"] = original.Format();
        Additional["08"] = "00";
        return this;
    }

    /// <summary>Overrides the flow type chosen by <see cref="Static"/>, <see cref="Dynamic"/> or <see cref="Refund"/>.</summary>
    public FastMerchantKarekodBuilder FlowType(FastFlowType flowType)
    {
        Account["02"] = ((int)flowType).ToString("D2", CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>A fixed amount (also allowed on static codes, for example a fixed-price item).</summary>
    public FastMerchantKarekodBuilder Amount(decimal amount)
    {
        _root["54"] = KarekodAmount.Format12(amount);
        return this;
    }

    /// <summary>The merchant's IBAN the payment is credited to.</summary>
    public FastMerchantKarekodBuilder Iban(string iban)
    {
        Account["01"] = iban;
        return this;
    }

    /// <summary>A producer-chosen 32-character security value (30.20).</summary>
    public FastMerchantKarekodBuilder Hash(string hash)
    {
        _signingKey = null;
        Account["20"] = hash;
        return this;
    }

    /// <summary>Fills 30.20 with <see cref="ProducerHash"/> over IBAN, amount, reference and expiry at build time.</summary>
    public FastMerchantKarekodBuilder SignWith(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _signingKey = [.. key];
        return this;
    }

    /// <summary>Name shown on the payer's screen (max 25), city (max 15) and ISO 18245 merchant category code.</summary>
    public FastMerchantKarekodBuilder Merchant(string name, string city, string mcc = "0000")
    {
        _root["59"] = name;
        _root["60"] = city;
        _root["52"] = mcc;
        return this;
    }

    /// <summary>Postal code (61).</summary>
    public FastMerchantKarekodBuilder PostalCode(string postalCode)
    {
        _root["61"] = postalCode;
        return this;
    }

    /// <summary>Merchant code in the merchant registry (49), left-padded to 10 digits.</summary>
    public FastMerchantKarekodBuilder MerchantCode(string merchantCode)
    {
        ArgumentNullException.ThrowIfNull(merchantCode);
        _root["49"] = merchantCode.PadLeft(10, '0');
        return this;
    }

    /// <summary>Location (50) with 6-15 decimals.</summary>
    public FastMerchantKarekodBuilder Location(GeoLocation location, int decimals = 6)
    {
        ArgumentNullException.ThrowIfNull(location);
        _root["50"] = location.Format(decimals);
        return this;
    }

    /// <summary>Identity template (51): producer code (left-padded to 4 digits), unique reference and creation time.</summary>
    public FastMerchantKarekodBuilder Producer(string producerCode, string reference, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(producerCode);
        Identity["02"] = producerCode.PadLeft(4, '0');
        Identity["03"] = reference;
        Identity["06"] = KarekodTimestamp.Format12(createdAt);
        return this;
    }

    /// <summary>Terminal type (51.04) and optional serial number (51.05).</summary>
    public FastMerchantKarekodBuilder Terminal(TerminalType type, string? serialNumber = null)
    {
        Identity["04"] = ((int)type).ToString("D2", CultureInfo.InvariantCulture);
        Identity["05"] = serialNumber;
        return this;
    }

    /// <summary>Expiry (51.07); optional on static codes.</summary>
    public FastMerchantKarekodBuilder ExpiresAt(DateTime expiresAt)
    {
        Identity["07"] = KarekodTimestamp.Format12(expiresAt);
        return this;
    }

    /// <summary>Payment purpose code from the FAST message catalogue (62.08), for example "09" for dues.</summary>
    public FastMerchantKarekodBuilder PaymentPurpose(string code)
    {
        Additional["08"] = code;
        return this;
    }

    /// <summary>Optional additional data (62.01-62.07).</summary>
    public FastMerchantKarekodBuilder AdditionalData(
        string? billNumber = null,
        string? mobileNumber = null,
        string? storeLabel = null,
        string? loyaltyNumber = null,
        string? referenceLabel = null,
        string? customerLabel = null,
        string? terminalLabel = null)
    {
        Additional["01"] = billNumber;
        Additional["02"] = mobileNumber;
        Additional["03"] = storeLabel;
        Additional["04"] = loyaltyNumber;
        Additional["05"] = referenceLabel;
        Additional["06"] = customerLabel;
        Additional["07"] = terminalLabel;
        return this;
    }

    /// <summary>Serialises and validates the code.</summary>
    /// <param name="allowWarnings">Accept warnings such as a placeholder IBAN (only for reproducing samples).</param>
    /// <exception cref="KarekodBuildException">The code would not validate.</exception>
    public string Build(bool allowWarnings = false)
    {
        if (_signingKey is not null)
        {
            Account["20"] = ProducerHash.Compute(_signingKey, Account["01"] ?? string.Empty, _root["54"], Identity["03"] ?? string.Empty, Identity["07"]);
        }

        return _root.Build(allowWarnings);
    }
}
