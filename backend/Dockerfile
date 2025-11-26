# ========================
#  Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Configure build args for deterministic builds (optional)
ARG BUILD_CONFIGURATION=Release
ARG CI=false

WORKDIR /src

# Copy csproj first for better layer caching
COPY ./BergenCollectionApi.csproj ./
RUN dotnet restore "./BergenCollectionApi.csproj"

# Copy the rest of the backend sources
COPY . .

# Publish the app to a self-contained folder (framework-dependent runtime)
RUN dotnet publish "./BergenCollectionApi.csproj" -c $BUILD_CONFIGURATION -o /app


# ========================
#  Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Install useful OS packages: CA certs, tzdata for correct time zone, curl for debugging
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates \
       tzdata \
       curl \
    && rm -rf /var/lib/apt/lists/* \
    && update-ca-certificates

# Set timezone (optional). You can override via TZ env at runtime.
ENV TZ=Etc/UTC

# Security best practices
# - Create a non-root user
RUN useradd -m -u 10001 appuser

WORKDIR /app

# Copy published output from build stage
COPY --from=build /app .

# Mark we are on Render to avoid HTTPS redirect issues behind a proxy (your Program.cs already checks this)
ENV RENDER=true

# Optional: increase Kestrel request body limits or thread pool (tune as needed)
# ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# ENV DOTNET_ReadyToRun=0

# HEALTHCHECK for Render/K8s
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -fsS "http://127.0.0.1:${PORT:-10000}/api/stops/all" || exit 1

# Switch to non-root
USER appuser

# Expose an arbitrary port for local runs (Render sets PORT at runtime)
EXPOSE 8080

# Start the API
CMD ["dotnet", "BergenCollectionApi.dll"]