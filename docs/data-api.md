---
layout: default
title: Data API workflow reference
---

# Data API workflow reference

Describes the SQL Commander workflow that generates and runs a Data API Builder runtime for selected SQL Server objects.

## Definition

```text
Data API workflow
```

## Product

SQL Commander

## Namespace

```text
SqlCmdr.Web.DataApi
```

## Assembly

```text
SqlCmdr.Web.dll
```

## Summary

The Data API workflow turns selected SQL Server tables, views, and stored procedures into a running [Data API Builder](https://aka.ms/dab/docs) API. The workflow is browser-driven: connect to SQL Server, choose endpoint families, select database objects, preview the generated configuration, and start the runtime.

SQL Commander launches the Data API Builder CLI behind the scenes and exposes the runtime through stable SQL Commander proxy paths such as `/data-api/swagger/`, `/data-api/graphql`, and `/data-api/mcp`.

## In this article

- [Definition](#definition)
- [Remarks](#remarks)
- [Properties](#properties)
- [Methods](#methods)
- [Examples](#examples)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Applies to](#applies-to)

## Remarks

Use this workflow when you want to quickly expose an existing SQL Server database through REST, GraphQL, or MCP without hand-authoring a Data API Builder configuration file.

The workflow is explicit. SQL Commander does not automatically expose every object. You choose what to expose, preview the generated configuration, and then start the Data API runtime.

When the runtime starts, SQL Commander uses the generated config and a connection string environment variable named `SQLCMDR_DAB_CONNECTION_STRING`. The generated config references the connection string instead of embedding the secret directly.

## Properties

The following options control the generated Data API Builder configuration.

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `RestEnabled` | `Boolean` | `true` | Enables REST endpoints and Swagger UI for selected objects. |
| `GraphQLEnabled` | `Boolean` | `true` | Enables the GraphQL endpoint and Nitro explorer. |
| `McpEnabled` | `Boolean` | `true` | Enables Data API Builder's MCP endpoint for compatible AI clients. |
| `Selections` | `DataApiBuilderSelection[]` | Empty list | The selected tables, views, and stored procedures to expose. |
| `ViewKeyFields` | `String[]` | Empty list | Key fields selected for views. Required for selected views that do not expose primary-key metadata. |
| `ShowUserObjectsOnly` | `Boolean` | `true` | Hides internal/system-style database objects in the picker. |

## Methods

The following actions are available from the Data API UI.

| Method | Description |
| --- | --- |
| `OpenDataApi()` | Opens the Data API object picker and runtime panel. |
| `SelectObjects()` | Selects tables, views, and stored procedures to expose. |
| `PreviewConfig()` | Generates and displays the Data API Builder config without starting the runtime. |
| `StartRuntime()` | Generates the config and starts the Data API Builder runtime. |
| `StopRuntime()` | Stops the running Data API Builder process. |
| `OpenSwagger()` | Opens the REST/Swagger UI surface. |
| `OpenGraphQL()` | Opens the GraphQL/Nitro surface. |
| `OpenMcp()` | Opens or copies the MCP endpoint for compatible clients. |

## Endpoint properties

When the runtime is running, SQL Commander exposes these stable paths.

| Endpoint | Value | Description |
| --- | --- | --- |
| `HealthUrl` | `/data-api/health` | Runtime health endpoint. |
| `SwaggerUrl` | `/data-api/swagger/` | REST documentation and test UI. |
| `RestBaseUrl` | `/data-api/api/{EntityName}` | REST entity endpoint base path. |
| `GraphQLUrl` | `/data-api/graphql` | GraphQL endpoint and Nitro explorer. |
| `McpUrl` | `/data-api/mcp` | MCP endpoint. |

The internal Data API Builder port can change. Use the SQL Commander `/data-api/...` proxy paths from the browser.

## Object requirements

| Object type | Requirement | Generated behavior |
| --- | --- | --- |
| Table | Must have a primary key. | Can be exposed through REST, GraphQL, and MCP DML tools. |
| View | Must have one or more selected key fields. | Exposed as a read-oriented entity. |
| Stored procedure | Parameters and result shape must use supported SQL types. | Exposed as an executable operation. |

## Warning indicators

SQL Commander marks objects that need attention before they are exposed.

| Indicator | Meaning | Resolution |
| --- | --- | --- |
| Unsupported type marker | A column, parameter, or result shape uses a SQL type Data API Builder cannot expose. | Deselect the object, change the database shape, or expose a compatible view/procedure. |
| Non-user object marker | The object is normally hidden by **Show user objects only**. | Leave unselected unless you intentionally need it. |
| Needs primary key | A table has no primary key. | Add a primary key or expose a keyed view instead. |

Common unsupported SQL types include:

- `geography`
- `geometry`
- `hierarchyid`
- `json`
- `rowversion`
- `timestamp`
- `sql_variant`
- `vector`
- `xml`

## Examples

### Example 1: Enable REST for a table

Select `dbo.TodoItem`, enable **REST**, and choose **Start**.

```http
GET /data-api/api/TodoItem
```

Example request:

```bash
curl http://localhost:8080/data-api/api/TodoItem
```

Use REST when clients need straightforward HTTP access to selected entities.

### Example 2: Query selected objects with GraphQL

Enable **GraphQL**, start the runtime, and open `/data-api/graphql`.

Example query shape:

```graphql
query {
  todoItems {
    items {
      id
      title
      isComplete
    }
  }
}
```

Use GraphQL when clients need shaped results or relationship traversal.

### Example 3: Enable MCP for AI tools

Enable **MCP**, start the runtime, and use the generated endpoint:

```text
http://localhost:8080/data-api/mcp
```

The runtime panel includes a **VS Code** action for adding the endpoint to compatible MCP clients.

### Example 4: Select a small todo API

For a todo database, select these objects:

```text
dbo.TodoCategory
dbo.TodoList
dbo.TodoItem
```

Then enable the endpoint families you need:

| Scenario | Recommended toggles |
| --- | --- |
| Simple web or script client | REST |
| Client needs shaped data | GraphQL |
| AI client or agent integration | MCP |
| Demo or exploration | REST, GraphQL, and MCP |

### Example 5: Preview the generated configuration

Choose **Preview Config File** before starting the runtime.

Example config shape:

```json
{
  "$schema": "https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json",
  "data-source": {
    "database-type": "mssql",
    "connection-string": "@env('SQLCMDR_DAB_CONNECTION_STRING')"
  },
  "runtime": {
    "rest": {
      "enabled": true
    },
    "graphql": {
      "enabled": true
    },
    "mcp": {
      "enabled": true
    }
  },
  "entities": {
    "TodoItem": {
      "source": "[dbo].[TodoItem]",
      "permissions": [
        {
          "role": "anonymous",
          "actions": ["*"]
        }
      ]
    }
  }
}
```

The connection string is referenced through `SQLCMDR_DAB_CONNECTION_STRING`. SQL Commander sets that environment variable when it starts the runtime.

### Example 6: Choose key fields for a view

Views need one or more key fields. Good key fields are stable, unique, and present in every row.

Good examples:

```text
Id
TodoItemId
TenantId + Id
```

Avoid fields that can change frequently, such as display names, descriptions, or status text.

## Security

The generated workflow is intended for local development, demos, and controlled environments. Review the generated config before exposing the runtime more broadly.

Important considerations:

- Generated permissions are broad for convenience.
- Do not expose sensitive tables or procedures unless you understand the generated actions.
- Treat connection strings and credentials as secrets.
- Use network controls, authentication, or a hardened Data API Builder configuration for production scenarios.

## Troubleshooting

### The Data API CLI is not found

Install the Data API Builder CLI or use the SQL Commander container image, which includes it.

### A selected table is disabled

Check whether the table has a primary key. Data API Builder needs a key to address individual rows.

### A selected view needs keys

Pick one or more fields that uniquely identify a row in the view.

### An object has an unsupported type warning

Remove the object from the selection, change the SQL type, or create a compatible view/procedure shape that excludes the unsupported column or parameter.

### The runtime starts but endpoints do not work

Check the diagnostics output in SQL Commander. Also verify that:

- The SQL connection is still valid.
- The selected objects still exist.
- The object names did not change after refresh.
- The Data API status shows **Running**.

## Applies to

| Product | Version |
| --- | --- |
| SQL Commander | 1.1.1 and later |
| Data API Builder CLI | 2.0.9 and later |

## See also

- [SQL Commander home](index.html)
- [Authentication overview](auth-overview.html)
- [Azure Default Credential authentication](auth-azure-default.html)
- [Managed Identity authentication](auth-managed-identity.html)
- [Data API Builder documentation](https://aka.ms/dab/docs)
