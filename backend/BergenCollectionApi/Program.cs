using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

DotNetEnv.Env.Load();

builder.Services.AddSingleton<BikeDataCache>();
builder.Services.AddHostedService<BikeDataFetcher>();
builder.Services.AddHostedService<StopDataImporter>(); // Add this line

builder.Services.AddDbContext<StopsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StopsDb")));
// Or UseSqlServer for SQL Server

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

Console.WriteLine("Backend server is running!");

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
