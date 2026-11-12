using System.Text;
using BenchmarkDotNet.Attributes;
using TrKarekod.Building;
using TrKarekod.Model;
using TrKarekod.Rendering;

namespace TrKarekod.Benchmarks;

/// <summary>Payloads are the official TCMB samples (see tests/TrKarekod.Tests/Vectors/OfficialVectors.cs).</summary>
internal static class Samples
{
    public const string FastSale =
        "00020101021230920016TR.GOV.TCMB.FAST0126TR1234567890123456789012340202012032E200C014A30EFCDC" +
        "7E9F379CE0766A684910002341567250163993942332851791519100021002040010030823451017040202052312" +
        "345678901234567890ABC0612200729153059071220072916305952045499530394954120000000150505802TR59" +
        "08ABC GIDA6008İSTANBUL61053410062750111TLK0123040502129031250750000306AVMSTR041023156341230610051" +
        "889411108020963043F2E";

    public const string FastShort = "970010REF666777888E7054DBB31781D7A15F5043372E802C55BFD";

    public static readonly string Tampered = FastSale.Replace("5908ABC GIDA", "5908ABD GIDA", StringComparison.Ordinal);
}

[MemoryDiagnoser]
public class CrcBenchmarks
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(Samples.FastSale[..^4]);

    [Benchmark(Baseline = true, Description = "CRC table-driven (391 B)")]
    public ushort Table() => Crc16Ccitt.Compute(_bytes);

    [Benchmark(Description = "CRC bitwise reference (391 B)")]
    public ushort Bitwise() => Crc16Ccitt.ComputeBitwise(_bytes);

    [Benchmark(Description = "CRC incl. UTF-8 encoding of the string")]
    public ushort Utf8String() => Crc16Ccitt.ComputeUtf8(Samples.FastSale.AsSpan(0, Samples.FastSale.Length - 4));
}

[MemoryDiagnoser]
public class ParseBenchmarks
{
    [Benchmark(Baseline = true, Description = "Parse+validate FAST sale (long, 394 chars)")]
    public bool FastSale() => Karekod.Parse(Samples.FastSale).IsValid;

    [Benchmark(Description = "Parse+validate FAST short code (54 chars)")]
    public bool FastShort() => Karekod.Parse(Samples.FastShort).IsValid;

    [Benchmark(Description = "Reject tampered FAST sale (CRC mismatch)")]
    public bool Tampered() => Karekod.Parse(Samples.Tampered).IsValid;
}

[MemoryDiagnoser]
public class BuildBenchmarks
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 30, 0);
    private static readonly byte[] Key = new byte[32];

    [Benchmark(Description = "Build signed dynamic FAST code (incl. self-validation)")]
    public string BuildDynamic() => new FastMerchantKarekodBuilder()
        .Dynamic(150.50m, Now.AddMinutes(10))
        .Iban("TR330006100519786457841326")
        .SignWith(Key)
        .Producer("0010", "23451017", Now)
        .Merchant("ABC GIDA", "İSTANBUL", "5499")
        .PaymentPurpose("01")
        .Build();

    [Benchmark(Description = "Render FAST sale PNG (8 px/module)")]
    public int RenderPng() => KarekodQr.RenderPng(Samples.FastSale, 8).Length;
}

/// <summary>Keeps the benchmarks honest: what they measure must still be correct.</summary>
internal static class Sanity
{
    public static void Check()
    {
        if (!Karekod.Parse(Samples.FastSale).IsValid || Karekod.Parse(Samples.Tampered).IsValid || !Karekod.Parse(Samples.FastShort).IsValid)
        {
            throw new InvalidOperationException("Benchmark inputs do not behave as expected.");
        }

        _ = TurkishIban.HasValidChecksum("TR330006100519786457841326") ? 0 : throw new InvalidOperationException("IBAN fixture invalid.");
    }
}
