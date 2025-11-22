# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./

# Ensure DomainLists folder is copied
COPY DomainLists ./DomainLists

RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks and SSL certificates
RUN apt-get update && apt-get install -y curl ca-certificates && rm -rf /var/lib/apt/lists/*

# Update SSL certificates
RUN update-ca-certificates

# Copy published application
COPY --from=build /app/out ./

# Copy domain lists folder
COPY --from=build /app/DomainLists ./DomainLists

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/swagger || exit 1

# Run the application
ENTRYPOINT ["dotnet", "RiskyWebsitesAPI.dll"]