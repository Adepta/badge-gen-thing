# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

# Install Node.js for npm build (Tailwind + Monaco copy)
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy solution and project files first (for restore caching)
COPY DocumentGenerator.Editor.sln ./
COPY src/DocumentGenerator.Editor.Core/DocumentGenerator.Editor.Core.csproj ./src/DocumentGenerator.Editor.Core/
COPY src/DocumentGenerator.Editor.Infrastructure/DocumentGenerator.Editor.Infrastructure.csproj ./src/DocumentGenerator.Editor.Infrastructure/
COPY src/DocumentGenerator.Editor.Web/DocumentGenerator.Editor.Web.csproj ./src/DocumentGenerator.Editor.Web/
COPY src/DocumentGenerator.Editor.Web/package.json ./src/DocumentGenerator.Editor.Web/
COPY src/DocumentGenerator.Editor.Web/package-lock.json ./src/DocumentGenerator.Editor.Web/

# Restore NuGet packages
RUN dotnet restore

# Install npm dependencies
WORKDIR /src/src/DocumentGenerator.Editor.Web
RUN npm ci

# Copy everything
WORKDIR /src
COPY . .

# Build npm assets (Tailwind + Monaco)
WORKDIR /src/src/DocumentGenerator.Editor.Web
RUN npm run build

# Publish .NET app
WORKDIR /src
RUN dotnet publish src/DocumentGenerator.Editor.Web -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime

# Create non-root user
RUN groupadd -r editor && useradd -r -g editor -d /app editor

WORKDIR /app
COPY --from=build /app/publish .

# Create templates directory
RUN mkdir -p /templates/assets && chown -R editor:editor /templates

USER editor

ENV ASPNETCORE_URLS=http://+:3500
ENV EDITOR__TEMPLATESDIR=/templates
ENV EDITOR__ASSETSDIR=/templates/assets
ENV EDITOR__SQLITECONNECTIONSTRING="Data Source=/app/editor-metadata.db"

EXPOSE 3500

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:3500/ || exit 1

ENTRYPOINT ["dotnet", "DocumentGenerator.Editor.Web.dll"]
