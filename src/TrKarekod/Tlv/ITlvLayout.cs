namespace TrKarekod.Tlv;

/// <summary>Tells the TLV parser which IDs at a level are templates and how their content is laid out.</summary>
public interface ITlvLayout
{
    /// <summary>The layout of the template with this ID, or null when the ID is a primitive data object.</summary>
    ITlvLayout? TemplateLayout(string id);
}

/// <summary>A layout in which every data object is primitive.</summary>
public sealed class FlatTlvLayout : ITlvLayout
{
    private FlatTlvLayout()
    {
    }

    /// <summary>The shared instance.</summary>
    public static FlatTlvLayout Instance { get; } = new();

    /// <inheritdoc />
    public ITlvLayout? TemplateLayout(string id) => null;
}
