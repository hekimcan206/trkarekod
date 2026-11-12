using TrKarekod.Schema;
using TrKarekod.Tests.Vectors;
using TrKarekod.Tlv;

namespace TrKarekod.Tests;

public sealed class SchemaValidationTests
{
    [Theory]
    [InlineData(nameof(OfficialVectors.FastSale), KarekodKind.MerchantPresented)]
    [InlineData(nameof(OfficialVectors.FastRefund), KarekodKind.MerchantPresented)]
    [InlineData(nameof(OfficialVectors.BkmCard), KarekodKind.MerchantPresented)]
    [InlineData(nameof(OfficialVectors.FastPersonToPerson), KarekodKind.PersonToPerson)]
    [InlineData(nameof(OfficialVectors.MobileConsumerPresented), KarekodKind.ConsumerPresented)]
    public void Official_long_codes_have_a_valid_crc_and_no_structural_errors(string name, KarekodKind kind)
    {
        var result = Karekod.Parse(OfficialVectors.Get(name));

        result.Kind.Should().Be(kind);
        result.CrcValid.Should().BeTrue();
        result.Errors.Where(e => e.Code is not DiagnosticCodes.IbanChecksum).Should().BeEmpty();
    }

    [Fact]
    public void Templates_are_parsed_into_their_fields()
    {
        var result = Karekod.Parse(OfficialVectors.FastSale);

        result.Value("30.00").Should().Be(KarekodSchemas.FastGuid);
        result.Value("30.01").Should().Be("TR123456789012345678901234");
        result.Value("51.07").Should().Be("200729163059");
        result.Value("62.08").Should().Be("09");
        result.Value("60").Should().Be("İSTANBUL");
        result.Value("99").Should().BeNull();
    }

    [Fact]
    public void A_single_changed_character_is_reported_as_a_crc_mismatch()
    {
        var tampered = OfficialVectors.FastSale.Replace("5908ABC GIDA", "5908XYZ GIDA", StringComparison.Ordinal);

        var result = Karekod.Parse(tampered);

        result.CrcValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == DiagnosticCodes.CrcMismatch);
    }

    [Theory]
    [InlineData("", DiagnosticCodes.UnknownFormat)]
    [InlineData("hello world", DiagnosticCodes.UnknownFormat)]
    [InlineData("000201010211", DiagnosticCodes.CrcMissing)]
    [InlineData("0002016304ZZZZ", DiagnosticCodes.CrcFormat)]
    public void Garbage_is_reported_not_thrown(string payload, string code)
    {
        var result = Karekod.Parse(payload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(d => d.Code == code);
    }

    [Fact]
    public void Oversized_input_is_rejected_before_parsing()
    {
        var result = Karekod.Parse(new string('0', 100_000));

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.PayloadTooLong);
    }

    [Fact]
    public void Lone_surrogate_is_rejected()
    {
        Karekod.Parse("000201\uD800").Errors.Should().ContainSingle().Which.Code.Should().Be(DiagnosticCodes.InvalidText);
    }

    [Fact]
    public void Missing_mandatory_fields_are_listed_with_their_paths()
    {
        var payload = Sign("000201010211");

        var result = Karekod.Parse(payload);

        result.Errors.Where(e => e.Code == DiagnosticCodes.MissingField).Select(e => e.Path)
            .Should().BeEquivalentTo(["52", "53", "58", "59", "60"]);
    }

    [Theory]
    [InlineData("5303A49", "53", DiagnosticCodes.InvalidCharacters)]
    [InlineData("53044949", "53", DiagnosticCodes.LengthOutOfRange)]
    [InlineData("5802DE", "58", DiagnosticCodes.InvalidValue)]
    [InlineData("010213", "01", DiagnosticCodes.InvalidValue)]
    public void Field_rules_are_enforced(string replacement, string path, string code)
    {
        var fields = new Dictionary<string, string>
        {
            ["01"] = "010211",
            ["52"] = "52045499",
            ["53"] = "5303949",
            ["58"] = "5802TR",
            ["59"] = "5903ABC",
            ["60"] = "6006ANKARA",
        };
        fields[path] = replacement;
        var payload = Sign("000201" + string.Concat(fields.Values));

        var result = Karekod.Parse(payload);

        result.Errors.Should().Contain(d => d.Code == code && d.Path == path);
    }

    [Fact]
    public void Not_used_and_unknown_fields_are_warnings()
    {
        var payload = Sign("000201010211520454995303949550201" + "5802TR5903ABC6006ANKARA" + "62059901X");

        var result = Karekod.Parse(payload);

        result.Warnings.Select(w => (w.Code, w.Path)).Should().Contain([(DiagnosticCodes.NotUsedInScheme, "55"), (DiagnosticCodes.UnknownField, "62.99")]);
    }

    [Fact]
    public void A_second_crc_object_is_a_duplicate()
    {
        // Structurally fine TLV, CRC correct for the bytes before it, but another object follows the CRC.
        var body = Sign("000201010211520454995303949" + "5802TR5903ABC6006ANKARA");
        var payload = Sign(body + "6103ABC");

        Karekod.Parse(payload).Errors.Should().Contain(d => d.Code == DiagnosticCodes.DuplicateId && d.Path == "63");
    }

    [Fact]
    public void Lower_case_crc_is_accepted_with_a_warning()
    {
        var payload = Sign("000201010211520454995303949" + "5802TR5903ABC6006ANKARA");
        var lower = payload[..^4] + payload[^4..].ToLowerInvariant();

        var result = Karekod.Parse(lower);

        result.CrcValid.Should().BeTrue();
        if (lower != payload)
        {
            result.Warnings.Should().Contain(d => d.Code == DiagnosticCodes.CrcFormat);
        }
    }

    [Fact]
    public void Every_diagnostic_code_is_documented()
    {
        var codes = typeof(DiagnosticCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

        DiagnosticCodes.All.Keys.Should().BeEquivalentTo(codes);
    }

    internal static string Sign(string body)
    {
        var withTag = body + "6304";
        return withTag + Crc16Ccitt.ToHex(Crc16Ccitt.ComputeUtf8(withTag));
    }

    [Fact]
    public void Schemas_list_each_id_once()
    {
        foreach (var schema in new[] { KarekodSchemas.MerchantPresented, KarekodSchemas.PersonToPerson, KarekodSchemas.AdditionalData, KarekodSchemas.Identity })
        {
            schema.Fields.Select(f => f.Id).Should().OnlyHaveUniqueItems(schema.Name);
        }

        KarekodSchemas.MerchantPresented.Fields.Should().HaveCount(100, "every ID 00-99 has a row");
        ((ITlvLayout)KarekodSchemas.MerchantPresented).TemplateLayout("62").Should().BeSameAs(KarekodSchemas.AdditionalData);
    }
}
