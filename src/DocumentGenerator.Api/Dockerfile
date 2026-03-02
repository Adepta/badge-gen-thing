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
# aspnet:10.0 is Ubuntu Noble. Noble ships Chromium as a snap-only stub that
# does not work in containers. Install Google Chrome Stable from Google's apt
# repo instead — it provides a real binary at /usr/bin/google-chrome-stable.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Chromium is only used when Kafka:Enabled=false (inline render path).
# In Kafka mode the Console owns Chrome — this install is a safety net.
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget gnupg ca-certificates \
    && wget -q -O- https://dl-ssl.google.com/linux/linux_signing_key.pub \
       | gpg --dearmor -o /usr/share/keyrings/google-chrome.gpg \
    && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-chrome.gpg] \
       http://dl.google.com/linux/chrome/deb/ stable main" \
       > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update && apt-get install -y --no-install-recommends \
    google-chrome-stable \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome-stable
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

WORKDIR /app
COPY --from=build /publish .

RUN useradd -r -m -s /bin/false appuser && chown -R appuser /app /home/appuser
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=5 \
    CMD wget --spider -q http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DocumentGenerator.Api.dll"]
