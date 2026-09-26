# Multi-stage Dockerfile for KrishiLink ASP.NET Core 8 Web Application

# Stage 1: Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Stage 2: SDK Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first to leverage Docker layer caching on dependency restore
COPY ["KrishiLink.csproj", "./"]
RUN dotnet restore "KrishiLink.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "KrishiLink.csproj" -c Release -o /app/build --no-restore

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "KrishiLink.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Stage 4: Production Final Runtime
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
COPY docker-entrypoint.sh /app/docker-entrypoint.sh

# Run as non-root app user for defense in depth
USER app

ENTRYPOINT ["/app/docker-entrypoint.sh"]
