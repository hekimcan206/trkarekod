namespace TrKarekod.Tlv;

/// <summary>One data object: a two-digit ID, its value and, for templates, the nested objects.</summary>
public sealed class TlvNode
{
    internal TlvNode(string id, string value, int offset, string path, IReadOnlyList<TlvNode>? children)
    {
        Id = id;
        Value = value;
        Offset = offset;
        Path = path;
        Children = children ?? [];
        IsTemplate = children is not null;
    }

    /// <summary>Two-digit ID, for example <c>"59"</c>.</summary>
    public string Id { get; }

    /// <summary>The raw value; for a template this is the encoded nested objects.</summary>
    public string Value { get; }

    /// <summary>Character offset of the ID within the whole payload.</summary>
    public int Offset { get; }

    /// <summary>Dotted path such as <c>62.08</c>.</summary>
    public string Path { get; }

    /// <summary>True when the value was parsed as nested data objects.</summary>
    public bool IsTemplate { get; }

    /// <summary>Nested objects of a template, in payload order; empty for primitive objects.</summary>
    public IReadOnlyList<TlvNode> Children { get; }

    /// <summary>The first nested object with the given ID, or null.</summary>
    public TlvNode? Child(string id)
    {
        foreach (var child in Children)
        {
            if (child.Id == id)
            {
                return child;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override string ToString() => IsTemplate ? $"{Path} [{Children.Count} objects]" : $"{Path} = {Value}";
}
