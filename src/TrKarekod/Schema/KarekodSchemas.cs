using System.Globalization;
using static TrKarekod.Schema.KarekodDataType;
using static TrKarekod.Schema.Presence;

namespace TrKarekod.Schema;

/// <summary>
/// The data-organisation tables of TR Karekod as data. Sources: TCMB "FAST - TR Karekod Teknik İlke ve Kuralları
/// Rehberi" v1.4 (May 2024), TCMB/BKM "Kartlı Ödemeler TR Karekod Teknik İlke ve Kuralları Rehberi" v1.2 (Jan 2024)
/// and TCMB "Mobil Ödemeler TR Karekod İlke ve Kuralları Rehberi" v1.0 (Dec 2020). See docs/adr/0002.
/// </summary>
public static class KarekodSchemas
{
    /// <summary>GUID (reverse domain) of the FAST merchant account template, ID 30.00.</summary>
    public const string FastGuid = "TR.GOV.TCMB.FAST";

    /// <summary>GUID (reverse domain) of the BKM card payment template, ID 26.00.</summary>
    public const string BkmGuid = "TR.COM.BKM";

    /// <summary>Merchant account information template for FAST (ID 30).</summary>
    public static FieldSchema FastAccount { get; } = new("FAST merchant account (30)",
    [
        new("00", "Globally unique identifier", "Evrensel Tekil Kimlik", SpecialAlphanumeric, 16, 16, Mandatory, AllowedValues: [FastGuid]),
        new("01", "Merchant IBAN", "İşyeri IBAN", SpecialAlphanumeric, 26, 26, Mandatory),
        new("02", "Flow type", "Karekod Akış Türü", Numeric, 2, 2, Mandatory, AllowedValues: ["01", "02", "04"]),
        new("20", "Security hash", "Özet Güvenlik Değeri", SpecialAlphanumeric, 32, 32, Mandatory),
    ]);

    /// <summary>FAST refund template (ID 31).</summary>
    public static FieldSchema FastRefund { get; } = new("FAST refund (31)",
    [
        new("01", "Related message reference", "İlgili Mesaj Referansı", SpecialAlphanumeric, 28, 28, Conditional),
    ]);

    /// <summary>BKM card payment template (ID 26).</summary>
    public static FieldSchema BkmAccount { get; } = new("BKM card payment (26)",
    [
        new("00", "Globally unique identifier", "Evrensel Tekil Kimlik", SpecialAlphanumeric, 10, 10, Mandatory, AllowedValues: [BkmGuid]),
        new("06", "Transaction type", "İşlem Tipi", Numeric, 1, 1, Mandatory, AllowedValues: ["1", "2", "3", "4"]),
        new("08", "Security hash", "Özet Güvenlik Değeri", SpecialAlphanumeric, 1, 32, Mandatory),
        new("09", "Supported card schemes", "Şemalar", SpecialAlphanumeric, 1, 10, Mandatory),
        new("10", "Loyalty brand", "Marka", SpecialAlphanumeric, 1, 1, Mandatory, AllowedValues: ["A", "B", "F", "M", "P", "W", "Z", "N"]),
        new("11", "Installment count", "Taksit sayısı", Numeric, 2, 2, Optional),
        new("13", "Original RRN (refunds)", "RRN", Numeric, 16, 16, Conditional),
    ]);

    /// <summary>
    /// Mobile payments template (ID 32). Its sub-fields belong to the mobile payment scheme, so they are accepted
    /// without interpretation.
    /// </summary>
    public static FieldSchema MobileAccount { get; } = new("Mobile payments (32)", [], UnknownFieldHandling.Accept);

    /// <summary>Templates reserved for other schemes (27-29, 33-46): structure is checked, content is not.</summary>
    public static FieldSchema ReservedAccount { get; } = new("Reserved merchant account template", [], UnknownFieldHandling.Accept);

