using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using TrKarekod.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// A payload is at most 512 characters; nothing this API accepts needs more than a few KB.
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 16 * 1024);

builder.Services.AddSingleton(sp => KarekodSettings.From(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var otlp = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("trkarekod-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrEmpty(otlp))
        {
            t.AddOtlpExporter();
        }
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddMeter(KarekodEndpoints.MeterName);
        if (!string.IsNullOrEmpty(otlp))
        {
            m.AddOtlpExporter();
        }
    });

var app = builder.Build();

// Fail at startup, not on the first request, when the producer key is missing or weak.
_ = app.Services.GetRequiredService<KarekodSettings>();

app.UseSerilogRequestLogging();
app.MapOpenApi();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapKarekodEndpoints();

app.Run();

/// <summary>Entry point, visible to integration tests.</summary>
public partial class Program;
