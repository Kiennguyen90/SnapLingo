# syntax=docker/dockerfile:1
# Build context: apps/api   (docker build -f apps/api/Dockerfile apps/api)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a csproj changes.
COPY SnapLingo.slnx ./
COPY src/SnapLingo.Domain/*.csproj src/SnapLingo.Domain/
COPY src/SnapLingo.Contracts/*.csproj src/SnapLingo.Contracts/
COPY src/SnapLingo.Application/*.csproj src/SnapLingo.Application/
COPY src/SnapLingo.Infrastructure/*.csproj src/SnapLingo.Infrastructure/
COPY src/SnapLingo.Api/*.csproj src/SnapLingo.Api/
RUN dotnet restore src/SnapLingo.Api/SnapLingo.Api.csproj

COPY src/ src/
RUN dotnet publish src/SnapLingo.Api/SnapLingo.Api.csproj \
    -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    Storage__RootPath=/data/storage

# Run as the non-root user shipped in the base image; /data holds uploaded media.
RUN mkdir -p /data/storage && chown -R $APP_UID /data
USER $APP_UID
VOLUME /data/storage

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SnapLingo.Api.dll"]
