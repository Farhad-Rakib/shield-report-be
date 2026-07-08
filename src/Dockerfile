# Build stage
# NOTE: build context is the repo root (shield-report-be/), not src/, so that
# Directory.Build.props (sets TargetFramework/Nullable/etc for every project) is visible to
# MSBuild — it lives one level above src/ and restore fails with NETSDK1013 without it.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["Directory.Build.props", "./"]

# Copy project files
COPY ["src/ShieldReport.Api/ShieldReport.Api.csproj", "src/ShieldReport.Api/"]
COPY ["src/ShieldReport.Application/ShieldReport.Application.csproj", "src/ShieldReport.Application/"]
COPY ["src/ShieldReport.Domain/ShieldReport.Domain.csproj", "src/ShieldReport.Domain/"]
COPY ["src/ShieldReport.Infrastructure/ShieldReport.Infrastructure.csproj", "src/ShieldReport.Infrastructure/"]
COPY ["src/ShieldReport.Persistence/ShieldReport.Persistence.csproj", "src/ShieldReport.Persistence/"]
COPY ["src/ShieldReport.Scanning/ShieldReport.Scanning.csproj", "src/ShieldReport.Scanning/"]

# Restore dependencies
RUN dotnet restore "src/ShieldReport.Api/ShieldReport.Api.csproj"

# Copy remaining files
COPY . .

# Build the application
RUN dotnet build "src/ShieldReport.Api/ShieldReport.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/ShieldReport.Api/ShieldReport.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Expose default port (HTTP only — see docker-compose.yml for the LAN demo setup)
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ShieldReport.Api.dll"]
