# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore — copy only project files first for layer caching
COPY src/DocumentGenerator.Core/DocumentGenerator.Core.csproj             src/DocumentGenerator.Core/
COPY src/DocumentGenerator.Templating/DocumentGenerator.Templating.csproj src/DocumentGenerator.Templating/
COPY src/DocumentGenerator.Pdf/DocumentGenerator.Pdf.csproj               src/DocumentGenerator.Pdf/
COPY src/DocumentGenerator.Messaging/DocumentGenerator.Messaging.csproj   src/DocumentGenerator.Messaging/
COPY src/DocumentGenerator.Api/DocumentGenerator.Api.csproj               src/DocumentGenerator.Api/

RUN dotnet restore src/DocumentGenerator.Api/DocumentGenerator.Api.csproj

# Build & publish
COPY src/ src/
RUN dotnet publish src/DocumentGenerator.Api/DocumentGenerator.Api.csproj \
    --no-restore -c Release -o /publish

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install Chromium and its dependencies for PuppeteerSharp (inline render mode).
# Chromium is only used when Kafka:Enabled=false (inline path).
# In Kafka mode the Console owns the pool — Chromium is NOT launched by the API.
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium \
    fonts-liberation \
    libnss3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    && rm -rf /var/lib/apt/lists/*

# Tell PuppeteerSharp to use the system Chromium instead of downloading its own.
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

WORKDIR /app
COPY --from=build /publish .

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DocumentGenerator.Api.dll"]
