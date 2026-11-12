namespace TrKarekod.Tlv;

/// <summary>
/// Splits ID-length-value text into data objects. It never throws on bad input: problems become diagnostics and parsing
/// of the affected level stops at the first unreadable header, because a TLV stream cannot be resynchronised.
/// </summary>
public static class TlvParser
{
    // IDs are two digits, so the 100 possible strings are shared instead of allocated per object.
    private static readonly string[] Ids = [.. Enumerable.Range(0, 100).Select(n => n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))];

    /// <summary>Parses <paramref name="text"/> into data objects using <paramref name="layout"/> to find templates.</summary>
    public static IReadOnlyList<TlvNode> Parse(string text, ITlvLayout layout, ICollection<KarekodDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(diagnostics);
        return ParseLevel(text, 0, text.Length, string.Empty, layout, diagnostics);
    }

    private static List<TlvNode> ParseLevel(
        string text, int start, int end, string parentPath, ITlvLayout layout, ICollection<KarekodDiagnostic> diagnostics)
    {
        var nodes = new List<TlvNode>();
        var i = start;
        while (i < end)
        {
            if (end - i < 4)
            {
                diagnostics.Add(Error(DiagnosticCodes.Truncated, parentPath, i, $"{end - i} trailing character(s) cannot form an ID and a length."));
                break;
            }

            if (!IsDigits(text, i, 2) || !IsDigits(text, i + 2, 2))
            {
                diagnostics.Add(Error(DiagnosticCodes.MalformedHeader, parentPath, i, $"Expected a two-digit ID and length, found \"{text.Substring(i, 4)}\"."));
                break;
            }

            var id = Ids[((text[i] - '0') * 10) + (text[i + 1] - '0')];
            var length = ((text[i + 2] - '0') * 10) + (text[i + 3] - '0');
            var path = parentPath.Length == 0 ? id : $"{parentPath}.{id}";
            if (length == 0)
            {
                diagnostics.Add(Error(DiagnosticCodes.ZeroLength, path, i, "Length 00 is not allowed; omit the object instead."));
                break;
            }

            var valueStart = i + 4;
            var valueEnd = RuneText.Advance(text.AsSpan(0, end), valueStart, length);
            if (valueEnd < 0)
            {
                diagnostics.Add(Error(DiagnosticCodes.Truncated, path, i, $"Declared length {length} runs past the end of the data."));
                break;
            }

            // A linear scan beats a HashSet here: a level holds at most a few dozen objects.
            if (nodes.Exists(n => n.Id == id))
            {
                diagnostics.Add(Error(DiagnosticCodes.DuplicateId, path, i, $"ID {id} appears more than once at this level."));
            }

            var value = text[valueStart..valueEnd];
            var childLayout = layout.TemplateLayout(id);
            var children = childLayout is null ? null : ParseLevel(text, valueStart, valueEnd, path, childLayout, diagnostics);
            nodes.Add(new TlvNode(id, value, i, path, children));
            i = valueEnd;
        }

        return nodes;
    }

    private static bool IsDigits(string text, int start, int count)
    {
        for (var k = start; k < start + count; k++)
        {
            if (!char.IsAsciiDigit(text[k]))
            {
                return false;
            }
        }

        return true;
    }

    private static KarekodDiagnostic Error(string code, string path, int offset, string message) =>
        new(code, DiagnosticSeverity.Error, path, offset, message);
}
