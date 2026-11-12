using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TrKarekod.Tests.Vectors;

namespace TrKarekod.Tests;

public sealed class ApiTests(ApiTests.Factory factory) : IClassFixture<ApiTests.Factory>
{
    private const string ValidIban = "TR330006100519786457841326";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Issues_a_dynamic_code_that_verifies()
    {
        var issued = await PostJson("/v1/karekod/fast", new
        {
            iban = ValidIban,
            merchantName = "Kafe 42",
            city = "ANKARA",
            reference = "POS1-000123",
            paymentPurpose = "01",
            merchantCategoryCode = "5814",
            amount = 87.50m,
            expiresInMinutes = 5,
        });
        var payload = issued["payload"]!.GetValue<string>();

        var verified = await PostJson("/v1/karekod/verify", new { payload });

        verified["valid"]!.GetValue<bool>().Should().BeTrue();
        verified["producerHashValid"]!.GetValue<bool>().Should().BeTrue();
        verified["expired"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task A_code_from_another_producer_fails_hash_verification()
    {
        var verified = await PostJson("/v1/karekod/verify", new { payload = OfficialVectors.FastSale });

        verified["valid"]!.GetValue<bool>().Should().BeTrue();
        verified["producerHashValid"]!.GetValue<bool>().Should().BeFalse();
        verified["expired"]!.GetValue<bool>().Should().BeTrue("the sample expired in July 2020");
    }

    [Fact]
    public async Task Parse_returns_the_typed_model()
    {
        var parsed = await PostJson("/v1/karekod/parse", new { payload = OfficialVectors.FastSale });

        parsed["kind"]!.GetValue<string>().Should().Be("MerchantPresented");
        parsed["merchant"]!["merchantCity"]!.GetValue<string>().Should().Be("İSTANBUL");
        parsed["merchant"]!["fast"]!["flowType"]!.GetValue<string>().Should().Be("DynamicVerification");
    }

    [Fact]
    public async Task Invalid_request_is_a_validation_problem_listing_the_codes()
    {
        using var response = await _client.PostAsJsonAsync("/v1/karekod/fast", new
        {
            iban = "TR123456789012345678901234",
            merchantName = "Kafe 42",
            city = "ANKARA",
            reference = "R1",
            paymentPurpose = "01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("TRK045");
    }

    [Fact]
    public async Task Negative_amount_is_a_validation_problem_not_a_500()
    {
        using var response = await _client.PostAsJsonAsync("/v1/karekod/fast", new
        {
            iban = ValidIban,
            merchantName = "Kafe 42",
            city = "ANKARA",
            reference = "R1",
            paymentPurpose = "01",
            amount = -5m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Render_returns_a_png()
    {
        using var response = await _client.PostAsJsonAsync("/v1/karekod/render", new { payload = OfficialVectors.FastShort, scale = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task Render_refuses_invalid_payloads()
    {
        using var response = await _client.PostAsJsonAsync("/v1/karekod/render", new { payload = "000201" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        (await _client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<JsonNode> PostJson(string path, object body)
    {
        using var response = await _client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(text);
        return JsonNode.Parse(text)!;
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("Karekod:ProducerKeyHex", "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
    }
}