    /// <summary>Karekod identity template (ID 51).</summary>
    public static FieldSchema Identity { get; } = new("Karekod identity (51)",
    [
        new("00", "TR Karekod version", "Sürüm Numarası", Numeric, 2, 2, Mandatory),
        new("02", "Producer code", "Karekod Üretici Kodu", Numeric, 4, 4, Mandatory),
        new("03", "Reference number", "Karekod Referans Numarası", SpecialAlphanumeric, 1, 12, Mandatory),
        new("04", "Terminal type", "Terminal Tipi", Numeric, 2, 2, Optional, AllowedValues: ["01", "02", "03", "04", "05", "06"]),
        new("05", "Terminal serial number", "Terminal Cihaz Seri Numarası", SpecialAlphanumeric, 1, 23, Optional),
        new("06", "Created at", "Oluşturulma Zamanı", Numeric, 12, 12, Mandatory),
        new("07", "Expires at", "Son Geçerlilik Zamanı", Numeric, 12, 12, Conditional),
    ]);

    /// <summary>Additional data field template (ID 62).</summary>
    public static FieldSchema AdditionalData { get; } = new("Additional data (62)",
    [
        new("01", "Bill number", "Fatura Numarası", SpecialAlphanumeric, 1, 25, Optional),
        new("02", "Mobile number", "Müşterinin Telefon Numarası", SpecialAlphanumeric, 1, 15, Optional),
        new("03", "Store label", "Mağaza Etiketi", SpecialAlphanumeric, 1, 25, Optional),
        new("04", "Loyalty number", "Müşteri Sadakat Numarası", SpecialAlphanumeric, 1, 25, Optional),
        new("05", "Reference label", "Referans Etiketi", SpecialAlphanumeric, 1, 25, Optional),
        new("06", "Customer label", "Müşteri Numarası", SpecialAlphanumeric, 1, 25, Optional),
        new("07", "Terminal label", "Terminal Etiketi", SpecialAlphanumeric, 1, 25, Optional),
        new("08", "Purpose of transaction", "Ödeme amacı", SpecialAlphanumeric, 2, 2, Conditional),
        new("09", "Additional consumer data request", "Ek Tüketici Verileri", SpecialAlphanumeric, 1, 3, NotUsed),
    ]);

    /// <summary>Root table of the merchant-presented long code (format indicator 00 = "01").</summary>
    public static FieldSchema MerchantPresented { get; } = new("Merchant-presented TR Karekod", BuildMerchantRoot());

    /// <summary>Application template of the person-to-person code (ID 61 under format indicator 75).</summary>
    public static FieldSchema PersonToPersonApplication { get; } = new("Person-to-person application (61)",
    [
        new("01", "Recipient IBAN", "Hesap Numarası (IBAN)", SpecialAlphanumeric, 26, 26, Mandatory),
        new("02", "Card number", "Kart Numarası", Numeric, 16, 16, NotUsed),
        new("04", "Easy address type", "Kolay Adres Tipi", KarekodDataType.Text, 1, 1, NotUsed),
        new("05", "Easy address", "Kolay adresi değeri", SpecialAlphanumeric, 1, 50, NotUsed),
        new("07", "Recipient name", "Ödeme Alıcısının Adı ve Soyadı", SpecialAlphanumeric, 2, 26, Mandatory),
        new("10", "Flow type", "Karekod Akış Türü", Numeric, 2, 2, Mandatory, AllowedValues: ["03"]),
    ]);

