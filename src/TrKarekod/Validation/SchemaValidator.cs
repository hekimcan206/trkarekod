using TrKarekod.Schema;
using TrKarekod.Tlv;

namespace TrKarekod.Validation;

/// <summary>
/// Checks parsed data objects against a data-organisation table: unknown and not-used IDs, mandatory rows, lengths,
/// data types and closed value sets. Conditional rows are left to the business rules.
/// </summary>
internal static class SchemaValidator
{
    public static void Validate(IReadOnlyList<TlvNode> nodes, FieldSchema schema, string parentPath, ICollection<KarekodDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            var spec = schema.Find(node.Id);
            if (spec is null)
            {
                if (schema.UnknownFields == UnknownFieldHandling.Warn)
                {
                    diagnostics.Add(new(DiagnosticCodes.UnknownField, DiagnosticSeverity.Warning, node.Path, node.Offset,
                        $"ID {node.Id} is not defined in {schema.Name}."));
                }

                continue;
            }

            if (spec.Presence == Presence.NotUsed)
            {
                diagnostics.Add(new(DiagnosticCodes.NotUsedInScheme, DiagnosticSeverity.Warning, node.Path, node.Offset,
                    $"{spec.Name} ({spec.TurkishName}) is reserved but not used by TR Karekod; payer apps will ignore it."));
            }

            if (spec.Template is not null)
            {
                Validate(node.Children, spec.Template, node.Path, diagnostics);
                continue;
            }

            CheckValue(node, spec, diagnostics);
        }

        foreach (var spec in schema.Fields)
        {
            if (spec.Presence == Presence.Mandatory && !Contains(nodes, spec.Id))
            {
                var path = parentPath.Length == 0 ? spec.Id : $"{parentPath}.{spec.Id}";
                diagnostics.Add(new(DiagnosticCodes.MissingField, DiagnosticSeverity.Error, path, -1,
                    $"{spec.Name} ({spec.TurkishName}) is mandatory."));
            }
        }
    }

    internal static void CheckValue(TlvNode node, FieldSpec spec, ICollection<KarekodDiagnostic> diagnostics)
    {
        var length = RuneText.Length(node.Value);
        if (length < spec.MinLength || length > spec.MaxLength)
        {
            var range = spec.MinLength == spec.MaxLength ? $"exactly {spec.MinLength}" : $"{spec.MinLength}-{spec.MaxLength}";
            diagnostics.Add(new(DiagnosticCodes.LengthOutOfRange, DiagnosticSeverity.Error, node.Path, node.Offset,
                $"{spec.Name} must be {range} characters, was {length}."));
        }

        if (!HasValidCharacters(node.Value, spec.Type))
        {
            diagnostics.Add(new(DiagnosticCodes.InvalidCharacters, DiagnosticSeverity.Error, node.Path, node.Offset,
                spec.Type == KarekodDataType.Numeric
                    ? $"{spec.Name} must contain digits only."
                    : $"{spec.Name} contains control characters."));
        }

        if (spec.AllowedValues is { } allowed && !allowed.Contains(node.Value, StringComparer.Ordinal))
        {
            diagnostics.Add(new(DiagnosticCodes.InvalidValue, DiagnosticSeverity.Error, node.Path, node.Offset,
                $"{spec.Name} must be one of {string.Join(", ", allowed)}; was \"{node.Value}\"."));
        }
    }

    internal static bool HasValidCharacters(string value, KarekodDataType type)
    {
        foreach (var c in value)
        {
            var ok = type == KarekodDataType.Numeric ? char.IsAsciiDigit(c) : !char.IsControl(c);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(IReadOnlyList<TlvNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
