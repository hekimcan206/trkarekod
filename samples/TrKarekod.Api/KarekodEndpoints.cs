using System.Diagnostics.Metrics;
using TrKarekod.Building;
using TrKarekod.Model;
using TrKarekod.Rendering;

namespace TrKarekod.Api;

/// <summary>Producer configuration: the code issued by the registry and the secret behind 30.20.</summary>
public sealed record KarekodSettings(string ProducerCode, byte[] ProducerKey)
{
    public static KarekodSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection("Karekod");
        var keyHex = section["ProducerKeyHex"];
        if (string.IsNullOrWhiteSpace(keyHex) || keyHex.Length < 32)
        {
            throw new InvalidOperationException("Karekod:ProducerKeyHex must be set to at least 16 random bytes in hex (see .env.example).");
        }

        return new KarekodSettings(section["ProducerCode"] ?? "0010", Convert.FromHexString(keyHex));
    }
}

public sealed record ParseRequest(string Payload);

public sealed record RenderRequest(string Payload, int? Scale);

/// <summary>A FAST merchant code request from a point of sale.</summary>
/// <param name="ExpiresInMinutes">Set together with <see cref="Amount"/> for a dynamic (single transaction) code.</param>
public sealed record FastCodeRequest(
    string Iban,
    string MerchantName,
    string City,
    string Reference,
    string PaymentPurpose,
    string? MerchantCategoryCode,
    decimal? Amount,
    int? ExpiresInMinutes,
    string? PostalCode);

public sealed record FastCodeResponse(string Payload, int QrVersion, DateTime? ExpiresAt);

public sealed record VerifyResponse(bool Valid, bool ProducerHashValid, bool Expired, IReadOnlyList<KarekodDiagnostic> Diagnostics);

internal static class KarekodEndpoints
{
    public const string MeterName = "TrKarekod.Api";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Issued = Meter.CreateCounter<long>("trkarekod.codes.issued", description: "FAST codes issued");
    private static readonly Counter<long> Parsed = Meter.CreateCounter<long>("trkarekod.codes.parsed", description: "Payloads parsed, by validity");

    public static void MapKarekodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/karekod");

        group.MapPost("/parse", (ParseRequest request) =>
        {
            var result = Karekod.Parse(request.Payload ?? string.Empty);
            Parsed.Add(1, new KeyValuePair<string, object?>("valid", result.IsValid));
            return Results.Ok(new
            {
                kind = result.Kind,
                valid = result.IsValid,
                crcValid = result.CrcValid,
                diagnostics = result.Diagnostics,
                merchant = result.Merchant,
                personToPerson = result.PersonToPerson,
                shortCode = result.ShortCode,
            });
        }).WithSummary("Parse and validate any TR Karekod payload");

        group.MapPost("/fast", (FastCodeRequest request, KarekodSettings settings, TimeProvider clock) =>
        {
            try
            {
                return IssueFastCode(request, settings, clock);
            }
            catch (KarekodBuildException ex)
            {
                return Results.ValidationProblem(
                    ex.Diagnostics.GroupBy(d => d.Path.Length == 0 ? "payload" : d.Path).ToDictionary(g => g.Key, g => g.Select(d => $"{d.Code}: {d.Message}").ToArray()),
                    title: "The code would not be valid");
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] });
            }
        }).WithSummary("Issue a signed static or dynamic FAST merchant code");

        group.MapPost("/verify", (ParseRequest request, KarekodSettings settings, TimeProvider clock) =>
        {
            // What the merchant's bank does when the payment message echoes the code back: integrity (CRC and rules),
            // authenticity (30.20 under our key) and freshness (51.07).
            var result = Karekod.Parse(request.Payload ?? string.Empty);
            var merchant = result.Merchant;
            var hashValid = merchant is not null && ProducerHash.Verify(settings.ProducerKey, merchant);
            var expired = merchant?.Identity?.ExpiresAt is { } e && e < KarekodTimestamp.TurkeyNow(clock);
            return Results.Ok(new VerifyResponse(result.IsValid, hashValid, expired, result.Diagnostics));
        }).WithSummary("Check integrity, producer hash and expiry of a code this producer issued");

        group.MapPost("/render", (RenderRequest request) =>
        {
            var result = Karekod.Parse(request.Payload ?? string.Empty);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = [.. result.Errors.Select(e => e.ToString())] });
            }

            return Results.File(KarekodQr.RenderPng(request.Payload!, Math.Clamp(request.Scale ?? 8, 1, 20)), "image/png");
        }).WithSummary("Render a valid payload as a PNG QR symbol");
    }

    private static IResult IssueFastCode(FastCodeRequest request, KarekodSettings settings, TimeProvider clock)
    {
        var now = KarekodTimestamp.TurkeyNow(clock);
        var builder = new FastMerchantKarekodBuilder()
            .Iban(request.Iban)
            .Merchant(request.MerchantName, request.City, request.MerchantCategoryCode ?? "0000")
            .Producer(settings.ProducerCode, request.Reference, now)
            .Terminal(TerminalType.Web)
            .PaymentPurpose(request.PaymentPurpose)
            .SignWith(settings.ProducerKey);

        DateTime? expiresAt = null;
        if (request.Amount is { } amount)
        {
            if (request.ExpiresInMinutes is { } minutes)
            {
                expiresAt = now.AddMinutes(minutes);
                builder.Dynamic(amount, expiresAt.Value);
            }
            else
            {
                builder.Amount(amount);
            }
        }

        if (request.PostalCode is { } postal)
        {
            builder.PostalCode(postal);
        }

        var payload = builder.Build();
        Issued.Add(1, new KeyValuePair<string, object?>("dynamic", expiresAt is not null));
        return Results.Ok(new FastCodeResponse(payload, KarekodQr.Version(payload), expiresAt));
    }
}
