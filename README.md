# SQL Cmdr

A lightweight web-based SQL Server management tool for developers who need quick database exploration, query execution, and script generation without the overhead of full-featured database management tools.

## What is SQL Cmdr?

SQL Cmdr provides a single-page interface for:
- Browsing database objects (tables, views, stored procedures)
- Executing SQL queries with real-time feedback
- Generating CREATE, SELECT, and DROP scripts
- Exporting database metadata as JSON

Perfect for:
- Quick database exploration during development
- Ad-hoc query execution
- Learning SQL Server schema structures
- Lightweight alternative to SSMS for simple tasks

## Getting Started (Local Dev)

Prerequisites:
- .NET 8 SDK
- Visual Studio 2022 or VS Code
- SQL Server (local or remote)

Press F5 to launch with .NET Aspire orchestration.

## Container Usage

Image: `jerrynixon/sql-commander`

Run:
```
docker run -p 8080:8080 \
  -e ConnectionStrings__db="Server=host.docker.internal;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=true" \
  jerrynixon/sql-commander:latest
```

Environment Variables:
- `ConnectionStrings__db` (required) SQL Server connection string
- `SQLCMDR_FILE_LOG` (optional, set to `1` to enable file logging inside container)
- `ASPNETCORE_URLS` (default `http://+:8080`)

Health Check:
- `GET /health` returns `{ "status": "ok" }`

Persistence:
- Settings stored in `sqlcmdr.settings.json` in container working directory

## VS Code Integration

- The toolbar includes an **Open in VS Code** button that builds a `vscode://ms-mssql.mssql/connect` link from your current settings and launches the VS Code connection dialog.
- Requires the [MS SQL extension for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql).
- When no server name is configured SQL Cmdr falls back to the current browser host on port 1433.

## Architecture
- ASP.NET Core 8 Razor Pages
- Microsoft.Data.SqlClient
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

