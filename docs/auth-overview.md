# Authentication overview

SQL Commander supports three SQL Server connection authentication choices.

## SQL Server Authentication

Use this for local SQL Server, containers, and SQL logins.

```text
Data Source=localhost;Initial Catalog=master;User ID=sa;Password=<password>;Encrypt=True;TrustServerCertificate=True;
```

## Azure Default Credential

Use this for local development against Azure SQL without storing a database password. SQL Commander uses Azure Identity to acquire an Entra token from tools such as Azure CLI or Visual Studio.

See [Azure Default Credential authentication](auth-azure-default.md).

## Azure Managed Identity

Use this when SQL Commander runs in Azure and should connect to Azure SQL without secrets.

See [Managed Identity authentication](auth-managed-identity.md).

## Recommendation

- Local SQL Server demo: SQL Server Authentication.
- Local Azure SQL development: Azure Default Credential.
- Azure-hosted SQL Commander: Managed Identity.
