---
layout: default
title: SQL Commander auth validation notes
---

# SQL Commander auth validation notes

This document records the auth validation scenarios for SQL Commander.

## Validated scenarios

### SQL username/password

Validated against local SQL Server using SQL authentication.

Example connection string:

```text
Data Source=localhost;Initial Catalog=master;User ID=sa;Password=<password>;Pooling=True;MultipleActiveResultSets=False;Encrypt=Mandatory;TrustServerCertificate=True;Application Name=SQL Commander;Connect Timeout=30;Command Timeout=300;
```

### Azure Default Credential

Validated from local SQL Commander against Azure SQL using the signed-in Azure CLI account.

Connection string shape:

```text
Data Source=tcp:<server-name>.database.windows.net,1433;Initial Catalog=<database-name>;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Application Name=SQL Commander;Connect Timeout=30;Command Timeout=300;
```

Observed success indicators:

- SQL Commander status: `Connected`
- User: Entra user principal, such as `user@example.com` or an external-account representation
- Metadata loaded from Azure SQL

### System-assigned Managed Identity

Validated from Azure App Service with system-assigned managed identity against Azure SQL.

Connection string shape:

```text
Data Source=tcp:<server-name>.database.windows.net,1433;Initial Catalog=<database-name>;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Application Name=SQL Commander;Connect Timeout=30;Command Timeout=300;
```

Observed success indicators:

- SQL Commander status: `Connected`
- User: managed identity principal representation
- Metadata loaded from Azure SQL

## Temporary validation environment

The validation used a short-lived Azure SQL database and Azure App Service Free plan. The temporary resource group was deleted after validation to control cost.

## Current limitations

- System-assigned managed identity works.
- User-assigned managed identity is not yet a first-class SQL Commander setting. Add a managed identity client ID setting before documenting it as fully supported.
