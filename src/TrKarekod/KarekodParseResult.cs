using TrKarekod.Model;
using TrKarekod.Tlv;

namespace TrKarekod;

/// <summary>Which TR Karekod usage model a payload follows, decided by its first characters.</summary>
public enum KarekodKind
{
    /// <summary>Not recognised as any TR Karekod model.</summary>
    Unknown,

    /// <summary>Merchant-presented long code, EMVCo layout (format indicator ID 00 = "01"): FAST, BKM or mobile.</summary>
    MerchantPresented,

    /// <summary>FAST person-to-person code (format indicator ID 75).</summary>
    PersonToPerson,

    /// <summary>Consumer-presented code (format indicator ID 85); structure and CRC only.</summary>
    ConsumerPresented,

    /// <summary>Fixed-width short code ("kısa karekod") with indicator 97 (FAST), 99 (BKM) or 96 (either).</summary>
    ShortCode,
}

/// <summary>Everything learned from one payload. Parsing never throws for bad input; check <see cref="IsValid"/>.</summary>
public sealed class KarekodParseResult
{
    internal KarekodParseResult(string payload, KarekodKind kind, IReadOnlyList<TlvNode> nodes, IReadOnlyList<KarekodDiagnostic> diagnostics, bool crcValid)
    {
        Payload = payload;
        Kind = kind;
        Nodes = nodes;
        Diagnostics = diagnostics;
        CrcValid = crcValid;
    }

    /// <summary>The input as given.</summary>
    public string Payload { get; }

    /// <summary>Detected usage model.</summary>
    public KarekodKind Kind { get; }

    /// <summary>Root data objects (empty for short codes and unreadable input).</summary>
    public IReadOnlyList<TlvNode> Nodes { get; }

    /// <summary>All findings, errors first.</summary>
    public IReadOnlyList<KarekodDiagnostic> Diagnostics { get; }

    /// <summary>True when the CRC was present and matched. A valid CRC proves integrity in transit, not authenticity.</summary>
    public bool CrcValid { get; }

    /// <summary>The typed merchant-presented code; set only when the payload is valid and of that kind.</summary>
    public MerchantKarekod? Merchant { get; internal init; }

    /// <summary>The typed person-to-person code; set only when the payload is valid and of that kind.</summary>
    public PersonToPersonKarekod? PersonToPerson { get; internal init; }

    /// <summary>The typed short code; set only when the payload is valid and of that kind.</summary>
    public ShortKarekod? ShortCode { get; internal init; }

    /// <summary>True when there is no error-level diagnostic.</summary>
    public bool IsValid => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>Errors only.</summary>
    public IEnumerable<KarekodDiagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>Warnings only.</summary>
    public IEnumerable<KarekodDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>Finds a data object by dotted path, for example <c>"62.08"</c>; null when absent.</summary>
    public TlvNode? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        TlvNode? current = null;
        var level = Nodes;
        foreach (var id in path.Split('.'))
        {
            current = null;
            foreach (var node in level)
            {
                if (node.Id == id)
                {
                    current = node;
                    break;
                }
            }

            if (current is null)
            {
                return null;
            }

            level = current.Children;
        }

        return current;
    }

    /// <summary>Value of the data object at <paramref name="path"/>, or null.</summary>
    public string? Value(string path) => Find(path)?.Value;
}
