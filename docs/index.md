---
layout: default
title: SQL Commander
---

# SQL Commander

A lightweight browser-based SQL Server companion for developers who want fast schema exploration, query execution, scripting, and Data API Builder config generation without opening a heavyweight database IDE.

[View on GitHub](https://github.com/JerryNixon/sql-commander) · [Data API workflow](data-api.html) · [Authentication docs](auth-overview.html)

## What SQL Commander does

### Explore schemas quickly

Browse tables, views, stored procedures, columns, keys, and relationships in a focused single-page UI.

### Run ad-hoc SQL

Execute queries with clear status feedback, durations, row counts, cancellation, and result display.

### Generate useful artifacts

Create SQL scripts, export metadata, diagram relationships, and preview Data API Builder configs.

### Use modern authentication

Connect with SQL username/password, Azure Default Credential for local Azure SQL work, or Managed Identity in Azure.

## Documentation

- [Data API workflow](data-api.html)
- [Authentication overview](auth-overview.html)
- [Azure Default Credential authentication](auth-azure-default.html)
- [Managed Identity authentication](auth-managed-identity.html)
- [Auth validation notes](auth-validation-notes.html)

## Quick start

Run the published container image:

```bash
docker run -p 8080:8080 jerrynixon/sql-commander:latest
```

Then open `http://localhost:8080` and configure a SQL Server connection.

