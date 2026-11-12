using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using TrKarekod.Building;
using TrKarekod.Model;
using TrKarekod.Tests.Vectors;

namespace TrKarekod.Tests;

public sealed class BuilderTests
{
    private const string ValidIban = "TR330006100519786457841326";
    private static readonly DateTime Now = new(2026, 11, 12, 10, 30, 0);

    [Fact]
    public void Reproduces_the_official_fast_sale_byte_for_byte()
    {
        var payload = new FastMerchantKarekodBuilder()
            .Dynamic(150.50m, new DateTime(2020, 7, 29, 16, 30, 59))
            .Iban("TR123456789012345678901234")
            .Hash("E200C014A30EFCDC7E9F379CE0766A68")
            .MerchantCode("0023415672")
            .Location(new GeoLocation(39.939423m, 32.851791m))
            .Producer("0010", "23451017", new DateTime(2020, 7, 29, 15, 30, 59))
            .Terminal(TerminalType.Pos, "12345678901234567890ABC")
            .Merchant("ABC GIDA", "İSTANBUL", mcc: "5499")
            .PostalCode("34100")
            .AdditionalData(billNumber: "TLK01230405", mobileNumber: "903125075000", storeLabel: "AVMSTR", loyaltyNumber: "2315634123", customerLabel: "0518894111")
            .PaymentPurpose("09")
            .Build(allowWarnings: true);

        payload.Should().Be(OfficialVectors.FastSale);
    }

    [Fact]
    public void Reproduces_the_official_fast_refund_byte_for_byte()
    {
        var payload = new FastMerchantKarekodBuilder()
            .Refund(new RefundReference(new DateOnly(2020, 12, 18), "0960", "123456"), 150.50m, new DateTime(2022, 12, 31, 0, 0, 0))
            .Iban("TR020095000100000354000010")
            .Hash("E200C014A30EFCDC7E9F379CE0766A68")
            .MerchantCode("0023415675")
            .Producer("0950", "REF0950D12", new DateTime(2021, 2, 15, 0, 0, 0))
            .Terminal(TerminalType.Pos, "12345678901234567890ABC")
            .Merchant("MERKEZ OLUMLU", "ANKARA", mcc: "5499")
            .Build(allowWarnings: true);

        payload.Should().Be(OfficialVectors.FastRefund);
    }

    [Fact]
    public void Reproduces_the_official_person_to_person_code_byte_for_byte()
    {
        var payload = new PersonToPersonKarekodBuilder()
            .Dynamic("RFR2345101")
            .Producer("0010")
            .Validity(new DateTime(2020, 5, 29, 14, 1, 59), new DateTime(2020, 5, 30, 14, 1, 59))
            .Amount(150.50m)
            .Recipient("TR123456789012345678901234", "HASAN YILDIZ")
            .Hash("F93CC13E3E6410C1BADEEAF349E09A56")
            .Location(new GeoLocation(39.939423m, 32.851791m))
            .Build(allowWarnings: true);

        payload.Should().Be(OfficialVectors.FastPersonToPerson);
    }

    [Theory]
    [InlineData(ShortKarekodScheme.Fast, "10", "REF666777888", "E7054DBB31781D7A15F5043372E802C5", OfficialVectors.FastShort)]
    [InlineData(ShortKarekodScheme.Bkm, "0800", "123456789012", "01234567890123456789012345678912", OfficialVectors.BkmShort)]
    public void Reproduces_the_official_short_codes(ShortKarekodScheme scheme, string producer, string reference, string hash, string expected)
    {
        ShortKarekodBuilder.Build(scheme, producer, reference, hash).Should().Be(expected);
    }

