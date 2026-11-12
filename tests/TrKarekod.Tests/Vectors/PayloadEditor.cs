using TrKarekod.Tlv;

namespace TrKarekod.Tests.Vectors;

/// <summary>
/// Edits a valid payload field by field and re-signs it, so a negative vector breaks exactly one rule instead of also
/// breaking the CRC.
/// </summary>
public sealed class PayloadEditor
{
    private readonly List<Node> _root;

    private PayloadEditor(List<Node> root) => _root = root;

    public static string Edit(string payload, Action<PayloadEditor> edit)
    {
        var result = Karekod.Parse(payload);
        if (result.Nodes.Count == 0)
        {
            throw new ArgumentException("Only TLV payloads can be edited.", nameof(payload));
        }

        var editor = new PayloadEditor([.. result.Nodes.Where(n => n.Id != "63").Select(Node.From)]);
        edit(editor);
        return editor.Serialize();
    }

    public PayloadEditor Set(string path, string value)
    {
        var (level, id) = Locate(path);
        var index = level.FindIndex(n => n.Id == id);
        if (index >= 0)
        {
            level[index] = new Node(id, value, null);
        }
        else
        {
            level.Add(new Node(id, value, null));
        }

        return this;
    }

    public PayloadEditor Remove(string path)
    {
        var (level, id) = Locate(path);
        level.RemoveAll(n => n.Id == id);
        return this;
    }

    /// <summary>Appends a raw object at the end of the root, allowing duplicates.</summary>
    public PayloadEditor AppendRaw(string id, string value)
    {
        _root.Add(new Node(id, value, null));
        return this;
    }

    private (List<Node> Level, string Id) Locate(string path)
    {
        var parts = path.Split('.');
        var level = _root;
        foreach (var part in parts[..^1])
        {
            level = level.Single(n => n.Id == part).Children ?? throw new ArgumentException($"{part} is not a template.", nameof(path));
        }

        return (level, parts[^1]);
    }

    private string Serialize()
    {
        var writer = new TlvWriter();
        Write(writer, _root);
        return writer.WriteCrcAndFinish();
    }

    private static void Write(TlvWriter writer, List<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children is { } children)
            {
                writer.WriteTemplate(node.Id, inner => Write(inner, children));
            }
            else
            {
                writer.Write(node.Id, node.Value);
            }
        }
    }

    private sealed record Node(string Id, string Value, List<Node>? Children)
    {
        public static Node From(TlvNode n) => new(n.Id, n.Value, n.IsTemplate ? [.. n.Children.Select(From)] : null);
    }
}
