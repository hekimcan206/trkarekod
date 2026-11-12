using System.Collections;
using QRCoder;

namespace TrKarekod.Rendering;

/// <summary>
/// Turns a payload into a QR symbol. Byte mode with UTF-8 and no ECI header: the CRC is defined over UTF-8 bytes, so
/// the symbol must carry exactly those bytes, and readers in payer apps detect UTF-8 without an ECI designator
/// (see docs/adr/0004). Error correction level M, the level the guides' sample symbols use.
/// </summary>
public static class KarekodQr
{
    /// <summary>Renders a PNG with a 4-module quiet zone.</summary>
    /// <param name="pixelsPerModule">Size of one module in pixels (1-40).</param>
    public static byte[] RenderPng(string payload, int pixelsPerModule = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerModule, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pixelsPerModule, 40);
        using var data = Encode(payload);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>The module matrix including the quiet zone; true is a dark module.</summary>
    public static bool[,] Modules(string payload)
    {
        using var data = Encode(payload);
        var rows = data.ModuleMatrix;
        var size = rows.Count;
        var modules = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            BitArray row = rows[y];
            for (var x = 0; x < size; x++)
            {
                modules[y, x] = row[x];
            }
        }

        return modules;
    }

    /// <summary>The QR version (1-40) the payload needs at level M.</summary>
    public static int Version(string payload)
    {
        using var data = Encode(payload);
        return data.Version;
    }

    private static QRCodeData Encode(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        var result = Karekod.Parse(payload);
        if (!result.IsValid)
        {
            throw new ArgumentException("Refusing to render an invalid payload: " + string.Join("; ", result.Errors), nameof(payload));
        }

        return QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.M, forceUtf8: true, utf8BOM: false, eciMode: QRCodeGenerator.EciMode.Default);
    }
}
