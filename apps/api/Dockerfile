# ── Stage 1: base (used by local dev via docker-compose) ─────────────────────
#
# This stage uses the full SDK image so docker-compose can run "dotnet watch run"
# inside the container with source code mounted as a volume.
# The EF Core CLI is installed here so "make migrate-add" and "make migrate-up"
# work without needing .NET installed on the host machine.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /app

# Install EF Core CLI — needed for "make migrate-add" / "make migrate-up"
# This runs on image build, not on every container start, so it's cached.
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy project file and restore packages first.
# This layer is cached — packages aren't re-downloaded unless .csproj changes.
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source code.
# docker-compose mounts the source over /app at runtime, so this is only used
# by CI (where there's no volume mount and we need the source inside the image).
COPY . .

EXPOSE 8080
# No CMD here — docker-compose provides "dotnet watch run --no-launch-profile"


# ── Stage 2: build (CI artifact + used by Stage 3) ───────────────────────────
#
# Produces a self-contained published output in /app/publish.
# This stage is NOT used for local dev — it's for App Platform and CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore


# ── Stage 3: final (used by App Platform / production) ───────────────────────
#
# Minimal runtime-only image — no SDK, no dotnet-ef, no source code.
# Roughly 200 MB smaller than the SDK image, faster startup, smaller attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# App Platform + DO health check probe against port 8080.
# ASP.NET Core 8+ defaults to port 8080 when ASPNETCORE_HTTP_PORTS is not set.
EXPOSE 8080

ENTRYPOINT ["dotnet", "SHPE-api.dll"]
