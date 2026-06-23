# SQL Commander Azure Auth Validation Deployment Plan

Status: Completed - validation succeeded; temporary Azure resources deleted after testing

## Objective
Validate SQL Commander authentication modes against Azure SQL:

1. SQL username/password (already validated locally; optionally validate against Azure SQL admin if enabled)
2. Azure Default Credential from local developer environment
3. System-assigned Managed Identity from Azure-hosted SQL Commander

## Azure Context
- Subscription: validation subscription selected at runtime
- Tenant: validation tenant selected at runtime
- User: validation Entra user selected at runtime
- Preferred location: lowest-friction low-cost US region, starting with `eastus`

## Cost Controls
- Use a single short-lived resource group.
- Use the smallest practical Azure SQL database tier.
- Use free App Service if available; otherwise use the smallest practical paid tier only long enough to validate Managed Identity.
- Tear down the resource group after validation.
- Avoid long-running paid resources.

## Proposed Architecture
- Azure Resource Group: short-lived validation RG
- Azure SQL logical server with Microsoft Entra admin set to the signed-in user
- Azure SQL database for SQL Commander metadata/query tests
- Azure App Service hosting SQL Commander with system-assigned managed identity
- App Service managed identity granted database permissions in Azure SQL

## Validation Steps
1. Confirm Azure CLI context and signed-in user object ID.
2. Create short-lived resource group.
3. Create Azure SQL server/database with Entra admin configured.
4. Apply todo demo schema/data to Azure SQL.
5. Grant signed-in user database access and validate Azure Default Credential locally.
6. Publish SQL Commander and deploy it to App Service.
7. Enable system-assigned managed identity on App Service.
8. Grant App Service managed identity access to the Azure SQL database.
9. Validate Managed Identity from deployed SQL Commander.
10. Create docs under `/docs` for Azure Default Credential and Managed Identity setup.
11. Tear down the short-lived resource group after validation.

## Rollback / Cleanup
- Delete the validation resource group after testing.
- No persistent user data should be stored in Azure.

## Notes
- User explicitly requested proceeding and teardown when done.
- If Azure prompts for re-authentication, consent, MFA, or quota/capacity issues occur, pause and ask the user to interact.
- Azure Default Credential validated from local SQL Commander against Azure SQL.
- System-assigned Managed Identity validated from Azure App Service Free plan against Azure SQL.
- App Service Free was unavailable in `westus` due quota, so the web app plan was created in `northcentralus`; Azure SQL remained in `westus`.
