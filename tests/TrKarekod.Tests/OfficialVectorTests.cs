using TrKarekod.Model;
using TrKarekod.Tests.Vectors;

namespace TrKarekod.Tests;

/// <summary>Every sample payload printed in the TCMB and BKM guides parses, validates and projects to the documented values.</summary>
public sealed class OfficialVectorTests
{
    [Theory]
    [MemberData(nameof(OfficialVectors.All), MemberType = typeof(OfficialVectors))]
    public void Every_official_vector_is_valid_with_its_published_crc(string name, string payload)
    {
        var result = Karekod.Parse(payload);

        result.IsValid.Should().BeTrue($"{name} is an official sample, but: {string.Join("; ", result.Errors)}");
        result.CrcValid.Should().BeTrue();
    }

    [Fact]
    public void Fast_dynamic_sale()
    {
        var result = Karekod.Parse(OfficialVectors.FastSale);
        var m = result.Merchant!;

        m.PointOfInitiation.Should().Be(PointOfInitiation.Dynamic);
        m.Fast.Should().Be(new FastAccount("TR123456789012345678901234", FastFlowType.DynamicVerification, "E200C014A30EFCDC7E9F379CE0766A68"));
        m.IsRefund.Should().BeFalse();
        m.MerchantCode.Should().Be("0023415672");
        m.Location.Should().Be(new GeoLocation(39.939423m, 32.851791m));
        m.Identity.Should().Be(new KarekodIdentity("10", "0010", "23451017", TerminalType.Pos, "12345678901234567890ABC",
            new DateTime(2020, 7, 29, 15, 30, 59), new DateTime(2020, 7, 29, 16, 30, 59)));
        (m.MerchantCategoryCode, m.Currency, m.Amount, m.CountryCode).Should().Be(("5499", "949", 150.50m, "TR"));
        (m.MerchantName, m.MerchantCity, m.PostalCode).Should().Be(("ABC GIDA", "İSTANBUL", "34100"));
        m.AdditionalData.Should().Be(new AdditionalData("TLK01230405", "903125075000", "AVMSTR", "2315634123", null, "0518894111", null, "09"));
        m.Bkm.Should().BeNull();
    }

    [Fact]
    public void Fast_sale_crc_only_matches_over_utf8()
    {
        var body = OfficialVectors.FastSale[..^4];

        Crc16Ccitt.ToHex(Crc16Ccitt.ComputeUtf8(body)).Should().Be("3F2E");
        Crc16Ccitt.ToHex(Crc16Ccitt.Compute(System.Text.Encoding.Latin1.GetBytes(body))).Should().NotBe("3F2E");
    }

    [Fact]
    public void Fast_refund()
    {
        var m = Karekod.Parse(OfficialVectors.FastRefund).Merchant!;

        m.IsRefund.Should().BeTrue();
        m.Fast!.Iban.Should().Be("TR020095000100000354000010");
        m.Refund.Should().Be(new RefundReference(new DateOnly(2020, 12, 18), "0960", "000000000000123456"));
        m.Identity!.Reference.Should().Be("REF0950D12");
        m.Identity.ExpiresAt.Should().Be(new DateTime(2022, 12, 31, 0, 0, 0));
        m.AdditionalData!.PaymentPurpose.Should().Be("00");
        (m.MerchantName, m.MerchantCity, m.Amount).Should().Be(("MERKEZ OLUMLU", "ANKARA", 150.50m));
    }

    [Fact]
    public void Fast_person_to_person()
    {
        var result = Karekod.Parse(OfficialVectors.FastPersonToPerson);

        result.Kind.Should().Be(KarekodKind.PersonToPerson);
        result.PersonToPerson.Should().Be(new PersonToPersonKarekod(
            PointOfInitiation.Dynamic, "0010", "RFR2345101", new DateTime(2020, 5, 29, 14, 1, 59), new DateTime(2020, 5, 30, 14, 1, 59),
            150.50m, "TR123456789012345678901234", "HASAN YILDIZ", "F93CC13E3E6410C1BADEEAF349E09A56", new GeoLocation(39.939423m, 32.851791m)));
    }

    [Fact]
    public void Bkm_card_payment()
    {
        var m = Karekod.Parse(OfficialVectors.BkmCard).Merchant!;

        m.PointOfInitiation.Should().Be(PointOfInitiation.Static);
        m.Bkm.Should().Be(new BkmAccount("1", "01234567890123456789", "TDVMAUJ000", "N", 3, null));
        m.Fast.Should().BeNull();
        (m.MerchantCategoryCode, m.Amount, m.MerchantName, m.MerchantCity).Should().Be(("1234", 1.23m, "ABCDEF", "ISTANBUL"));
        m.Identity!.TerminalType.Should().Be(TerminalType.Pos);
    }

    [Theory]
    [InlineData(nameof(OfficialVectors.FastShort), ShortKarekodScheme.Fast, "0010", "REF666777888", "E7054DBB31781D7A15F5043372E802C5")]
    [InlineData(nameof(OfficialVectors.BkmShort), ShortKarekodScheme.Bkm, "0800", "123456789012", "01234567890123456789012345678912")]
    public void Short_codes(string name, ShortKarekodScheme scheme, string producer, string reference, string hash)
    {
        var result = Karekod.Parse(OfficialVectors.Get(name));

        result.Kind.Should().Be(KarekodKind.ShortCode);
        result.ShortCode.Should().Be(new ShortKarekod(scheme, producer, reference, hash));
    }

    [Fact]
    public void Consumer_presented_code_is_structurally_valid()
    {
        var result = Karekod.Parse(OfficialVectors.MobileConsumerPresented);

        result.Kind.Should().Be(KarekodKind.ConsumerPresented);
        result.Value("32.01").Should().Be("T");
        result.Merchant.Should().BeNull();
    }

    [Fact]
    public void Placeholder_ibans_in_the_samples_are_flagged_as_warnings()
    {
        var result = Karekod.Parse(OfficialVectors.FastSale);

        result.Warnings.Should().ContainSingle(w => w.Code == DiagnosticCodes.IbanChecksum).Which.Path.Should().Be("30.01");
    }
}
