using TrKarekod.Tlv;

namespace TrKarekod.Tests;

public sealed class TlvTests
{
    [Fact]
    public void Parses_primitive_objects_in_order()
    {
        var diagnostics = new List<KarekodDiagnostic>();
        var nodes = TlvParser.Parse("000201010211", FlatTlvLayout.Instance, diagnostics);

        diagnostics.Should().BeEmpty();
        nodes.Select(n => (n.Id, n.Value, n.Offset)).Should().Equal(("00", "01", 0), ("01", "11", 6));
    }

    [Fact]
    public void Parses_templates_with_paths_and_absolute_offsets()
    {
        var diagnostics = new List<KarekodDiagnostic>();
        var nodes = TlvParser.Parse("00020162130103ABC0202XY", new TemplateAt("62"), diagnostics);

        diagnostics.Should().BeEmpty();
        var template = nodes[1];
        template.IsTemplate.Should().BeTrue();
        template.Children.Select(c => (c.Path, c.Value, c.Offset)).Should().Equal(("62.01", "ABC", 10), ("62.02", "XY", 17));
        template.Child("02")!.Value.Should().Be("XY");
        template.Child("09").Should().BeNull();
    }

    [Fact]
    public void Length_counts_characters_not_bytes()
    {
        var diagnostics = new List<KarekodDiagnostic>();
        var nodes = TlvParser.Parse("6008İSTANBUL610534100", FlatTlvLayout.Instance, diagnostics);

        diagnostics.Should().BeEmpty();
        nodes[0].Value.Should().Be("İSTANBUL");
        nodes[1].Value.Should().Be("34100");
    }

    [Fact]
    public void A_surrogate_pair_counts_as_one_character()
    {
        var diagnostics = new List<KarekodDiagnostic>();
        var nodes = TlvParser.Parse("5903A\U0001F600B0101X", FlatTlvLayout.Instance, diagnostics);

        diagnostics.Should().BeEmpty();
        nodes[0].Value.Should().Be("A\U0001F600B");
        nodes[1].Value.Should().Be("X");
    }

    [Theory]
    [InlineData("0002010", DiagnosticCodes.Truncated)]
    [InlineData("00050101", DiagnosticCodes.Truncated)]
    [InlineData("0A0201", DiagnosticCodes.MalformedHeader)]
    [InlineData("00X201", DiagnosticCodes.MalformedHeader)]
    [InlineData("000001", DiagnosticCodes.ZeroLength)]
    [InlineData("000201000202", DiagnosticCodes.DuplicateId)]
    public void Malformed_input_becomes_a_diagnostic(string input, string code)
    {
        var diagnostics = new List<KarekodDiagnostic>();
        TlvParser.Parse(input, FlatTlvLayout.Instance, diagnostics);

        diagnostics.Should().ContainSingle().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Writer_round_trips_through_the_parser()
    {
        var text = new TlvWriter()
            .Write("00", "01")
            .WriteTemplate("62", t => t.Write("01", "Fatura-1").Write("03", "ŞUBE"))
            .WriteTemplate("64", _ => { })
            .ToString();

        text.Should().Be("00020162200108Fatura-10304ŞUBE");
        var nodes = TlvParser.Parse(text, new TemplateAt("62"), new List<KarekodDiagnostic>());
        nodes[1].Children.Select(c => c.Value).Should().Equal("Fatura-1", "ŞUBE");
    }

    [Theory]
    [InlineData("0", "x")]
    [InlineData("AB", "x")]
    [InlineData("01", "")]
    public void Writer_rejects_invalid_objects(string id, string value)
    {
        var act = () => new TlvWriter().Write(id, value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Writer_rejects_values_longer_than_99_characters()
    {
        var act = () => new TlvWriter().Write("59", new string('A', 100));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Crc_covers_the_tag_and_length_but_not_the_value()
    {
        var payload = new TlvWriter().Write("00", "01").WriteCrcAndFinish();

        payload.Should().StartWith("0002016304");
        payload[^4..].Should().Be(Crc16Ccitt.ToHex(Crc16Ccitt.ComputeUtf8("0002016304")));
    }

    [Fact]
    public void Lone_surrogates_are_detected()
    {
        RuneText.HasLoneSurrogate("ab\uD83D").Should().BeTrue();
        RuneText.HasLoneSurrogate("a\uDE00b").Should().BeTrue();
        RuneText.HasLoneSurrogate("a\U0001F600b").Should().BeFalse();
    }

    private sealed class TemplateAt(string templateId) : ITlvLayout
    {
        public ITlvLayout? TemplateLayout(string id) => id == templateId ? FlatTlvLayout.Instance : null;
    }
}