    /// <summary>Root table of the FAST person-to-person code (format indicator 75 = "10").</summary>
    public static FieldSchema PersonToPerson { get; } = new("Person-to-person TR Karekod",
    [
        new("75", "Format indicator", "Karekod Biçim Göstergesi", Numeric, 2, 2, Mandatory, AllowedValues: ["10"]),
        new("01", "Point of initiation", "Karekod Türü", Numeric, 2, 2, Mandatory, AllowedValues: ["11", "12"]),
        new("02", "Producer code", "Karekod Üretici Kodu", Numeric, 4, 4, Mandatory),
        new("03", "Reference number", "Karekod Referans Numarası", SpecialAlphanumeric, 1, 12, Conditional),
        new("06", "Created at", "Oluşturulma Zamanı", Numeric, 12, 12, Optional),
        new("07", "Expires at", "Son Geçerlilik Zamanı", Numeric, 12, 12, Optional),
        new("54", "Amount", "Tutar", Numeric, 12, 12, Optional),
        new("61", "Application template", "Uygulama Şablonu", SpecialAlphanumeric, 1, 99, Mandatory, PersonToPersonApplication),
        new("20", "Security hash", "Özet Güvenlik Değeri", SpecialAlphanumeric, 1, 32, Optional),
        new("50", "Location", "Konum", Numeric, 16, 34, Optional),
        new("63", "CRC", "CRC", SpecialAlphanumeric, 4, 4, Mandatory),
    ]);

    /// <summary>
    /// Root of the consumer-presented code (format indicator 85). Only the TLV structure and the CRC are checked:
    /// typed consumer-presented support is out of scope (see README, Limitations).
    /// </summary>
    public static FieldSchema ConsumerPresented { get; } = new("Consumer-presented TR Karekod",
    [
        new("85", "Format indicator", "Karekod Biçim Göstergesi", Numeric, 2, 2, Mandatory, AllowedValues: ["10"]),
        new("32", "Mobile payments template", "Mobil ödeme şablonu", SpecialAlphanumeric, 1, 99, Optional, MobileAccount),
        new("63", "CRC", "CRC", SpecialAlphanumeric, 4, 4, Mandatory),
    ], UnknownFieldHandling.Accept);

    /// <summary>The root table of a usage model, or null for short codes and unknown input.</summary>
    public static FieldSchema? RootOf(KarekodKind kind) => kind switch
    {
        KarekodKind.MerchantPresented => MerchantPresented,
        KarekodKind.PersonToPerson => PersonToPerson,
        KarekodKind.ConsumerPresented => ConsumerPresented,
        _ => null,
    };

    /// <summary>The table row for a dotted path such as <c>62.08</c>, or null when the path is not defined.</summary>
    public static FieldSpec? FieldAt(KarekodKind kind, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var schema = RootOf(kind);
        FieldSpec? spec = null;
        foreach (var id in path.Split('.'))
        {
            spec = schema?.Find(id);
            if (spec is null)
            {
                return null;
            }

            schema = spec.Template;
        }

        return spec;
    }

