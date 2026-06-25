---
layout: default
title: Data API workflow
---

# Data API workflow

SQL Commander can turn selected SQL Server objects into a Data API Builder configuration. The workflow is designed to be explicit: choose the API features you want, choose the database objects to expose, preview the generated config, and then start the Data API runtime.

## Feature toggles

The Data API panel includes three feature toggles. These control which endpoint families SQL Commander enables in the generated Data API Builder config.

### REST

REST exposes selected tables, views, and stored procedures as HTTP endpoints. Use REST when you want straightforward resource-style access from web apps, scripts, integration tools, or API clients.

When REST is enabled, generated entities include REST exposure so clients can call endpoint paths for the selected objects.

### GraphQL

GraphQL exposes selected objects through a GraphQL endpoint. Use GraphQL when clients need to request nested or shaped data without creating separate REST calls for every view of the data.

When GraphQL is enabled, SQL Commander includes GraphQL exposure for generated entities. Relationships detected from selected foreign keys can make the GraphQL experience more useful because related objects can be navigated from the API model.

### MCP

MCP enables Data API Builder's Model Context Protocol surface. Use MCP when AI tools or agents need a structured way to discover and call database-backed operations through the Data API runtime.

When MCP is enabled, SQL Commander configures the generated Data API Builder runtime so compatible MCP clients can discover the exposed data operations.

## Object selection

The object picker is where you decide exactly what the generated API can expose.

- **Tables** can be selected when Data API Builder can safely address rows, which means a primary key is required.
- **Views** can be selected when you provide one or more key fields.
- **Stored procedures** can be selected when their parameters and return shapes use supported SQL types.
- **Select All** and **Select None** let you quickly start broad or narrow.
- **Show user objects only** keeps system/internal database objects out of the picker.
- The filter box narrows the visible objects without changing what has already been selected.

SQL Commander generates explicit Data API Builder entities for the selected objects instead of relying on automatic entity generation. That keeps the configuration readable and allows relationships to be included when selected tables are connected by foreign keys.

## Keys for views

Views do not always expose primary-key metadata the way tables do, but Data API Builder still needs key fields to address a view entity correctly.

For each selected view, SQL Commander shows available columns as **Key** choices. Select the column, or columns, that uniquely identify each row in the view.

Use a key choice that is stable and unique. If the view is built from a table with an `Id` column, that `Id` column is often the best starting point.

## Unsupported objects and types

Some SQL Server objects cannot be exposed safely or correctly by Data API Builder. SQL Commander keeps those objects visible but disables them with a reason.

Common reasons include:

- **Needs primary key**: a table has no primary key, so Data API Builder cannot identify individual rows.
- **Unsupported type**: a column, stored procedure parameter, or result shape uses a SQL type that Data API Builder does not support for that object.

Disabled objects are intentionally left out of generated configs. Fix the database object, choose a compatible view/procedure shape, or leave the object unselected.

## Previewing and running

After selecting objects and features, choose **Preview Config File** to inspect the generated Data API Builder configuration before starting anything.

Choose **Start** to launch the Data API runtime. When it is running, the status bar changes to show the Data API state and the runtime endpoints become available.

## Recommended workflow

1. Start SQL Commander with Docker.
2. Configure and test the SQL Server connection.
3. Open **Data API** from the status bar.
4. Enable REST, GraphQL, MCP, or the combination you need.
5. Select tables, views, and stored procedures.
6. Pick key fields for selected views.
7. Review disabled objects and fix unsupported database shapes if needed.
8. Preview the generated config.
9. Start the Data API runtime and use the generated endpoints.
