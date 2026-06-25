---
layout: default
title: Managed Identity authentication
---

# Managed Identity authentication

SQL Commander supports Azure SQL authentication with **system-assigned managed identity** when SQL Commander is hosted in Azure.

This is the recommended production-style passwordless option for Azure-hosted SQL Commander.

## What this mode does

When you select **Azure Managed Identity**, SQL Commander builds a connection string using:

```text
Authentication=Active Directory Managed Identity
```

`Microsoft.Data.SqlClient` obtains a token from the Azure hosting environment's managed identity endpoint.

## Supported host environments

Managed identity requires SQL Commander to run in Azure, for example:

- Azure App Service
- Azure Container Apps
- Azure Virtual Machines
- AKS with workload identity or managed identity patterns

A local developer machine cannot normally validate managed identity because it does not expose the Azure managed identity endpoint. Use **Azure Default Credential** for local passwordless testing.

## Azure setup

### 1. Enable system-assigned managed identity

For Azure App Service:

```powershell
az webapp identity assign --resource-group <resource-group> --name <web-app-name>
```

Capture the principal ID if you need it for troubleshooting:

```powershell
az webapp identity show --resource-group <resource-group> --name <web-app-name>
```

### 2. Configure Azure SQL Entra admin

Your Azure SQL logical server needs a Microsoft Entra admin. This admin is used to create contained database users for Entra identities.

### 3. Create a database user for the managed identity

Connect to the target Azure SQL database as the Entra admin and run:

```sql
CREATE USER [<web-app-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<web-app-name>];
ALTER ROLE db_datawriter ADD MEMBER [<web-app-name>];
ALTER ROLE db_ddladmin ADD MEMBER [<web-app-name>];
```

For a short validation environment, `db_owner` is simpler:

```sql
ALTER ROLE db_owner ADD MEMBER [<web-app-name>];
```

Use least privilege in real deployments.

## SQL Commander settings

Open the Azure-hosted SQL Commander site and go to **Settings** → **Connection Properties**:

| Setting | Value |
| --- | --- |
| Server Name | `tcp:<server-name>.database.windows.net,1433` |
| Database Name | Your Azure SQL database name |
| Authentication | `Azure Managed Identity` |
| User Name | Leave blank for system-assigned identity |
| Password | Leave blank |
| Encrypt | `Mandatory` |
| Trust Server Certificate | Usually off for Azure SQL |

Or paste this connection string in **Settings** → **Connection String**:

```text
Data Source=tcp:<server-name>.database.windows.net,1433;Initial Catalog=<database-name>;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Application Name=SQL Commander;Connect Timeout=30;Command Timeout=300;
```

Then click **Test Connection** or save the settings and let SQL Commander load metadata.

## What success looks like

- Status bar shows **Connected**.
- Server shows the Azure SQL logical server.
- User may show a managed identity principal format, often a GUID-like principal plus tenant ID.
- The schema tree loads database objects.

## User-assigned managed identity note

SQL Commander currently validates the system-assigned managed identity path. User-assigned managed identity needs an additional setting for the managed identity client ID and a small code path to preserve/pass that value to SqlClient.

## Troubleshooting

### Login failed for token-identified principal

The managed identity probably does not have a contained user in the database, or it lacks role membership. Create the user and grant database roles.

### CREATE USER cannot find the identity

Make sure you are connected as the Azure SQL Entra admin. In some tenants, service principal lookup may require directory permissions. For App Service system-assigned identity, using the web app resource name as the user name usually works:

```sql
CREATE USER [<web-app-name>] FROM EXTERNAL PROVIDER;
```

### Firewall error from App Service

For quick validation, allow Azure services through the Azure SQL firewall. For production, prefer private endpoints or controlled outbound networking.

### Works locally but not in Azure

Local passwordless testing uses Azure Default Credential. Managed Identity only works after the app is hosted in Azure and the managed identity has database access.
