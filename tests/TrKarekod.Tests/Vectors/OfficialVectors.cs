namespace TrKarekod.Tests.Vectors;

/// <summary>
/// Sample payloads printed in the official TR Karekod guides, transcribed character for character. Each one reproduces
/// its published CRC (see OfficialVectorTests), which is the evidence that the transcription is exact. All account
/// numbers, names and hashes in them are the guides' own placeholders, not real data.
/// </summary>
public static class OfficialVectors
{
    public const string FastGuide = "TCMB, FAST - TR Karekod Teknik İlke ve Kuralları Rehberi, v1.4 (May 2024)";
    public const string BkmGuide = "TCMB/BKM, Kartlı Ödemeler TR Karekod Teknik İlke ve Kuralları Rehberi, v1.2 (Jan 2024)";
    public const string MobileGuide = "TCMB, Mobil Ödemeler TR Karekod İlke ve Kuralları Rehberi, v1.0 (Dec 2020)";

    /// <summary>FAST dynamic merchant sale, long code (Table 1 example). Contains "İSTANBUL": CRC only matches over UTF-8.</summary>
    public const string FastSale =
        "00020101021230920016TR.GOV.TCMB.FAST0126TR1234567890123456789012340202012032E200C014A30EFCDC" +
        "7E9F379CE0766A684910002341567250163993942332851791519100021002040010030823451017040202052312" +
        "345678901234567890ABC0612200729153059071220072916305952045499530394954120000000150505802TR59" +
        "08ABC GIDA6008İSTANBUL61053410062750111TLK0123040502129031250750000306AVMSTR041023156341230610051" +
        "889411108020963043F2E";

    /// <summary>FAST merchant refund, long code (flow type 04 with template 31).</summary>
    public const string FastRefund =
        "00020101021230920016TR.GOV.TCMB.FAST0126TR0200950001000003540000100202042032E200C014A30EFCDC" +
        "7E9F379CE0766A68313201282012180960000000000000123456491000234156755193000210020409500310REF0" +
        "950D12040202052312345678901234567890ABC06122102150000000712221231000000520454995303949541200" +
        "00000150505802TR5913MERKEZ OLUMLU6006ANKARA620608020063048B01";

    /// <summary>FAST person-to-person code (format indicator 75, Table 3 example).</summary>
    public const string FastPersonToPerson =
        "750210010212020400100310RFR234510106122005291401590712200530140159541200000001505061520126TR" +
        "1234567890123456789012340712HASAN YILDIZ1002032032F93CC13E3E6410C1BADEEAF349E09A565016399394233285179163045E7C";

    /// <summary>FAST short code (indicator 97, Table 2 example).</summary>
    public const string FastShort = "970010REF666777888E7054DBB31781D7A15F5043372E802C55BFD";

    /// <summary>BKM card payment long code (template 26).</summary>
    public const string BkmCard =
        "00020101021126680010TR.COM.BKM060110820012345678901234567890910TDVMAUJ0001001N110203491" +
        "000234156725195000210020400010312180904121314040202052312345678901234567890ABC06122005291" +
        "40159071220052915015952041234530394954120000000001235802TR5906ABCDEF6008ISTANBUL6304C2B6";

    /// <summary>BKM short code (indicator 99).</summary>
    public const string BkmShort = "9908001234567890120123456789012345678901234567891280BE";

    /// <summary>Consumer-presented code (format indicator 85) from the mobile payments guide.</summary>
    public const string MobileConsumerPresented =
        "85021001021102040742030817652401040110612201002140000071220123123595932410101T0208" +
        "231458770310592623121404066123452032F2675AB8646A538D318346558012A76450163993943232" +
        "85172063048C75";

    public static TheoryData<string, string> All => new()
    {
        { nameof(FastSale), FastSale },
        { nameof(FastRefund), FastRefund },
        { nameof(FastPersonToPerson), FastPersonToPerson },
        { nameof(FastShort), FastShort },
        { nameof(BkmCard), BkmCard },
        { nameof(BkmShort), BkmShort },
        { nameof(MobileConsumerPresented), MobileConsumerPresented },
    };

    public static string Get(string name) => name switch
    {
        nameof(FastSale) => FastSale,
        nameof(FastRefund) => FastRefund,
        nameof(FastPersonToPerson) => FastPersonToPerson,
        nameof(FastShort) => FastShort,
        nameof(BkmCard) => BkmCard,
        nameof(BkmShort) => BkmShort,
        nameof(MobileConsumerPresented) => MobileConsumerPresented,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
