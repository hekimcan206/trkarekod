namespace TrKarekod.Schema;

/// <summary>Value data types of the TR Karekod guides.</summary>
public enum KarekodDataType
{
    /// <summary>"N" (Nümerik): ASCII digits only.</summary>
    Numeric,

    /// <summary>
    /// "ÖAN" (Özel Alfa Nümerik): digits, letters and punctuation. The official samples put Turkish letters
    /// ("İSTANBUL") in ÖAN fields, so letters are any Unicode letters; control characters are rejected.
    /// </summary>
    SpecialAlphanumeric,

    /// <summary>"K" (Karakter Dizgisi): any UTF-8 text except control characters.</summary>
    Text,
}

/// <summary>Presence column of the guides' tables.</summary>
public enum Presence
{
    /// <summary>"Z" (Zorunlu): always present.</summary>
    Mandatory,

    /// <summary>"İ" (İsteğe bağlı): may be present.</summary>
    Optional,

    /// <summary>"K" (Koşullu): required under a condition checked by a business rule.</summary>
    Conditional,

    /// <summary>The scheme reserves the ID but does not use it ("FAST için kullanılmayacaktır").</summary>
    NotUsed,
}

/// <summary>One row of a data-organisation table.</summary>
/// <param name="Id">Two-digit ID.</param>
/// <param name="Name">English name.</param>
/// <param name="TurkishName">Name as written in the TCMB/BKM guide.</param>
/// <param name="Template">Layout of the nested objects when this row is a template.</param>
/// <param name="AllowedValues">Closed value set, or null when any value of the right type and length is allowed.</param>
public sealed record FieldSpec(
    string Id,
    string Name,
    string TurkishName,
    KarekodDataType Type,
    int MinLength,
    int MaxLength,
    Presence Presence,
    FieldSchema? Template = null,
    IReadOnlyList<string>? AllowedValues = null);
