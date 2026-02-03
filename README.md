# SQL Commander

A lightweight web-based SQL Server management tool for developers who need quick database exploration, query execution, and script generation without the overhead of full-featured database management tools.

## What is SQL Commander?

SQL Commander provides a single-page interface for:
- Browsing database objects (tables, views, stored procedures)
- Executing SQL queries with real-time feedback
- Generating CREATE, SELECT, and DROP scripts
- Exporting database metadata as JSON
- **Azure Default Credential support** for passwordless authentication

Perfect for:
- Quick database exploration during development
- Ad-hoc query execution
- Learning SQL Server schema structures
- Lightweight alternative to SSMS for simple tasks
- Running in Azure with managed identity authentication

## Getting Started (Local Dev)

Prerequisites:
- .NET 8 SDK
- Visual Studio 2022 or VS Code
- SQL Server (local or remote)

Press F5 to launch with .NET Aspire orchestration.

## Container Usage

Image: `jerrynixon/sql-commander`

### Connecting to a Local SQL Server

When connecting to SQL Server running directly on your host machine (not in a container):

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__db="Server=host.docker.internal;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=true" \
  jerrynixon/sql-commander:latest
```

> **Note:** `host.docker.internal` resolves to your host machine from within a Docker container.

### Connecting to SQL Server in Another Container

When both SQL Cmdr and SQL Server are running as containers, they must be on the same Docker network and you must use the SQL Server container name as the server address:

```bash
# 1. Create a shared network
docker network create sql-network

# 2. Start SQL Server on the shared network
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name sql-server-dev --network sql-network \
  -d mcr.microsoft.com/mssql/server:2025-latest

# 3. Wait for SQL Server to initialize (~10 seconds), then start SQL Cmdr
docker run -p 8080:8080 --name sql-commander --network sql-network \
  -e ConnectionStrings__db="Server=sql-server-dev;Database=master;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true" \
  -d jerrynixon/sql-commander:latest
```

> **Important:** Use the container name (`sql-server-dev`) as the server address, not `host.docker.internal` or `localhost`. Container-to-container communication requires a shared Docker network.

### Azure Authentication (Managed Identity, Azure CLI, etc.)

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__db="Server=myserver.database.windows.net;Database=mydb" \
  jerrynixon/sql-commander:latest
```

When no credentials are provided in the connection string, SQL Cmdr automatically uses **Azure Default Credential** for authentication, which supports:
- Managed Identity (System-assigned or User-assigned)
- Azure CLI (`az login`)
- Visual Studio / VS Code credentials
- Azure PowerShell
- Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, etc.)

This is ideal for running in Azure Container Instances, Azure Container Apps, AKS, or any Azure service with managed identity support.

### Environment Variables
- `ConnectionStrings__db` (required) SQL Server connection string
- `SQLCMDR_FILE_LOG` (optional, set to `1` to enable file logging inside container)
- `ASPNETCORE_URLS` (default `http://+:8080`)

### Health Check
- `GET /health` returns `{ "status": "ok" }`

### Persistence
- Settings stored in `sqlcmdr.settings.json` in container working directory
- Use a volume mount to persist settings across container restarts:
  ```bash
  docker run -p 8080:8080 \
    -v $(pwd)/data:/app \
    -e ConnectionStrings__db="..." \
    jerrynixon/sql-commander:latest
  ```

## VS Code Integration

- The toolbar includes an **Open in VS Code** button that builds a `vscode://ms-mssql.mssql/connect` link from your current settings and launches the VS Code connection dialog.
- Requires the [MS SQL extension for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql).
- When no server name is configured SQL Cmdr falls back to the current browser host on port 1433.

## Authentication

SQL Cmdr supports two authentication modes (configurable via the settings modal):

### SQL Authentication
Traditional username/password authentication:
- Server: `myserver.database.windows.net`
- Database: `mydb`
- User ID: `myuser`
- Password: `mypassword`

### Azure Default Credential (Recommended for Azure)
Passwordless authentication using Azure identity:
- Server: `myserver.database.windows.net`
- Database: `mydb`
- No credentials required

The UI automatically detects and uses the appropriate authentication method. When running in Azure with managed identity, no password management is needed.

## Architecture
- ASP.NET Core 8 Razor Pages
- Microsoft.Data.SqlClient 5.2.2
- Azure.Identity 1.17.0 (for Azure Default Credential)
- Serilog (console by default, optional file sink)
- .NET Aspire (dev only)

## Project Structure
```
sql-commander/
├── AppHost/              # Aspire orchestration (dev)
├── SqlCmdr.Web/          # Web UI
├── SqlCmdr.Library/      # Services + abstractions
├── SqlCmdr.Library.Tests # Tests
└── SqlServer/            # Database project
```

## License
MIT License - See LICENSE
