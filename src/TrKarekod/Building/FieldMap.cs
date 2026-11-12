using TrKarekod.Schema;
using TrKarekod.Tlv;

namespace TrKarekod.Building;

/// <summary>Values for one level of a code, emitted in the table order of its schema.</summary>
internal sealed class FieldMap(FieldSchema schema)
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FieldMap> _templates = new(StringComparer.Ordinal);

    public FieldSchema Schema { get; } = schema;

    public string? this[string id]
    {
        get => _values.GetValueOrDefault(id);
        set
        {
            if (value is null)
            {
                _values.Remove(id);
            }
            else
            {
                _values[id] = value;
            }
        }
    }

    public FieldMap Template(string id)
    {
        if (!_templates.TryGetValue(id, out var map))
        {
            var spec = Schema.Find(id) ?? throw new ArgumentException($"{id} is not defined in {Schema.Name}.", nameof(id));
            map = new FieldMap(spec.Template ?? throw new ArgumentException($"{id} is not a template.", nameof(id)));
            _templates[id] = map;
        }

        return map;
    }

    public void RemoveTemplate(string id) => _templates.Remove(id);

    public void WriteTo(TlvWriter writer)
    {
        var ids = _values.Keys.Concat(_templates.Keys).Distinct(StringComparer.Ordinal).OrderBy(Schema.OrderOf).ThenBy(id => id, StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (_templates.TryGetValue(id, out var template))
            {
                writer.WriteTemplate(id, template.WriteTo);
            }
            else
            {
                writer.Write(id, _values[id]);
            }
        }
    }

    /// <summary>
    /// Serialises the code and validates it with the same parser payers use: a builder can never emit a payload its own
    /// parser would reject.
    /// </summary>
    public string Build(bool allowWarnings)
    {
        var writer = new TlvWriter();
        try
        {
            WriteTo(writer);
        }
        catch (ArgumentException ex)
        {
            throw new KarekodBuildException(ex.Message, ex);
        }

        var payload = writer.WriteCrcAndFinish();
        var result = Karekod.Parse(payload);
        var blocking = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || !allowWarnings).ToList();
        if (blocking.Count > 0)
        {
            throw new KarekodBuildException(blocking);
        }

        return payload;
    }
}