    private static List<FieldSpec> BuildMerchantRoot()
    {
        var rows = new List<FieldSpec>
        {
            new("00", "Payload format indicator", "Karekod Biçim Göstergesi", Numeric, 2, 2, Mandatory, AllowedValues: ["01"]),
            new("01", "Point of initiation", "Karekod Türü", Numeric, 2, 2, Mandatory, AllowedValues: ["11", "12"]),
        };

        // 02-25: primitive merchant account fields reserved by EMVCo for card networks (Visa, Mastercard, ...).
        for (var id = 2; id <= 25; id++)
        {
            rows.Add(new(Id(id), "Card network merchant account", "İşyeri Hesap Bilgileri", SpecialAlphanumeric, 1, 99, Optional));
        }

        rows.Add(new("26", "BKM card payment account", "İşyeri Hesap Bilgileri (BKM)", SpecialAlphanumeric, 1, 99, Conditional, BkmAccount));
        for (var id = 27; id <= 29; id++)
        {
            rows.Add(new(Id(id), "Reserved for BKM", "BKM için ayrılmıştır", SpecialAlphanumeric, 1, 99, Optional, ReservedAccount));
        }

        rows.Add(new("30", "FAST merchant account", "İşyeri Hesap Bilgileri (FAST)", SpecialAlphanumeric, 1, 99, Conditional, FastAccount));
        rows.Add(new("31", "FAST refund", "İade Şablonu (FAST)", SpecialAlphanumeric, 1, 99, Optional, FastRefund));
        rows.Add(new("32", "Mobile payments account", "İşyeri Hesap Bilgileri (Mobil)", SpecialAlphanumeric, 1, 99, Conditional, MobileAccount));
        for (var id = 33; id <= 46; id++)
        {
            rows.Add(new(Id(id), "Reserved merchant account", "Ayrılmış İşyeri Hesap Bilgileri", SpecialAlphanumeric, 1, 99, Optional, ReservedAccount));
        }

        // 47-48 may hold a primitive or a template at the merchant's discretion, so they are kept raw.
        rows.Add(new("47", "Merchant free template", "İşyeri Serbest Şablonu", SpecialAlphanumeric, 1, 99, Optional));
        rows.Add(new("48", "Merchant free template", "İşyeri Serbest Şablonu", SpecialAlphanumeric, 1, 99, Optional));
        rows.Add(new("49", "Merchant code", "İşyeri Kodu", Numeric, 10, 10, Optional));
        rows.Add(new("50", "Location", "Konum", Numeric, 16, 34, Optional));
        rows.Add(new("51", "Karekod identity", "Karekod Kimlik Şablonu", SpecialAlphanumeric, 1, 99, Conditional, Identity));
        rows.Add(new("52", "Merchant category code", "İşyeri Kategori Kodu", Numeric, 4, 4, Mandatory));
        rows.Add(new("53", "Transaction currency", "Para Birimi", Numeric, 3, 3, Mandatory));
        rows.Add(new("54", "Transaction amount", "Tutar", Numeric, 12, 12, Conditional));
        rows.Add(new("55", "Tip or convenience indicator", "Bahşiş veya Diğer Tutar Göstergesi", Numeric, 2, 2, NotUsed));
        rows.Add(new("56", "Convenience fee (fixed)", "Bahşiş veya Diğer Tutar Değeri (Sabit)", Numeric, 1, 12, NotUsed));
        rows.Add(new("57", "Convenience fee (percentage)", "Bahşiş veya Diğer Tutar Değeri (Yüzde)", Numeric, 1, 5, NotUsed));
        rows.Add(new("58", "Country code", "Ülke Kodu", SpecialAlphanumeric, 2, 2, Mandatory, AllowedValues: ["TR"]));
        rows.Add(new("59", "Merchant name", "İşyeri Adı", SpecialAlphanumeric, 1, 25, Mandatory));
        rows.Add(new("60", "Merchant city", "İşyeri Şehri", SpecialAlphanumeric, 1, 15, Mandatory));
        rows.Add(new("61", "Postal code", "Posta Kodu", SpecialAlphanumeric, 1, 10, Optional));
        rows.Add(new("62", "Additional data", "Ek Veri Alanları Şablonu", KarekodDataType.Text, 1, 99, Optional, AdditionalData));
        rows.Add(new("64", "Merchant information - language", "İşyeri Lisan Bilgisi Şablonu", KarekodDataType.Text, 1, 99, NotUsed));
        for (var id = 65; id <= 79; id++)
        {
            rows.Add(new(Id(id), "Reserved by EMVCo", "EMVCo Ayrılmış Alanı", KarekodDataType.Text, 1, 99, NotUsed));
        }

        for (var id = 80; id <= 99; id++)
        {
            rows.Add(new(Id(id), "Unreserved template", "Serbest Şablon", KarekodDataType.Text, 1, 99, NotUsed));
        }

        // The CRC sorts last in table order, so builders write it last as the standard requires.
        rows.Add(new("63", "CRC", "CRC", SpecialAlphanumeric, 4, 4, Mandatory));
        return rows;
    }

    private static string Id(int id) => id.ToString("D2", CultureInfo.InvariantCulture);
}
