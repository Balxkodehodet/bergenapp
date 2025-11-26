using System;
using System.IO;
using Microsoft.AspNetCore.Builder;

namespace BergenCollectionApi.services;

/// <summary>
/// Centralized loader for environment variables and secrets from .env files.
/// Loads, in order:
/// 1) Root .env next to the application (optional)
/// 2) backend/secrets/.env
/// 3) repo-root/secrets/.env
/// </summary>
public static class EnvSecretsLoader
{
    public static void Load(WebApplicationBuilder builder)
    {
        // 1) Root .env next to the application (optional)
        DotNetEnv.Env.Load();

        // 2) secrets/.env in backend folder or repo root (preferred for local secrets)
        try
        {
            var contentRoot = builder.Environment.ContentRootPath; // typically .../backend
            var backendSecrets = Path.GetFullPath(Path.Combine(contentRoot, "secrets", ".env"));
            var repoRootSecrets = Path.GetFullPath(Path.Combine(contentRoot, "..", "secrets", ".env"));

            if (File.Exists(backendSecrets)) DotNetEnv.Env.Load(backendSecrets);
            if (File.Exists(repoRootSecrets)) DotNetEnv.Env.Load(repoRootSecrets);
        }
        catch
        {
            // ignore if secrets file is missing or any IO errors occur
        }
    }
}
