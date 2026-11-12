using System.Text;
using FsCheck.Xunit;

namespace TrKarekod.Tests;

public sealed class Crc16CcittTests
{
    [Fact]
    public void Matches_the_crc_catalogue_check_value()
    {
        // CRC-16/CCITT-FALSE check value for the ASCII string "123456789" (Greg Cook's CRC RevEng catalogue).
        Crc16Ccitt.Compute("123456789"u8).Should().Be(0x29B1);
    }

    [Fact]
    public void Empty_input_yields_the_initial_value()
    {
        Crc16Ccitt.Compute([]).Should().Be(0xFFFF);
    }

    [Theory]
    [InlineData(0x0000, "0000")]
    [InlineData(0x3F2E, "3F2E")]
    [InlineData(0x00AB, "00AB")]
    public void Formats_as_four_upper_case_hex_digits(int crc, string expected)
    {
        Crc16Ccitt.ToHex((ushort)crc).Should().Be(expected);
    }

    [Fact]
    public void Hashes_text_as_utf8_bytes()
    {
        Crc16Ccitt.ComputeUtf8("İSTANBUL").Should().Be(Crc16Ccitt.Compute(Encoding.UTF8.GetBytes("İSTANBUL")));
        Crc16Ccitt.ComputeUtf8("İSTANBUL").Should().NotBe(Crc16Ccitt.Compute(Encoding.Latin1.GetBytes("İSTANBUL")));
    }

    [Fact]
    public void Long_text_beyond_the_stack_buffer_is_handled()
    {
        var text = new string('Ş', 2000);
        Crc16Ccitt.ComputeUtf8(text).Should().Be(Crc16Ccitt.ComputeBitwise(Encoding.UTF8.GetBytes(text)));
    }

    [Property(MaxTest = 500)]
    public bool Table_driven_path_equals_the_bitwise_reference(byte[] data) =>
        Crc16Ccitt.Compute(data) == Crc16Ccitt.ComputeBitwise(data);

    [Property(MaxTest = 500)]
    public bool Any_single_bit_flip_changes_the_checksum(byte[] data, int position)
    {
        if (data.Length == 0)
        {
            return true;
        }

        var bit = (int)((uint)position % (uint)(data.Length * 8));
        var flipped = (byte[])data.Clone();
        flipped[bit / 8] ^= (byte)(1 << (bit % 8));
        return Crc16Ccitt.Compute(data) != Crc16Ccitt.Compute(flipped);
    }
}
