# SQL Commander

A lightweight web-based SQL Server management tool for developers who need quick database exploration, query execution, and script generation without the overhead of full-featured database management tools.

## What is SQL Commander?

SQL Commander provides a single-page interface for:
- Browsing database objects (tables, views, stored procedures)
- Executing SQL queries with real-time feedback
- Generating CREATE, SELECT, and DROP scripts
- Exporting database metadata as JSON

Perfect for:
- Quick database exploration during development
- Ad-hoc query execution
- Learning SQL Server schema structures
- Lightweight alternative to SSMS for simple tasks

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 (or VS Code with C# extension)
- SQL Server (local or remote)

### Running the Application

Press F5 in Visual Studio to launch the application.

The solution uses .NET Aspire to:
- Orchestrate the SQL Commander web app
- Configure the development environment
- Manage the SQL Server connection
- Provide a rich debugging experience with the Aspire dashboard

The Aspire AppHost will automatically:
1. Launch the SQL Commander web application
2. Open the Aspire dashboard for monitoring
3. Configure logging and telemetry
4. Handle service orchestration

### First Run Setup

1. Press F5 to launch
2. In SQL Commander, click the Settings icon
3. Enter your SQL Server connection details:
   - Server name
   - Database name
   - Credentials
4. Click Test Connection to verify
5. Click Save to persist settings

## Architecture

Simple and focused:
- ASP.NET Core 8.0 with Razor Pages
- Microsoft.Data.SqlClient for database access
- .NET Aspire for development orchestration
- Serilog for structured logging

## Key Features

- Database object browser (tables, views, stored procedures)
- SQL query execution with real-time feedback
- Script generation (SELECT, CREATE, DROP)
- Schema export to JSON
- Connection management with test functionality
- Single-page interface - no navigation complexity

## Development

This solution uses **.NET Aspire** for local development:
- `AppHost` project orchestrates the application
- Aspire dashboard provides monitoring and logging
- F5 in Visual Studio launches everything automatically

### Project Structure
```
sql-commander/
├── AppHost/                    # .NET Aspire orchestration
├── SqlCommander.Web/           # Main web application
├── SqlCommander.Library/       # Business logic and services
├── SqlCommander.Library.Tests/ # Unit tests (120 tests)
└── SqlServer/                  # Database project
```

## License

MIT License - See LICENSE file for details

