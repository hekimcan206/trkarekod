using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using TrKarekod.Tests.Vectors;

namespace TrKarekod.Tests;

/// <summary>The payload comes from a camera pointed at an untrusted sticker: no input may crash the parser.</summary>
public sealed class RobustnessTests
{
    [Property(MaxTest = 2000)]
    public bool Arbitrary_strings_never_throw(string? input)
    {
        var result = Karekod.Parse(input ?? string.Empty);
        return result.IsValid || result.Errors.Any();
    }

    [Property(MaxTest = 2000)]
    public Property Mutated_official_vectors_never_throw()
    {
        var vectors = OfficialVectors.All.Select(row => (string)row.Data.Item2).ToArray();
        var mutations =
            from payload in Gen.Elements(vectors)
            from position in Gen.Choose(0, payload.Length)
            from kind in Gen.Choose(0, 3)
            from c in Gen.Elements("0123456789AZ çİ\u0000\uD800😀".ToCharArray())
            select Mutate(payload, position, kind, c);

        return Prop.ForAll(mutations.ToArbitrary(), payload =>
        {
            var result = Karekod.Parse(payload);
            return result.IsValid || result.Errors.Any();
        });
    }

    [Fact]
    public void Every_prefix_of_every_official_vector_is_handled()
    {
        foreach (var row in OfficialVectors.All)
        {
            var payload = (string)row.Data.Item2;
            for (var length = 0; length < payload.Length; length++)
            {
                var result = Karekod.Parse(payload[..length]);
                result.IsValid.Should().BeFalse($"a {length}-character prefix of {row.Data.Item1} is not a complete code");
            }
        }
    }

    [Fact]
    public void Deeply_nested_looking_input_is_bounded_by_the_length_cap()
    {
        // Templates nest one level in the tables, so recursion depth is bounded by the schema, not by the input.
        var payload = "0002016299" + new string('6', 89) + "6304ABCD";

        var result = Karekod.Parse(payload);

        result.IsValid.Should().BeFalse();
    }

    private static string Mutate(string payload, int position, int kind, char c)
    {
        var p = Math.Min(position, payload.Length);
        return kind switch
        {
            0 => payload.Insert(p, c.ToString()),
            1 when p < payload.Length => payload.Remove(p, 1),
            2 when p < payload.Length => string.Concat(payload.AsSpan(0, p), c.ToString(), payload.AsSpan(p + 1)),
            _ => payload[..p],
        };
    }
}
