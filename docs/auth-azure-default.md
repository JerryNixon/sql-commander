---
layout: default
title: Azure Default Credential authentication
---

# Azure Default Credential authentication

SQL Commander supports Microsoft Entra authentication to Azure SQL by using **Azure Default Credential**. This is the best local-development option when you do not want to use SQL usernames and passwords.

## What this mode does

When you select **Azure Default Credential**, SQL Commander asks Azure Identity for an access token scoped to Azure SQL:

```text
https://database.windows.net/.default
```

The app can get that token from developer tools such as Azure CLI, Visual Studio, Visual Studio Code, Azure PowerShell, or environment credentials.

## Prerequisites

- An Azure SQL logical server and database.
- Microsoft Entra admin configured on the Azure SQL server.
- Your signed-in Entra user has a contained database user in the target database.
- Your client IP is allowed by the Azure SQL firewall, or you are connecting from an allowed network.
- You are signed in locally with Azure CLI or another supported Azure Identity source.

## Azure setup

Set the Entra admin on the Azure SQL server. Then connect as that admin and grant your user access in the database:

```sql
CREATE USER [you@example.com] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [you@example.com];
ALTER ROLE db_datawriter ADD MEMBER [you@example.com];
ALTER ROLE db_ddladmin ADD MEMBER [you@example.com];
```

For a short demo or validation environment, `db_owner` is simpler:

```sql
ALTER ROLE db_owner ADD MEMBER [you@example.com];
```

Use least privilege for real environments.

## Local sign-in

Sign in to Azure CLI with the same tenant/subscription context that has access to the Azure SQL database:

```powershell
az login --tenant <tenant-id>
az account set --subscription <subscription-id>
```

Confirm the current user:

```powershell
az account show
az ad signed-in-user show
```

## SQL Commander settings

Open **Settings** → **Connection Properties**:

| Setting | Value |
| --- | --- |
| Server Name | `tcp:<server-name>.database.windows.net,1433` |
| Database Name | Your Azure SQL database name |
| Authentication | `Azure Default Credential` |
| User Name | Leave blank |
| Password | Leave blank |
| Encrypt | `Mandatory` |
| Trust Server Certificate | Usually off for Azure SQL |

Or paste a connection string in **Settings** → **Connection String**:

```text
Data Source=tcp:<server-name>.database.windows.net,1433;Initial Catalog=<database-name>;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Application Name=SQL Commander;Connect Timeout=30;Command Timeout=300;
```

Then click **Test Connection** or save the settings and let SQL Commander load metadata.

## What success looks like

- Status bar shows **Connected**.
- Server shows your Azure SQL logical server.
- User shows your Entra user, for example `live.com#user@example.com` or `user@example.com`.
- The schema tree loads tables, views, and stored procedures.

## Troubleshooting

### Login failed for token-identified principal

The signed-in identity does not have a database user or role membership in the target database. Create the user and grant roles as shown above.

### Token acquisition failed

Check local Azure sign-in:

```powershell
az account get-access-token --resource https://database.windows.net/
```

If this fails, sign in again with `az login`.

### Firewall or network error

Add your public IP to the Azure SQL server firewall or use a private network path.

### Wrong tenant

If you have multiple tenants, make sure Azure CLI is signed into the tenant that owns the Azure SQL server and where your database user was created.
