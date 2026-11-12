using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using TrKarekod.Tests.Vectors;
using TrKarekod.Tlv;

namespace TrKarekod.Tests;

/// <summary>
/// Golden test for the published vector file: the committed vectors/trkarekod-vectors.json must equal what the parser
/// produces today, so any behaviour change shows up as a reviewable diff. Set TRKAREKOD_UPDATE_GOLDEN=1 to rewrite it.
/// </summary>
public sealed class VectorFileTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_vector_breaks_exactly_the_rule_it_was_designed_for(string name)
    {
        var vector = VectorCatalog.All.Single(v => v.Name == name);
        var result = Karekod.Parse(vector.Payload);

        if (vector.PrimaryCode is null)
        {
            result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
        }
        else
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Select(e => e.Code).Should().Contain(vector.PrimaryCode);
        }
    }

    [Fact]
    public void Negative_vectors_keep_a_valid_crc_unless_the_crc_is_the_point()
    {
        var crcCases = new[] { DiagnosticCodes.CrcMismatch, DiagnosticCodes.CrcMissing, DiagnosticCodes.ShortCodeLayout, DiagnosticCodes.UnknownFormat, DiagnosticCodes.PayloadTooLong };
        foreach (var vector in VectorCatalog.All.Where(v => v.PrimaryCode is not null && !crcCases.Contains(v.PrimaryCode)))
        {
            Karekod.Parse(vector.Payload).CrcValid.Should().BeTrue(vector.Name);
        }
    }

    [Fact]
    public void Published_vector_file_matches_the_parser()
    {
        var path = Path.Combine(RepositoryRoot(), "vectors", "trkarekod-vectors.json");
        var actual = Render();

        if (Environment.GetEnvironmentVariable("TRKAREKOD_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        File.Exists(path).Should().BeTrue($"{path} is missing; run the tests with TRKAREKOD_UPDATE_GOLDEN=1 to create it");
        File.ReadAllText(path).ReplaceLineEndings("\n").Should().Be(actual);
    }

    public static TheoryData<string> Cases() => [.. VectorCatalog.All.Select(v => v.Name)];

    private static string Render()
    {
        var vectors = new JsonArray();
        foreach (var vector in VectorCatalog.All)
        {
            var result = Karekod.Parse(vector.Payload);
            var entry = new JsonObject
            {
                ["name"] = vector.Name,
                ["source"] = vector.Source,
                ["description"] = vector.Description,
                ["payload"] = vector.Payload,
                ["kind"] = result.Kind.ToString(),
                ["valid"] = result.IsValid,
                ["crcValid"] = result.CrcValid,
                ["errors"] = new JsonArray([.. result.Errors.Select(e => (JsonNode)$"{e.Code} {e.Path}".TrimEnd())]),
                ["warnings"] = new JsonArray([.. result.Warnings.Select(e => (JsonNode)$"{e.Code} {e.Path}".TrimEnd())]),
            };
            if (result.IsValid && result.Nodes.Count > 0)
            {
                var fields = new JsonObject();
                Flatten(result.Nodes, fields);
                entry["fields"] = fields;
            }

            vectors.Add(entry);
        }

        var document = new JsonObject
        {
            ["description"] = "TR Karekod test vectors: official samples from the TCMB and BKM guides and negative vectors derived from them. "
                + "Error and warning codes are documented in the README. Generated and verified by tests/TrKarekod.Tests/VectorFileTests.cs.",
            ["crc"] = "CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) over the UTF-8 bytes up to and including '6304'; short codes: over the first 50 characters.",
            ["vectors"] = vectors,
        };
        return document.ToJsonString(Json).ReplaceLineEndings("\n") + "\n";
    }

    private static void Flatten(IReadOnlyList<TlvNode> nodes, JsonObject fields)
    {
        foreach (var node in nodes)
        {
            if (node.IsTemplate)
            {
                Flatten(node.Children, fields);
            }
            else
            {
                fields[node.Path] = node.Value;
            }
        }
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TrKarekod.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (TrKarekod.slnx) not found.");
    }
}
