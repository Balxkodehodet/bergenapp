using System;
using System.IO;
using BergenCollectionApi.data;
using BergenCollectionApi.services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: allow local dev and optionally a production origin provided via env FRONTEND_ORIGIN
var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = new List<string> { "http://localhost:5173" };
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            // Support comma-separated list of origins
            allowedOrigins.AddRange(frontendOrigin
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Bind to Render-provided port if present
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// Load environment variables from .env files via centralized helper
EnvSecretsLoader.Load(builder);

builder.Services.AddSingleton<BikeDataCache>();
builder.Services.AddHostedService<BikeDataFetcher>();
builder.Services.AddHostedService<StopDataImporter>();

// Build PostgreSQL connection string via helper
var connectionString = PostgreSqlBuilder.Build(builder.Configuration);

builder.Services.AddDbContext<StopsDbContext>(options =>
    options.UseNpgsql(connectionString));
// Switched to PostgreSQL via Npgsql

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
// Respect X-Forwarded-* headers from reverse proxies like Render
var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
fwdOptions.KnownNetworks.Clear();
fwdOptions.KnownProxies.Clear();
app.UseForwardedHeaders(fwdOptions);

// Avoid HTTPS redirection issues behind a proxy unless explicitly desired
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER")))
{
    app.UseHttpsRedirection();
}
app.MapControllers();

app.Run();

Console.WriteLine("Backend server is running!");

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
