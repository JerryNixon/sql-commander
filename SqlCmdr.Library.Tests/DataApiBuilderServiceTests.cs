using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SqlCmdr.Models;
using SqlCmdr.Services;
using System.Text;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "DataApiBuilder")]
public class DataApiBuilderServiceTests
{
    sealed class CapturingDabRunner
    {
        public List<string[]> Commands { get; } = [];

        public Task RunAsync(string workingDirectory, StringBuilder diagnostics, CancellationToken cancellationToken, string[] arguments)
        {
            Commands.Add(arguments.ToArray());
            var configPath = GetOption(arguments, "--config");
            if (!string.IsNullOrWhiteSpace(configPath) && arguments.FirstOrDefault() == "init")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, "{\"autoentities\":{}}");
            }

            diagnostics.AppendLine($"fake: dab {string.Join(' ', arguments)}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GenerateConfigAsync_WithSelectedKeylessTable_ReturnsFriendlyErrorBeforeRunningDab()
    {
        var service = new DataApiBuilderService(NullLogger<DataApiBuilderService>.Instance);
        var settings = new AppSettings { Server = "localhost", Database = "master" };
        var metadata = new DatabaseMetadata();
        var table = new TableMetadata { Schema = "dbo", Name = "Ruben" };
        table.ColumnsInternal.Add(new ColumnMetadata
        {
            Name = "Name",
            DataType = "nvarchar",
            IsNullable = false,
            IsPrimaryKey = false,
            MaxLength = 100
        });
        metadata.TablesInternal.Add(table);

        var request = new DataApiBuilderGenerateRequest
        {
            Selections =
            [
                new DataApiBuilderSelection { Type = "table", Schema = "dbo", Name = "Ruben" }
            ]
        };

        var result = await service.GenerateConfigAsync(settings, metadata, request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("requires selected tables to have a primary key");
        result.ErrorMessage.Should().Contain("dbo.Ruben");
        result.Diagnostics.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateConfigAsync_WithSelectedKeylessView_ReturnsFriendlyErrorBeforeRunningDab()
    {
        var service = new DataApiBuilderService(NullLogger<DataApiBuilderService>.Instance);
        var settings = new AppSettings { Server = "localhost", Database = "master" };
        var metadata = new DatabaseMetadata();
        var view = new ViewMetadata { Schema = "dbo", Name = "vTodos" };
        view.ColumnsInternal.Add(new ColumnMetadata
        {
            Name = "id",
            DataType = "int",
            IsNullable = false,
            IsPrimaryKey = false
        });
        view.ColumnsInternal.Add(new ColumnMetadata
        {
            Name = "title",
            DataType = "nvarchar",
            IsNullable = false,
            IsPrimaryKey = false,
            MaxLength = 200
        });
        metadata.ViewsInternal.Add(view);

        var request = new DataApiBuilderGenerateRequest
        {
            Selections =
            [
                new DataApiBuilderSelection { Type = "view", Schema = "dbo", Name = "vTodos" }
            ]
        };

        var result = await service.GenerateConfigAsync(settings, metadata, request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("requires selected views to have one or more key fields");
        result.ErrorMessage.Should().Contain("dbo.vTodos");
        result.Diagnostics.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateConfigAsync_WithUnsupportedProcedureParameter_ReturnsFriendlyErrorBeforeRunningDab()
    {
        var service = new DataApiBuilderService(NullLogger<DataApiBuilderService>.Instance);
        var settings = new AppSettings { Server = "localhost", Database = "master" };
        var metadata = new DatabaseMetadata();
        var procedure = new StoredProcedureMetadata { Schema = "dbo", Name = "SetMsDescription" };
        procedure.ParametersInternal.Add(new ParameterMetadata
        {
            Name = "@value",
            DataType = "sql_variant",
            Direction = "Input",
            MaxLength = 8016
        });
        metadata.StoredProceduresInternal.Add(procedure);

        var request = new DataApiBuilderGenerateRequest
        {
            Selections =
            [
                new DataApiBuilderSelection { Type = "proc", Schema = "dbo", Name = "SetMsDescription" }
            ]
        };

        var result = await service.GenerateConfigAsync(settings, metadata, request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not support one or more selected object data types");
        result.ErrorMessage.Should().Contain("dbo.SetMsDescription");
        result.ErrorMessage.Should().Contain("sql_variant");
        result.Diagnostics.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateConfigAsync_WithSelectedRelatedTables_AddsExplicitEntitiesFieldsAndRelationships()
    {
        var runner = new CapturingDabRunner();
        var service = new DataApiBuilderService(NullLogger<DataApiBuilderService>.Instance, runner.RunAsync);
        var settings = new AppSettings { Server = "localhost", Database = "master", DataApiMcpEnabled = true };
        var metadata = new DatabaseMetadata();
        metadata.TablesInternal.Add(CreateTable("dbo", "TodoCategory", ("Id", "int", true), ("Name", "nvarchar", false)));
        metadata.TablesInternal.Add(CreateTable("dbo", "TodoItem", ("Id", "int", true), ("CategoryId", "int", false), ("Title", "nvarchar", false)));
        metadata.ForeignKeysInternal.Add(new ForeignKeyMetadata
        {
            Name = "FK_TodoItem_TodoCategory",
            ParentSchema = "dbo",
            ParentTable = "TodoItem",
            ParentColumn = "CategoryId",
            ReferencedSchema = "dbo",
            ReferencedTable = "TodoCategory",
            ReferencedColumn = "Id",
            ConstraintColumnId = 1
        });

        var request = new DataApiBuilderGenerateRequest
        {
            Selections =
            [
                new DataApiBuilderSelection { Type = "table", Schema = "dbo", Name = "TodoCategory" },
                new DataApiBuilderSelection { Type = "table", Schema = "dbo", Name = "TodoItem" }
            ]
        };

        var result = await service.GenerateConfigAsync(settings, metadata, request);

        result.Success.Should().BeTrue(result.ErrorMessage);
    result.ConfigJson.Should().NotContain("autoentities");
        runner.Commands.Should().NotContain(command => command.FirstOrDefault() == "auto-config");
        runner.Commands.Should().Contain(command => StartsWith(command, "add", "TodoCategory"));
        runner.Commands.Should().Contain(command => StartsWith(command, "add", "TodoItem"));

        var todoItemMetadataUpdate = runner.Commands.Single(command =>
            StartsWith(command, "update", "TodoItem") &&
            command.Contains("--fields.name"));
        GetOption(todoItemMetadataUpdate, "--fields.name").Should().Be("Id,CategoryId,Title");
        GetOption(todoItemMetadataUpdate, "--fields.description").Should().Contain("Column CategoryId from dbo.TodoItem");
        GetOption(todoItemMetadataUpdate, "--fields.primary-key").Should().Be("true,false,false");

        runner.Commands.Should().Contain(command =>
            StartsWith(command, "update", "TodoItem") &&
            GetOption(command, "--relationship") == "todoCategory" &&
            GetOption(command, "--target.entity") == "TodoCategory" &&
            GetOption(command, "--cardinality") == "one" &&
            GetOption(command, "--relationship.fields") == "CategoryId:Id");

        runner.Commands.Should().Contain(command =>
            StartsWith(command, "update", "TodoCategory") &&
            GetOption(command, "--relationship") == "todoItems" &&
            GetOption(command, "--target.entity") == "TodoItem" &&
            GetOption(command, "--cardinality") == "many" &&
            GetOption(command, "--relationship.fields") == "Id:CategoryId");
    }

    [Fact]
    public async Task GenerateConfigAsync_WithOnlyOneSideOfForeignKey_SkipsRelationship()
    {
        var runner = new CapturingDabRunner();
        var service = new DataApiBuilderService(NullLogger<DataApiBuilderService>.Instance, runner.RunAsync);
        var settings = new AppSettings { Server = "localhost", Database = "master" };
        var metadata = new DatabaseMetadata();
        metadata.TablesInternal.Add(CreateTable("dbo", "TodoCategory", ("Id", "int", true)));
        metadata.TablesInternal.Add(CreateTable("dbo", "TodoItem", ("Id", "int", true), ("CategoryId", "int", false)));
        metadata.ForeignKeysInternal.Add(new ForeignKeyMetadata
        {
            Name = "FK_TodoItem_TodoCategory",
            ParentSchema = "dbo",
            ParentTable = "TodoItem",
            ParentColumn = "CategoryId",
            ReferencedSchema = "dbo",
            ReferencedTable = "TodoCategory",
            ReferencedColumn = "Id",
            ConstraintColumnId = 1
        });

        var request = new DataApiBuilderGenerateRequest
        {
            Selections =
            [
                new DataApiBuilderSelection { Type = "table", Schema = "dbo", Name = "TodoItem" }
            ]
        };

        var result = await service.GenerateConfigAsync(settings, metadata, request);

        result.Success.Should().BeTrue(result.ErrorMessage);
        runner.Commands.Should().NotContain(command => command.Contains("--relationship"));
    }

    static TableMetadata CreateTable(string schema, string name, params (string Name, string DataType, bool IsPrimaryKey)[] columns)
    {
        var table = new TableMetadata { Schema = schema, Name = name };
        foreach (var column in columns)
        {
            table.ColumnsInternal.Add(new ColumnMetadata
            {
                Name = column.Name,
                DataType = column.DataType,
                IsNullable = !column.IsPrimaryKey,
                IsPrimaryKey = column.IsPrimaryKey,
                MaxLength = column.DataType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) ? 100 : null
            });
        }

        return table;
    }

    static string? GetOption(IReadOnlyList<string> arguments, string optionName)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == optionName)
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    static bool StartsWith(IReadOnlyList<string> arguments, string first, string second)
    {
        return arguments.Count >= 2 && arguments[0] == first && arguments[1] == second;
    }
}
