using System.Text.Json.Nodes;
using TrKarekod.Cli;
using TrKarekod.Tests.Vectors;

namespace TrKarekod.Tests;

public sealed class CliTests
{
    private const string ValidIban = "TR330006100519786457841326";

    [Fact]
    public void Parse_explains_a_valid_code_with_field_names()
    {
        var (code, output, _) = Run("parse", OfficialVectors.FastSale);

        code.Should().Be(0);
        output.Should().StartWith("MerchantPresented: VALID (CRC ok)");
        output.Should().Contain("60     Merchant city").And.Contain("İSTANBUL");
        output.Should().Contain("warning TRK045 [30.01]");
    }

    [Fact]
    public void Parse_returns_1_and_the_diagnostics_for_an_invalid_code()
    {
        var tampered = OfficialVectors.FastSale.Replace("ABC GIDA", "ABD GIDA", StringComparison.Ordinal);

        var (code, output, _) = Run("parse", tampered);

        code.Should().Be(1);
        output.Should().Contain("error TRK011 [63]");
    }

    [Fact]
    public void Parse_json_is_machine_readable()
    {
        var (code, output, _) = Run("parse", "--json", OfficialVectors.FastShort);

        code.Should().Be(0);
        var json = JsonNode.Parse(output)!;
        json["kind"]!.GetValue<string>().Should().Be("ShortCode");
        json["valid"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Parse_reads_stdin()
    {
        var (code, _, _) = RunWithInput(OfficialVectors.FastPersonToPerson + "\n", "parse", "-");

        code.Should().Be(0);
    }

    [Fact]
    public void Build_fast_dynamic_produces_a_valid_signed_code_and_a_png()
    {
        var png = Path.Combine(Path.GetTempPath(), $"trkarekod-{Guid.NewGuid():N}.png");
        try
        {
            var (code, output, error) = Run("build", "fast", "--iban", ValidIban, "--name", "Kafe 42", "--city", "İZMİR",
                "--producer", "0010", "--reference", "INV-0042", "--purpose", "01", "--amount", "87.50", "--expires-minutes", "10",
                "--key-hex", "00112233445566778899AABBCCDDEEFF", "--png", png);

            code.Should().Be(0, error);
            var merchant = Karekod.Parse(output.Trim()).Merchant!;
            merchant.IsDynamic.Should().BeTrue();
            merchant.Amount.Should().Be(87.50m);
            Model.ProducerHash.Verify(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"), merchant).Should().BeTrue();
            File.ReadAllBytes(png)[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        }
        finally
        {
            File.Delete(png);
        }
    }

    [Fact]
    public void Build_fast_reports_why_it_refuses()
    {
        var (code, _, error) = Run("build", "fast", "--iban", "TR123456789012345678901234", "--name", "X", "--city", "ANKARA",
            "--producer", "10", "--reference", "R", "--purpose", "01");

        code.Should().Be(1);
        error.Should().Contain("TRK045");
    }

    [Fact]
    public void Build_p2p_produces_a_valid_code()
    {
        var (code, output, error) = Run("build", "p2p", "--iban", ValidIban, "--name", "AYŞE ÇELİK", "--producer", "10", "--amount", "25");

        code.Should().Be(0, error);
        Karekod.Parse(output.Trim()).PersonToPerson!.RecipientName.Should().Be("AYŞE ÇELİK");
    }

    [Fact]
    public void Vectors_self_check_passes()
    {
        var (code, output, _) = Run("vectors");

        code.Should().Be(0);
        output.Should().Contain($"{VectorCatalog.All.Count} passed, 0 failed");
    }

    [Theory]
    [InlineData]
    [InlineData("frobnicate")]
    [InlineData("parse")]
    [InlineData("build", "fast", "--iban")]
    [InlineData("build", "fast", "--amount", "abc")]
    public void Usage_errors_return_2(params string[] args)
    {
        var (code, _, error) = Run(args);

        code.Should().Be(2);
        error.Should().Contain("usage:");
    }

    [Fact]
    public void Codes_lists_every_diagnostic()
    {
        var (_, output, _) = Run("codes");

        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(DiagnosticCodes.All.Count);
    }

    private static (int Code, string Output, string Error) Run(params string[] args) => RunWithInput(string.Empty, args);

    private static (int Code, string Output, string Error) RunWithInput(string stdin, params string[] args)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliApp.Run(args, input, output, error);
        return (code, output.ToString().ReplaceLineEndings("\n"), error.ToString());
    }
}
