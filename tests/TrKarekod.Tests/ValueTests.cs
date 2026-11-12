using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using TrKarekod.Model;

namespace TrKarekod.Tests;

public sealed class ValueTests
{
    [Theory]
    [InlineData("TR330006100519786457841326", true)]
    [InlineData("TR340006100519786457841326", false)]
    public void Iban_checksum(string iban, bool valid)
    {
        TurkishIban.IsWellFormed(iban).Should().BeTrue();
        TurkishIban.HasValidChecksum(iban).Should().Be(valid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TR33000610051978645784132")]
    [InlineData("DE89370400440532013000ABCD")]
    [InlineData("TR33000610051978645784132A")]
    public void Malformed_ibans(string? iban)
    {
        TurkishIban.IsWellFormed(iban).Should().BeFalse();
    }

    [Property(MaxTest = 300)]
    public bool Created_ibans_pass_the_checksum(PositiveInt bank, NonNegativeInt account)
    {
        var iban = TurkishIban.Create((bank.Get % 100_000).ToString(System.Globalization.CultureInfo.InvariantCulture),
            account.Get.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return TurkishIban.HasValidChecksum(iban) && iban.Length == 26;
    }

    [Theory]
    [InlineData(150.50, "000000015050")]
    [InlineData(0.01, "000000000001")]
    [InlineData(9999999999.99, "999999999999")]
    public void Amount_formatting(decimal amount, string expected)
    {
        KarekodAmount.Format12(amount).Should().Be(expected);
        KarekodAmount.TryParse(expected, out var back).Should().BeTrue();
        back.Should().Be(amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.005)]
    [InlineData(10000000000)]
    public void Amounts_that_cannot_be_written(decimal amount)
    {
        var act = () => KarekodAmount.Format12(amount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Property(MaxTest = 300)]
    public Property Amount_round_trips()
    {
        var kurus = Gen.Choose(1, int.MaxValue).Select(k => k / 100m);
        return Prop.ForAll(kurus.ToArbitrary(), amount =>
            KarekodAmount.TryParse(KarekodAmount.Format12(amount), out var back) && back == amount);
    }

    [Fact]
    public void Location_formats_like_the_guide()
    {
        new GeoLocation(39.939423m, 32.851791m).Format().Should().Be("3993942332851791");
        GeoLocation.TryParse("50163993942332851791"[4..], out var parsed).Should().BeTrue();
        parsed.Should().Be(new GeoLocation(39.939423m, 32.851791m));
        new GeoLocation(41.0082m, 28.9784m).Format(8).Should().Be("41008200002897840000");
    }

    [Fact]
    public void Location_outside_the_format_is_refused()
    {
        var act = () => new GeoLocation(-12.5m, 130m).Format();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refund_reference_round_trips()
    {
        var reference = new RefundReference(new DateOnly(2020, 12, 18), "0062", "123456");

        reference.Format().Should().Be("2012180062000000000000123456");
        RefundReference.TryParse(reference.Format(), out var back).Should().BeTrue();
        back.Should().Be(reference with { QueryNumber = "000000000000123456" });
    }

    [Fact]
    public void Timestamps_cover_two_digit_years()
    {
        KarekodTimestamp.Format12(new DateTime(2020, 7, 29, 15, 30, 59)).Should().Be("200729153059");
        var act = () => KarekodTimestamp.Format12(new DateTime(1999, 1, 1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
