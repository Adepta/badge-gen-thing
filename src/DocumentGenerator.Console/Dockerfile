# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/DocumentGenerator.Core/DocumentGenerator.Core.csproj             src/DocumentGenerator.Core/
COPY src/DocumentGenerator.Templating/DocumentGenerator.Templating.csproj src/DocumentGenerator.Templating/
COPY src/DocumentGenerator.Pdf/DocumentGenerator.Pdf.csproj               src/DocumentGenerator.Pdf/
COPY src/DocumentGenerator.Messaging/DocumentGenerator.Messaging.csproj   src/DocumentGenerator.Messaging/
COPY src/DocumentGenerator.Console/DocumentGenerator.Console.csproj       src/DocumentGenerator.Console/

RUN dotnet restore src/DocumentGenerator.Console/DocumentGenerator.Console.csproj

COPY src/ src/
RUN dotnet publish src/DocumentGenerator.Console/DocumentGenerator.Console.csproj \
    --no-restore -c Release -o /publish

# ── Runtime stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Chromium is required by PuppeteerSharp for PDF rendering.
# --no-sandbox is used at runtime because Docker containers lack the kernel
# namespace isolation that Chromium's sandbox requires.
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

ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

WORKDIR /app
COPY --from=build /publish .

RUN adduser --disabled-password --gecos "" appuser \
    && chown -R appuser /app \
    && mkdir -p /app/output /app/logs \
    && chown -R appuser /app/output /app/logs
USER appuser

ENTRYPOINT ["dotnet", "DocumentGenerator.Console.dll"]
