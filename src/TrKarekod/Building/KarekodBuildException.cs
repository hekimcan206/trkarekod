namespace TrKarekod.Building;

/// <summary>Thrown when a builder's input would produce a code that does not validate.</summary>
public sealed class KarekodBuildException : Exception
{
    /// <summary>Creates the exception from the findings that blocked the build.</summary>
    public KarekodBuildException(IReadOnlyList<KarekodDiagnostic> diagnostics)
        : base("The code would not be valid: " + string.Join("; ", diagnostics))
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Creates the exception with a message.</summary>
    public KarekodBuildException(string message)
        : base(message)
    {
        Diagnostics = [];
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public KarekodBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
        Diagnostics = [];
    }

    /// <summary>Creates an empty exception.</summary>
    public KarekodBuildException()
        : this("The code would not be valid.")
    {
    }

    /// <summary>The findings that blocked the build.</summary>
    public IReadOnlyList<KarekodDiagnostic> Diagnostics { get; }
}
