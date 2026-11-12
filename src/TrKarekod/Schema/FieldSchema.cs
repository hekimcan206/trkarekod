using TrKarekod.Tlv;

namespace TrKarekod.Schema;

/// <summary>
/// A data-organisation table (the root of a code or one template). Rows are kept in table order, which is also the
/// order builders emit them in: the official samples follow table order, so a builder that does the same reproduces
/// them byte for byte.
/// </summary>
public sealed class FieldSchema : ITlvLayout
{
    private readonly Dictionary<string, FieldSpec> _byId;

    /// <summary>Creates a table.</summary>
    /// <param name="name">Human-readable name used in diagnostics.</param>
    /// <param name="fields">Rows in table order.</param>
    /// <param name="unknownFields">How to treat IDs the table does not define.</param>
    public FieldSchema(string name, IEnumerable<FieldSpec> fields, UnknownFieldHandling unknownFields = UnknownFieldHandling.Warn)
    {
        Name = name;
        Fields = [.. fields];
        _byId = Fields.ToDictionary(f => f.Id, StringComparer.Ordinal);
        UnknownFields = unknownFields;
    }

    /// <summary>Human-readable name.</summary>
    public string Name { get; }

    /// <summary>Rows in table order.</summary>
    public IReadOnlyList<FieldSpec> Fields { get; }

    /// <summary>How IDs the table does not define are reported.</summary>
    public UnknownFieldHandling UnknownFields { get; }

    /// <summary>The row with this ID, or null.</summary>
    public FieldSpec? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Position of the ID in table order (unknown IDs sort last).</summary>
    public int OrderOf(string id)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            if (Fields[i].Id == id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <inheritdoc />
    public ITlvLayout? TemplateLayout(string id) => Find(id)?.Template;
}

/// <summary>What to do with an ID a table does not define.</summary>
public enum UnknownFieldHandling
{
    /// <summary>Report a warning (TRK035).</summary>
    Warn,

    /// <summary>Accept silently: used where the guide leaves the content to another scheme.</summary>
    Accept,
}
