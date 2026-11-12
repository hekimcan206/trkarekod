# syntax=docker/dockerfile:1

# ---- build -------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore in its own layer: it only re-runs when package versions or project files change.
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/TrKarekod/TrKarekod.csproj src/TrKarekod/
COPY src/TrKarekod.Rendering/TrKarekod.Rendering.csproj src/TrKarekod.Rendering/
COPY src/TrKarekod.Cli/TrKarekod.Cli.csproj src/TrKarekod.Cli/
COPY samples/TrKarekod.Api/TrKarekod.Api.csproj samples/TrKarekod.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore samples/TrKarekod.Api/TrKarekod.Api.csproj && \
    dotnet restore src/TrKarekod.Cli/TrKarekod.Cli.csproj

COPY .editorconfig ./
COPY src/ src/
COPY samples/TrKarekod.Api/ samples/TrKarekod.Api/
COPY vectors/ vectors/
# Framework-dependent IL (no apphost) runs unchanged on the glibc-based runtime image below.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish samples/TrKarekod.Api/TrKarekod.Api.csproj -c Release --no-restore -o /app/api -p:UseAppHost=false && \
    dotnet publish src/TrKarekod.Cli/TrKarekod.Cli.csproj -c Release --no-restore -o /app/cli -p:UseAppHost=false

# ---- runtime -----------------------------------------------------------------------------------------
# Chiseled Ubuntu: no shell, no package manager, and a non-root "app" user (UID 1654) baked in.
# One image holds the sample API (default) and the CLI (compose overrides the entrypoint for the demo).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="trkarekod-sample" \
      org.opencontainers.image.source="https://github.com/hekimcan206/trkarekod" \
      org.opencontainers.image.licenses="MIT"
COPY --from=build --link /app/api /app/api
COPY --from=build --link /app/cli /app/cli
WORKDIR /app/api
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrKarekod.Api.dll"]
