# Docker Deployment Guide

## Overview
This document provides instructions for deploying SQL Commander to Docker Hub.

## Prerequisites
- Docker Desktop installed and running
- Docker Hub account (jerrynixon)
- Logged in to Docker Hub: `docker login`

## Quick Deploy
Use the provided batch script to build and push in one command:
```bash
build-and-push.bat 1.0.0
```

This will:
1. Build Docker images with tags `latest` and the specified version
2. Push both tags to Docker Hub

## Manual Deployment

### 1. Build Images
```bash
docker build -f SqlCmdr.Web\Dockerfile -t jerrynixon/sql-commander:latest -t jerrynixon/sql-commander:1.0.0 .
```

### 2. Test Locally
```bash
# Start container
docker run --rm -d -p 8080:8080 ^
  -e "ConnectionStrings__db=Server=host.docker.internal,1433;Database=tempdb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;" ^
  --name sqlcmdr-test ^
  jerrynixon/sql-commander:latest

# Test health endpoint
curl http://localhost:8080/health

# Stop container
docker stop sqlcmdr-test
```

### 3. Push to Docker Hub
```bash
docker push jerrynixon/sql-commander:latest
docker push jerrynixon/sql-commander:1.0.0
```

## Version Management
- Always tag with both `latest` and semantic version (e.g., `1.0.0`)
- Update version number for each release
- Consider using `1.0.0`, `1.0`, and `1` for compatibility

## CI/CD Considerations
For automated builds, consider creating a GitHub Actions workflow:
- Trigger on push to main branch
- Build and test Docker image
- Push to Docker Hub with automatic versioning
- Tag releases in GitHub

## Local Development Testing
Use docker-compose for local development with SQL Server:
```bash
docker-compose up
```

This starts:
- SQL Server 2022 on port 1433
- SQL Commander on port 8080
- Automatic health checks and service dependencies

## Image Details
- **Base Image**: mcr.microsoft.com/dotnet/aspnet:8.0
- **Build Image**: mcr.microsoft.com/dotnet/sdk:8.0
- **Port**: 8080
- **User**: Non-root user (appuser)
- **Health Endpoint**: /health

## Environment Variables
- `ConnectionStrings__db` - SQL Server connection string (required)
- Additional settings can be configured via environment variables following .NET configuration patterns

## Security Notes
- Container runs as non-root user
- No sensitive data in image
- Connection strings must be provided at runtime
- Use TrustServerCertificate=True for development only
