using System.Buffers;
using System.Text;

namespace TrKarekod;

/// <summary>
/// CRC-16/CCITT-FALSE (ISO/IEC 13239 polynomial 0x1021, initial value 0xFFFF, no reflection, no final XOR) as required
/// by EMVCo Merchant-Presented Mode and every TR Karekod guide.
/// </summary>
/// <remarks>
/// The checksum covers the UTF-8 bytes of the payload up to and including the CRC tag and length ("6304"). The official
/// FAST sample with "İSTANBUL" only reproduces its published CRC (3F2E) when the dotted capital I is hashed as its two
/// UTF-8 bytes, so the encoding is not a free choice (see docs/adr/0001).
/// </remarks>
public static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    // EMVCo caps a payload at 512 characters; at most 3 UTF-8 bytes per BMP character keeps this on the stack.
    private const int StackLimitBytes = 1536;

    private static readonly ushort[] Table = BuildTable();

    /// <summary>Computes the checksum of raw bytes.</summary>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        var crc = InitialValue;
        foreach (var b in data)
        {
            crc = (ushort)((crc << 8) ^ Table[((crc >> 8) ^ b) & 0xFF]);
        }

        return crc;
    }

    /// <summary>Computes the checksum of the UTF-8 encoding of <paramref name="text"/>.</summary>
    public static ushort ComputeUtf8(ReadOnlySpan<char> text)
    {
        var max = Encoding.UTF8.GetMaxByteCount(text.Length);
        byte[]? rented = null;
        var buffer = max <= StackLimitBytes ? stackalloc byte[StackLimitBytes] : (rented = ArrayPool<byte>.Shared.Rent(max));
        try
        {
            var written = Encoding.UTF8.GetBytes(text, buffer);
            return Compute(buffer[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Formats a checksum the way it is written into a payload: four upper-case hexadecimal digits.</summary>
    public static string ToHex(ushort crc) => crc.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reference bit-by-bit implementation. It is slower than the table but obviously correct, so tests and benchmarks
    /// use it as the oracle for the table-driven path.
    /// </summary>
    internal static ushort ComputeBitwise(ReadOnlySpan<byte> data)
    {
        var crc = InitialValue;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ Polynomial) : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    private static ushort[] BuildTable()
    {
        var table = new ushort[256];
        for (var i = 0; i < 256; i++)
        {
            var crc = (ushort)(i << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ Polynomial) : (ushort)(crc << 1);
            }

            table[i] = crc;
        }

        return table;
    }
}
