# ────────────────────────────────────────────────────────────────
# Stage 1 – Build
# ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy NuGet config first so the nightly feed is available to restore
COPY NuGet.Config ./

# Restore dependencies (layer-cache friendly)
COPY src/QotD.Bot/QotD.Bot.csproj src/QotD.Bot/
RUN dotnet restore src/QotD.Bot/QotD.Bot.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/QotD.Bot/QotD.Bot.csproj \
      --no-restore \
      -c Release \
      -o /app/publish

# ────────────────────────────────────────────────────────────────
# Stage 2 – Runtime
# ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Set timezone (matches the Scheduling:Timezone config value)
ENV TZ=Europe/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QotD.Bot.dll"]
