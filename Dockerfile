# Multi-stage build for MAria2 Download Manager

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY *.sln .
COPY MAria2.Core/*.csproj ./MAria2.Core/
COPY MAria2.Application/*.csproj ./MAria2.Application/
COPY MAria2.Infrastructure/*.csproj ./MAria2.Infrastructure/
COPY MAria2.Presentation/*.csproj ./MAria2.Presentation/
COPY MAria2.UnitTests/*.csproj ./MAria2.UnitTests/
COPY MAria2.IntegrationTests/*.csproj ./MAria2.IntegrationTests/

# Restore dependencies
RUN dotnet restore

# Copy entire solution
COPY . .

# Build solution
RUN dotnet build -c Release --no-restore

# Run tests
RUN dotnet test -c Release --no-build --verbosity normal

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish MAria2.Presentation -c Release -o /app/publish --no-build

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install FFmpeg and other dependencies
RUN apt-get update && \
    apt-get install -y ffmpeg wget curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy published artifacts
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose ports
EXPOSE 5000
EXPOSE 5001

# Create non-root user for security
RUN groupadd -r maria2 && useradd -r -g maria2 maria2
RUN mkdir -p /app/downloads && chown -R maria2:maria2 /app/downloads
USER maria2

# Configure health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s \
  CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "MAria2.Presentation.dll"]
