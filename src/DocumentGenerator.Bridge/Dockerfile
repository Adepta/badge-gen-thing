# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/DocumentGenerator.Core/DocumentGenerator.Core.csproj             src/DocumentGenerator.Core/
COPY src/DocumentGenerator.Bridge/DocumentGenerator.Bridge.csproj         src/DocumentGenerator.Bridge/

RUN dotnet restore src/DocumentGenerator.Bridge/DocumentGenerator.Bridge.csproj

COPY src/DocumentGenerator.Core/ src/DocumentGenerator.Core/
COPY src/DocumentGenerator.Bridge/ src/DocumentGenerator.Bridge/
RUN dotnet publish src/DocumentGenerator.Bridge/DocumentGenerator.Bridge.csproj \
    --no-restore -c Release -o /publish

# ── Runtime stage ──────────────────────────────────────────────────────────────
# Bridge is lightweight — no Chromium needed (all rendering is done by the cloud API).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /publish .

RUN useradd -r -s /bin/false appuser && chown -R appuser /app
USER appuser

EXPOSE 5100
ENV ASPNETCORE_URLS=http://+:5100

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5100/health || exit 1

ENTRYPOINT ["dotnet", "DocumentGenerator.Bridge.dll"]
