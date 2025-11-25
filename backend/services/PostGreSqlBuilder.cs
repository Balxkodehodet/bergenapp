using Microsoft.Extensions.Configuration;

namespace BergenCollectionApi.services;

// Helper responsible for building the PostgreSQL connection string from
// configuration and environment variables. Not a hosted service.
public static class PostgreSqlBuilder
{
        public static string Build(IConfiguration configuration)
        {
            // 1) If explicitly configured via ConnectionStrings:StopsDb, use it
            var configured = configuration.GetConnectionString("StopsDb");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured!;

            // 2) DATABASE_URL (Render/Heroku style)
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                try
                {
                    var uri = new Uri(databaseUrl);
                    var userInfo = uri.UserInfo.Split(':', 2);
                    var u = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
                    var p = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
                    var h = uri.Host;
                    var prt = uri.Port > 0 ? uri.Port.ToString() : (Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432");
                    var db = uri.AbsolutePath?.TrimStart('/') ?? "postgres";

                    var sslInUrl = databaseUrl.IndexOf("sslmode=require", StringComparison.OrdinalIgnoreCase) >= 0;

                    var cs = $"Host={h};Port={prt};Database={db};Username={u};Password={p};";
                    if (sslInUrl)
                        cs += "SSL Mode=Require;Trust Server Certificate=true;";

                    Console.WriteLine($"[DB] Using DATABASE_URL → Host={h}; Port={prt}; Database={db}; Username={u}");
                    return cs;
                }
                catch
                {
                    throw new InvalidOperationException("Invalid DATABASE_URL format. Provide ConnectionStrings__StopsDb or POSTGRES_* variables instead.");
                }
            }

            // 3) Environment variables (.env/User Secrets/OS env)
            var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "bergen";
            var username = Environment.GetEnvironmentVariable("POSTGRES_USER");
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "PostgreSQL credentials not configured. Set either ConnectionStrings__StopsDb or provide POSTGRES_USER and POSTGRES_PASSWORD (and optionally POSTGRES_HOST, POSTGRES_PORT, POSTGRES_DB).");
            }

            var sslMode = Environment.GetEnvironmentVariable("POSTGRES_SSLMODE");            // e.g. Require, VerifyFull
            var trustServerCert = Environment.GetEnvironmentVariable("POSTGRES_TRUST_SERVER_CERT"); // true/false

            var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};";
            if (!string.IsNullOrWhiteSpace(sslMode))
                connectionString += $"SSL Mode={sslMode};";
            if (!string.IsNullOrWhiteSpace(trustServerCert))
                connectionString += $"Trust Server Certificate={trustServerCert};";

            Console.WriteLine($"[DB] Using environment-based PostgreSQL connection → Host={host}; Port={port}; Database={database}; Username={username}");
            return connectionString;
        }
}
