using TrKarekod.Rendering;
using TrKarekod.Tests.Vectors;
using ZXing;
using ZXing.Common;

namespace TrKarekod.Tests;

/// <summary>
/// Renders each official sample as a QR symbol and decodes it with an independent reader (ZXing). This is what proves
/// the symbol carries exactly the UTF-8 bytes the CRC was computed over, including the dotted capital I.
/// </summary>
public sealed class QrRoundTripTests
{
    [Theory]
    [MemberData(nameof(OfficialVectors.All), MemberType = typeof(OfficialVectors))]
    public void Rendered_symbol_decodes_to_the_exact_payload(string name, string payload)
    {
        var decoded = Decode(KarekodQr.Modules(payload));

        decoded.Should().Be(payload, name);
        Karekod.Parse(decoded!).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Png_output_is_a_png()
    {
        var png = KarekodQr.RenderPng(OfficialVectors.FastSale, pixelsPerModule: 4);

        png[..8].Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }

    [Fact]
    public void Invalid_payloads_are_not_rendered()
    {
        var act = () => KarekodQr.RenderPng("000201");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(nameof(OfficialVectors.FastShort), 4)]
    [InlineData(nameof(OfficialVectors.FastSale), 15)]
    public void Symbol_versions_at_level_m(string name, int version)
    {
        KarekodQr.Version(OfficialVectors.Get(name)).Should().Be(version, "a short code is a 33x33 symbol, the full FAST sale 77x77");
    }

    private static string? Decode(bool[,] modules, int scale = 4)
    {
        var size = modules.GetLength(0);
        var width = size * scale;
        var pixels = new byte[width * width];
        for (var y = 0; y < width; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = modules[y / scale, x / scale] ? (byte)0 : (byte)255;
            }
        }

        var source = new RGBLuminanceSource(pixels, width, width, RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true },
        };
        return reader.Decode(source)?.Text;
    }
}