    [Fact]
    public void Placeholder_iban_is_refused_unless_warnings_are_allowed()
    {
        var builder = MinimalStatic().Iban("TR123456789012345678901234");

        var act = () => builder.Build();

        act.Should().Throw<KarekodBuildException>().Which.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.IbanChecksum);
    }

    [Fact]
    public void A_static_sticker_without_amount_is_valid()
    {
        var payload = MinimalStatic().Build();

        var merchant = Karekod.Parse(payload).Merchant!;
        merchant.IsDynamic.Should().BeFalse();
        merchant.Amount.Should().BeNull();
        merchant.Fast!.FlowType.Should().Be(FastFlowType.StaticVerification);
    }

    [Fact]
    public void Missing_payment_purpose_is_refused()
    {
        var act = () => new FastMerchantKarekodBuilder().Iban(ValidIban).Hash(new string('A', 32))
            .Producer("10", "R1", Now).Merchant("ABC", "ANKARA").Build();

        act.Should().Throw<KarekodBuildException>().Which.Diagnostics.Should().Contain(d => d.Path == "62.08");
    }

    [Theory]
    [InlineData("A name that is longer than 25 characters", "ANKARA", DiagnosticCodes.LengthOutOfRange)]
    [InlineData("ABC", "KAHRAMANMARAŞ MERKEZ", DiagnosticCodes.LengthOutOfRange)]
    public void Field_limits_are_enforced(string name, string city, string code)
    {
        var act = () => MinimalStatic().Merchant(name, city).Build();

        act.Should().Throw<KarekodBuildException>().Which.Diagnostics.Should().Contain(d => d.Code == code);
    }

    [Fact]
    public void Value_longer_than_99_characters_is_refused_with_a_build_exception()
    {
        var act = () => MinimalStatic().AdditionalData(billNumber: new string('1', 25), storeLabel: new string('2', 25), loyaltyNumber: new string('3', 25), customerLabel: new string('4', 25)).Build();

        act.Should().Throw<KarekodBuildException>();
    }

    [Fact]
    public void Expiry_before_creation_is_refused()
    {
        var act = () => MinimalStatic().Dynamic(10m, Now.AddMinutes(-1)).Build();

        act.Should().Throw<KarekodBuildException>().Which.Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.ExpiryBeforeCreation);
    }

    [Fact]
    public void Signed_codes_verify_and_tampering_is_detected()
    {
        var key = "0123456789abcdef0123456789abcdef"u8.ToArray();
        var payload = MinimalStatic().Dynamic(99.90m, Now.AddMinutes(10)).SignWith(key).Build();
        var merchant = Karekod.Parse(payload).Merchant!;

        ProducerHash.Verify(key, merchant).Should().BeTrue();
        ProducerHash.Verify("another-key-of-sufficient-len"u8, merchant).Should().BeFalse();
        ProducerHash.Verify(key, merchant with { Amount = 9.99m }).Should().BeFalse();
        ProducerHash.Verify(key, merchant with { Fast = merchant.Fast! with { Iban = "TR760006100519786457841327" } }).Should().BeFalse();
    }

    [Property(MaxTest = 200)]
    public Property Built_fast_codes_parse_back_to_their_inputs()
    {
        var inputs =
            from kurus in Gen.Choose(1, 99_999_999)
            from name in Gen.Elements("ABC GIDA", "Şükrü Çiğköfte", "ÖZ İĞNE", "Kafe 42", "Ünlü Börekçi")
            from city in Gen.Elements("İSTANBUL", "ANKARA", "Iğdır", "Muş", "Çanakkale")
            from minutes in Gen.Choose(1, 24 * 60)
            from account in Gen.Choose(1, int.MaxValue)
            from reference in Gen.Choose(1, int.MaxValue)
            select (Amount: kurus / 100m, name, city, minutes, Iban: TurkishIban.Create("00061", account.ToString(System.Globalization.CultureInfo.InvariantCulture)), Reference: "R" + reference);

        return Prop.ForAll(inputs.ToArbitrary(), x =>
        {
            var payload = new FastMerchantKarekodBuilder()
                .Dynamic(x.Amount, Now.AddMinutes(x.minutes))
                .Iban(x.Iban)
                .SignWith(new byte[32])
                .Producer("0010", x.Reference, Now)
                .Merchant(x.name, x.city, "5499")
                .PaymentPurpose("01")
                .Build();

            var m = Karekod.Parse(payload).Merchant!;
            return m.Amount == x.Amount && m.MerchantName == x.name && m.MerchantCity == x.city && m.Fast!.Iban == x.Iban
                && m.Identity!.Reference == x.Reference && m.Identity.ExpiresAt == Now.AddMinutes(x.minutes) && ProducerHash.Verify(new byte[32], m);
        });
    }

    [Property(MaxTest = 200)]
    public Property Any_single_character_substitution_is_detected()
    {
        var payload = OfficialVectors.FastSale;
        var edits =
            from position in Gen.Choose(0, payload.Length - 5)
            from replacement in Gen.Elements("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ ÇĞİÖŞÜ".ToCharArray())
            where payload[position] != replacement
            select (position, replacement);

        return Prop.ForAll(edits.ToArbitrary(), e =>
        {
            var tampered = string.Concat(payload.AsSpan(0, e.position), e.replacement.ToString(), payload.AsSpan(e.position + 1));
            var result = Karekod.Parse(tampered);
            return !result.IsValid && !result.CrcValid;
        });
    }

    private static FastMerchantKarekodBuilder MinimalStatic() => new FastMerchantKarekodBuilder()
        .Static()
        .Iban(ValidIban)
        .Hash("0123456789ABCDEF0123456789ABCDEF")
        .Producer("0010", "STICKER01", Now)
        .Merchant("ABC GIDA", "ANKARA", "5499")
        .PaymentPurpose("01");
}
