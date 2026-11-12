namespace TrKarekod;

/// <summary>How bad a finding is. Only errors make a payload invalid.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The payload is usable but something is suspicious (for example a placeholder IBAN).</summary>
    Warning,

    /// <summary>The payload violates the standard; a payer application must not use it.</summary>
    Error,
}

/// <summary>A single finding about a payload.</summary>
/// <param name="Code">Stable identifier from <see cref="DiagnosticCodes"/>, safe to match on.</param>
/// <param name="Path">Dotted field path such as <c>62.08</c>; empty for the payload as a whole.</param>
/// <param name="Offset">Character offset of the offending field in the payload, or -1.</param>
public sealed record KarekodDiagnostic(string Code, DiagnosticSeverity Severity, string Path, int Offset, string Message)
{
    /// <inheritdoc />
    public override string ToString() =>
        $"{(Severity == DiagnosticSeverity.Error ? "error" : "warning")} {Code}{(Path.Length > 0 ? $" [{Path}]" : string.Empty)}: {Message}";
}
