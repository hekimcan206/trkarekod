using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrKarekod.Building;
using TrKarekod.Model;
using TrKarekod.Rendering;
using TrKarekod.Schema;
using TrKarekod.Tlv;

namespace TrKarekod.Cli;

/// <summary>The trkarekod command line. Exit codes: 0 success/valid, 1 invalid payload or failed check, 2 usage error.</summary>
internal static class CliApp
{
    private const string Usage = """
        trkarekod - parse, validate, build and render TR Karekod (Turkish payment QR) payloads

        usage:
          trkarekod parse [--json] <payload | ->           validate and explain a payload (- reads stdin)
          trkarekod render <payload> -o <file.png> [--scale N]
          trkarekod build fast --iban IBAN --name NAME --city CITY --producer CODE --reference REF --purpose CODE
                               [--mcc MCC] [--amount 150.50 --expires-minutes 10] [--postal CODE]
                               [--hash HASH | --key-hex HEX] [--allow-warnings] [--png file.png]
          trkarekod build p2p  --iban IBAN --name NAME --producer CODE [--amount 10] [--reference REF] [--png file.png]
          trkarekod vectors                                 check the bundled published test vectors
          trkarekod codes                                   list diagnostic codes
        """;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
    };

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            return args switch
            {
                ["parse", .. var rest] => Parse(rest, stdin, stdout),
                ["render", .. var rest] => Render(rest, stdout),
                ["build", "fast", .. var rest] => BuildFast(rest, stdout),
                ["build", "p2p", .. var rest] => BuildPersonToPerson(rest, stdout),
                ["vectors"] => Vectors(stdout),
                ["codes"] => Codes(stdout),
                ["--version"] => Print(stdout, typeof(Karekod).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"),
                _ => UsageError(stderr, null),
            };
        }
        catch (UsageException ex)
        {
            return UsageError(stderr, ex.Message);
        }
        catch (KarekodBuildException ex)
        {
            stderr.WriteLine("cannot build the code:");
            foreach (var d in ex.Diagnostics)
            {
                stderr.WriteLine($"  {d}");
            }

            if (ex.Diagnostics.Count == 0)
            {
                stderr.WriteLine($"  {ex.Message}");
            }

            return 1;
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Parse(string[] args, TextReader stdin, TextWriter stdout)
    {
        var options = Options.Read(args, flags: ["--json"]);
        var payload = options.Positional.Count == 1 ? options.Positional[0] : throw new UsageException("parse needs exactly one payload");
        if (payload == "-")
        {
            payload = stdin.ReadToEnd().TrimEnd('\r', '\n');
        }

        var result = Karekod.Parse(payload);
        if (options.Has("--json"))
        {
            stdout.WriteLine(ToJson(result).ToJsonString(Json));
        }
        else
        {
            WriteReport(result, stdout);
        }

        return result.IsValid ? 0 : 1;
    }

    private static int Render(string[] args, TextWriter stdout)
    {
        var options = Options.Read(args, flags: []);
        var payload = options.Positional.Count == 1 ? options.Positional[0] : throw new UsageException("render needs exactly one payload");
        var file = options.Required("-o");
        var scale = options.Int("--scale") ?? 10;
        var png = KarekodQr.RenderPng(payload, scale);
        File.WriteAllBytes(file, png);
        stdout.WriteLine($"wrote {file}: QR version {KarekodQr.Version(payload)}, {png.Length} bytes");
        return 0;
    }

    private static int BuildFast(string[] args, TextWriter stdout)
    {
        var o = Options.Read(args, flags: ["--allow-warnings"]);
        var now = KarekodTimestamp.TurkeyNow();
        var builder = new FastMerchantKarekodBuilder()
            .Iban(o.Required("--iban"))
            .Merchant(o.Required("--name"), o.Required("--city"), o.Value("--mcc") ?? "0000")
            .Producer(o.Required("--producer"), o.Required("--reference"), now)
            .PaymentPurpose(o.Required("--purpose"));

        if (o.Decimal("--amount") is { } amount)
        {
            builder = o.Int("--expires-minutes") is { } minutes
                ? builder.Dynamic(amount, now.AddMinutes(minutes))
                : builder.Amount(amount);
        }

        if (o.Value("--postal") is { } postal)
        {
            builder.PostalCode(postal);
        }

        if (o.Value("--key-hex") is { } keyHex)
        {
            builder.SignWith(Convert.FromHexString(keyHex));
        }
        else
        {
            // Without a key the 32-character value is random: it still fills the mandatory field, but nobody can verify it.
            builder.Hash(o.Value("--hash") ?? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)));
        }

        return Emit(builder.Build(o.Has("--allow-warnings")), o, stdout);
    }

    private static int BuildPersonToPerson(string[] args, TextWriter stdout)
    {
        var o = Options.Read(args, flags: ["--allow-warnings"]);
        var builder = new PersonToPersonKarekodBuilder()
            .Producer(o.Required("--producer"))
            .Recipient(o.Required("--iban"), o.Required("--name"))
            .Validity(KarekodTimestamp.TurkeyNow());
        if (o.Value("--reference") is { } reference)
        {
            builder.Dynamic(reference);
        }

        if (o.Decimal("--amount") is { } amount)
        {
            builder.Amount(amount);
        }

        return Emit(builder.Build(o.Has("--allow-warnings")), o, stdout);
    }

    private static int Emit(string payload, Options options, TextWriter stdout)
    {
        stdout.WriteLine(payload);
        if (options.Value("--png") is { } file)
        {
            File.WriteAllBytes(file, KarekodQr.RenderPng(payload));
        }

        return 0;
    }

    private static int Vectors(TextWriter stdout)
    {
        using var stream = typeof(CliApp).Assembly.GetManifestResourceStream("trkarekod-vectors.json")
            ?? throw new InvalidOperationException("Vector file is not embedded.");
        var document = JsonNode.Parse(stream)!;
        int passed = 0, failed = 0;
        foreach (var vector in document["vectors"]!.AsArray())
        {
            var name = vector!["name"]!.GetValue<string>();
            var result = Karekod.Parse(vector["payload"]!.GetValue<string>());
            var expectedErrors = vector["errors"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();
            var actualErrors = result.Errors.Select(e => $"{e.Code} {e.Path}".TrimEnd()).ToList();
            var ok = result.IsValid == vector["valid"]!.GetValue<bool>() && expectedErrors.SequenceEqual(actualErrors);
            stdout.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name,-32} {(result.IsValid ? "valid" : string.Join(", ", actualErrors))}");
            _ = ok ? passed++ : failed++;
        }

        stdout.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static int Codes(TextWriter stdout)
    {
        foreach (var (code, description) in DiagnosticCodes.All)
        {
            stdout.WriteLine($"{code}  {description}");
        }

        return 0;
    }

    private static void WriteReport(KarekodParseResult result, TextWriter stdout)
    {
        var crc = result.CrcValid ? "CRC ok" : "CRC not verified";
        stdout.WriteLine($"{result.Kind}: {(result.IsValid ? "VALID" : "INVALID")} ({crc})");
        if (result.ShortCode is { } shortCode)
        {
            stdout.WriteLine($"  scheme {shortCode.Scheme}, producer {shortCode.ProducerCode}, reference {shortCode.Reference}, hash {shortCode.Hash}");
        }

        WriteNodes(result.Kind, result.Nodes, stdout, indent: 2);
        foreach (var d in result.Diagnostics)
        {
            stdout.WriteLine(d.ToString());
        }
    }

    private static void WriteNodes(KarekodKind kind, IReadOnlyList<TlvNode> nodes, TextWriter stdout, int indent)
    {
        foreach (var node in nodes)
        {
            var spec = KarekodSchemas.FieldAt(kind, node.Path);
            var label = $"{new string(' ', indent)}{node.Path,-6} {spec?.Name ?? "(not defined)"}";
            stdout.WriteLine(node.IsTemplate ? label : $"{label.PadRight(48, '.')} {node.Value}");
            WriteNodes(kind, node.Children, stdout, indent + 2);
        }
    }

    private static JsonObject ToJson(KarekodParseResult result)
    {
        var fields = new JsonObject();
        void Flatten(IReadOnlyList<TlvNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsTemplate)
                {
                    Flatten(node.Children);
                }
                else
                {
                    fields[node.Path] = node.Value;
                }
            }
        }

        Flatten(result.Nodes);
        return new JsonObject
        {
            ["kind"] = result.Kind.ToString(),
            ["valid"] = result.IsValid,
            ["crcValid"] = result.CrcValid,
            ["fields"] = fields,
            ["diagnostics"] = new JsonArray([.. result.Diagnostics.Select(d => (JsonNode)new JsonObject
            {
                ["code"] = d.Code,
                ["severity"] = d.Severity.ToString().ToLowerInvariant(),
                ["path"] = d.Path,
                ["offset"] = d.Offset,
                ["message"] = d.Message,
            })]),
        };
    }

    private static int Print(TextWriter stdout, string text)
    {
        stdout.WriteLine(text);
        return 0;
    }

    private static int UsageError(TextWriter stderr, string? message)
    {
        if (message is not null)
        {
            stderr.WriteLine($"error: {message}");
        }

        stderr.WriteLine(Usage);
        return 2;
    }

    private sealed class UsageException(string message) : Exception(message);

    /// <summary>Minimal "--name value" option parsing; the tool has too few options to justify a dependency.</summary>
    private sealed class Options
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

        public List<string> Positional { get; } = [];

        public static Options Read(string[] args, string[] flags)
        {
            var options = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (flags.Contains(arg))
                {
                    options._flags.Add(arg);
                }
                else if (arg.StartsWith('-') && arg != "-")
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new UsageException($"{arg} needs a value");
                    }

                    options._values[arg] = args[++i];
                }
                else
                {
                    options.Positional.Add(arg);
                }
            }

            return options;
        }

        public bool Has(string flag) => _flags.Contains(flag);

        public string? Value(string name) => _values.GetValueOrDefault(name);

        public string Required(string name) => Value(name) ?? throw new UsageException($"{name} is required");

        public int? Int(string name) => Value(name) is { } v
            ? int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : throw new UsageException($"{name} must be an integer")
            : null;

        public decimal? Decimal(string name) => Value(name) is { } v
            ? decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : throw new UsageException($"{name} must be a number like 150.50")
            : null;
    }
}
