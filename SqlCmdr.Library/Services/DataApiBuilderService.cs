using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;
using SqlCmdr.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SqlCmdr.Services;

internal delegate Task DabCommandRunner(string workingDirectory, StringBuilder diagnostics, CancellationToken cancellationToken, string[] arguments);

public sealed class DataApiBuilderService : IDataApiBuilderService
{
    const string DabConnectionStringEnvironmentVariable = "SQLCMDR_DAB_CONNECTION_STRING";
    const int DefaultDataApiPort = 5001;

    readonly ILogger<DataApiBuilderService> _logger;
    readonly DabCommandRunner _runDabAsync;
    readonly object _runtimeLock = new();
    RunningDataApi? _runtime;
    DataApiRuntimeResponse? _lastRuntimeStatus;

    public DataApiBuilderService(ILogger<DataApiBuilderService> logger)
        : this(logger, RunDabProcessAsync)
    {
    }

    internal DataApiBuilderService(ILogger<DataApiBuilderService> logger, DabCommandRunner runDabAsync)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runDabAsync = runDabAsync ?? throw new ArgumentNullException(nameof(runDabAsync));
    }

    public async Task<DataApiBuilderGenerateResponse> GenerateConfigAsync(
        AppSettings settings,
        DatabaseMetadata metadata,
        DataApiBuilderGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(settings.Server))
        {
            return new DataApiBuilderGenerateResponse
            {
                Success = false,
                ErrorMessage = "No connection is configured. Apply settings and connect before viewing a Data API config."
            };
        }

        GeneratedDabConfig? generated = null;
        try
        {
            var options = DataApiOptions.From(settings, request.RestEnabled, request.GraphQLEnabled, request.McpEnabled);
            generated = await GenerateConfigFileAsync(settings, metadata, request.Selections, options, cancellationToken).ConfigureAwait(false);
            var configJson = await File.ReadAllTextAsync(generated.ConfigPath, cancellationToken).ConfigureAwait(false);

            return new DataApiBuilderGenerateResponse
            {
                Success = true,
                ConfigJson = configJson,
                Diagnostics = ScrubSecrets(generated.Diagnostics.ToString(), settings),
                FileName = BuildFileName(settings)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data API config generation failed");
            return new DataApiBuilderGenerateResponse
            {
                Success = false,
                ErrorMessage = ScrubSecrets(ex.Message, settings),
                Diagnostics = generated is null ? null : ScrubSecrets(generated.Diagnostics.ToString(), settings)
            };
        }
        finally
        {
            if (generated is not null)
            {
                TryDeleteDirectory(generated.WorkDirectory);
            }
        }
    }

    public async Task<DataApiRuntimeResponse> StartAsync(
        AppSettings settings,
        DatabaseMetadata metadata,
        DataApiBuilderStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(settings.Server))
        {
            return new DataApiRuntimeResponse
            {
                Success = false,
                State = "failed",
                ErrorMessage = "No connection is configured. Apply settings and connect before starting Data API."
            };
        }

        var existing = GetStatus();
        if (existing.Running)
        {
            return existing with
            {
                Success = true,
                Diagnostics = AppendDiagnostic(existing.Diagnostics, "info: Data API is already running.")
            };
        }

        GeneratedDabConfig? generated = null;
        try
        {
            var options = DataApiOptions.From(settings, request.RestEnabled, request.GraphQLEnabled, request.McpEnabled);
            generated = await GenerateConfigFileAsync(settings, metadata, request.Selections, options, cancellationToken).ConfigureAwait(false);
            var diagnostics = generated.Diagnostics;
            var baseUrl = BuildBaseUrl(options.Port);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dab",
                WorkingDirectory = generated.WorkDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("start");
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(generated.ConfigPath);
            startInfo.ArgumentList.Add("--no-https-redirect");
            startInfo.ArgumentList.Add("--LogLevel");
            startInfo.ArgumentList.Add("Error");
            startInfo.Environment[DabConnectionStringEnvironmentVariable] = settings.ToConnectionString(includeCommandTimeout: false);
            startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
            startInfo.Environment["DOTNET_URLS"] = baseUrl;

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, eventArgs) => AppendProcessOutput(diagnostics, eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => AppendProcessOutput(diagnostics, eventArgs.Data);

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("The Data API CLI could not be started.");
                }
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            {
                process.Dispose();
                throw new InvalidOperationException("The Data API CLI was not found. Install Microsoft.DataApiBuilder or use the SQL Commander container image, which includes it.", ex);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var runtime = new RunningDataApi(
                process,
                generated.WorkDirectory,
                generated.ConfigPath,
                BuildFileName(settings),
                options.Port,
                baseUrl,
                diagnostics,
                settings.Password,
                DateTimeOffset.UtcNow);

            lock (_runtimeLock)
            {
                _runtime = runtime;
                _lastRuntimeStatus = null;
            }

            try
            {
                await WaitForRuntimeReadyAsync(runtime, TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            if (process.HasExited)
            {
                var status = BuildStatus(runtime, process.ExitCode == 0 ? "stopped" : "failed", success: process.ExitCode == 0) with
                {
                    ErrorMessage = process.ExitCode == 0 ? null : $"Data API exited immediately with code {process.ExitCode}."
                };
                RememberRuntimeStatus(status);
                ClearRuntime(runtime);
                return status;
            }

            generated = null;
            return BuildStatus(runtime, "running", success: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data API start failed");
            if (generated is not null)
            {
                TryDeleteDirectory(generated.WorkDirectory);
            }

            var baseUrl = BuildBaseUrl(DefaultDataApiPort);
            var response = new DataApiRuntimeResponse
            {
                Success = false,
                Running = false,
                State = "failed",
                BaseUrl = baseUrl,
                HealthUrl = CombineUrl(baseUrl, "health"),
                SwaggerUrl = CombineUrl(baseUrl, "swagger"),
                NitroUrl = CombineUrl(baseUrl, "graphql"),
                ErrorMessage = ScrubSecrets(ex.Message, settings),
                Diagnostics = generated is null ? null : ScrubSecrets(generated.Diagnostics.ToString(), settings)
            };
            RememberRuntimeStatus(response);
            return response;
        }
    }

    public async Task<DataApiRuntimeResponse> StopAsync(CancellationToken cancellationToken = default)
    {
        RunningDataApi? runtime;
        lock (_runtimeLock)
        {
            runtime = _runtime;
            _runtime = null;
        }

        if (runtime is null)
        {
            return new DataApiRuntimeResponse
            {
                Success = true,
                State = "stopped",
                Running = false
            };
        }

        try
        {
            if (!runtime.Process.HasExited)
            {
                AppendProcessOutput(runtime.Diagnostics, "info: Stopping Data API...");
                try
                {
                    runtime.Process.CloseMainWindow();
                }
                catch
                {
                    // No window is expected; this is a best-effort graceful stop.
                }

                var waitTask = runtime.Process.WaitForExitAsync(cancellationToken);
                var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken)).ConfigureAwait(false);
                if (completed != waitTask && !runtime.Process.HasExited)
                {
                    runtime.Process.Kill(entireProcessTree: true);
                    await runtime.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            var status = BuildStatus(runtime, "stopped", success: true) with { Running = false };
            RememberRuntimeStatus(null);
            return status;
        }
        finally
        {
            runtime.Process.Dispose();
            TryDeleteDirectory(runtime.WorkDirectory);
        }
    }

    public DataApiRuntimeResponse GetStatus()
    {
        RunningDataApi? runtime;
        DataApiRuntimeResponse? lastRuntimeStatus;
        lock (_runtimeLock)
        {
            runtime = _runtime;
            lastRuntimeStatus = _lastRuntimeStatus;
        }

        if (runtime is null)
        {
            return lastRuntimeStatus ?? new DataApiRuntimeResponse
            {
                Success = true,
                State = "stopped",
                Running = false
            };
        }

        if (runtime.Process.HasExited)
        {
            var state = runtime.Process.ExitCode == 0 ? "stopped" : "failed";
            var response = BuildStatus(runtime, state, success: runtime.Process.ExitCode == 0) with
            {
                Running = false,
                ErrorMessage = runtime.Process.ExitCode == 0 ? null : $"Data API exited with code {runtime.Process.ExitCode}."
            };
            RememberRuntimeStatus(response);
            ClearRuntime(runtime);
            return response;
        }

        return BuildStatus(runtime, "running", success: true);
    }

    async Task<GeneratedDabConfig> GenerateConfigFileAsync(
        AppSettings settings,
        DatabaseMetadata metadata,
        IReadOnlyList<DataApiBuilderSelection> selections,
        DataApiOptions options,
        CancellationToken cancellationToken)
    {
        var selected = BuildSelectedObjects(metadata, selections);
        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Select at least one table, view, or stored procedure.");
        }

        ValidateSelectedObjects(selected);

        var workDirectory = Path.Combine(Path.GetTempPath(), "sqlcmdr-dab", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var configPath = Path.Combine(workDirectory, "dab-config.generated.json");
        var diagnostics = new StringBuilder();

        try
        {
            var connectionStringReference = $"@env('{DabConnectionStringEnvironmentVariable}')";
            diagnostics.AppendLine($"info: Connection string is referenced as {connectionStringReference}. SQL Commander sets {DabConnectionStringEnvironmentVariable} when Data API starts.");

            await RunDabAsync(workDirectory, diagnostics, cancellationToken,
                "init",
                "--database-type", "mssql",
                "--connection-string", connectionStringReference,
                "--host-mode", "Development",
                "--auth.provider", "Unauthenticated",
                "--cors-origin", "*",
                "--rest.enabled", ToCliBool(options.RestEnabled),
                "--graphql.enabled", ToCliBool(options.GraphQLEnabled),
                "--mcp.enabled", ToCliBool(options.McpEnabled),
                "--config", configPath).ConfigureAwait(false);

            var explicitEntities = selected;
            var entityNames = BuildEntityNameMap(explicitEntities);
            foreach (var item in explicitEntities)
            {
                var entityName = entityNames[item.Key];
                await AddEntityAsync(workDirectory, configPath, diagnostics, item, entityName, options, cancellationToken).ConfigureAwait(false);
                await AddMetadataAsync(workDirectory, configPath, diagnostics, item, entityName, cancellationToken).ConfigureAwait(false);
            }

            await AddRelationshipsAsync(workDirectory, configPath, diagnostics, metadata, explicitEntities, entityNames, cancellationToken).ConfigureAwait(false);
            await RemoveEmptyAutoEntitiesAsync(configPath, diagnostics, cancellationToken).ConfigureAwait(false);

            return new GeneratedDabConfig(workDirectory, configPath, diagnostics);
        }
        catch
        {
            TryDeleteDirectory(workDirectory);
            throw;
        }
    }

    static List<SelectedDabObject> BuildSelectedObjects(DatabaseMetadata metadata, IReadOnlyList<DataApiBuilderSelection> selections)
    {
        var requestedSelections = selections
            .Where(static selection => !string.IsNullOrWhiteSpace(selection.Type) && !string.IsNullOrWhiteSpace(selection.Schema) && !string.IsNullOrWhiteSpace(selection.Name))
            .ToDictionary(static selection => BuildKey(selection.Type, selection.Schema, selection.Name), static selection => selection, StringComparer.OrdinalIgnoreCase);

        var includeAll = requestedSelections.Count == 0;
        var items = new List<SelectedDabObject>();

        items.AddRange(metadata.Tables
            .Where(table => includeAll || requestedSelections.ContainsKey(BuildKey("table", table.Schema, table.Name)))
            .Select(static table => SelectedDabObject.FromTable(table)));
        items.AddRange(metadata.Views
            .Where(view => includeAll || requestedSelections.ContainsKey(BuildKey("view", view.Schema, view.Name)))
            .Select(view => SelectedDabObject.FromView(view, requestedSelections.GetValueOrDefault(BuildKey("view", view.Schema, view.Name))?.KeyFields ?? [])));
        items.AddRange(metadata.StoredProcedures
            .Where(procedure => includeAll || requestedSelections.ContainsKey(BuildKey("proc", procedure.Schema, procedure.Name)))
            .Select(static procedure => SelectedDabObject.FromStoredProcedure(procedure)));

        return items;
    }

    static void ValidateSelectedObjects(IReadOnlyList<SelectedDabObject> selected)
    {
        var unsupportedObjects = selected
            .Select(static item => new
            {
                ObjectName = $"{item.Schema}.{item.Name}",
                Types = GetUnsupportedDataTypes(item).ToList()
            })
            .Where(static item => item.Types.Count > 0)
            .Select(static item => $"{item.ObjectName} ({string.Join(", ", item.Types)})")
            .ToList();

        if (unsupportedObjects.Count > 0)
        {
            throw new InvalidOperationException($"Data API Builder does not support one or more selected object data types. Deselect or change: {string.Join(", ", unsupportedObjects)}.");
        }

        var keylessTables = selected
            .Where(static item => item.Kind == DabObjectKind.Table && !item.Columns.Any(static column => column.IsPrimaryKey))
            .Select(static item => $"{item.Schema}.{item.Name}")
            .ToList();

        if (keylessTables.Count > 0)
        {
            throw new InvalidOperationException($"Data API Builder requires selected tables to have a primary key. Add a primary key or deselect: {string.Join(", ", keylessTables)}.");
        }

        var keylessViews = selected
            .Where(static item => item.Kind == DabObjectKind.View && !item.KeyFields.Any())
            .Select(static item => $"{item.Schema}.{item.Name}")
            .ToList();

        if (keylessViews.Count > 0)
        {
            throw new InvalidOperationException($"Data API Builder requires selected views to have one or more key fields. Select key fields or deselect: {string.Join(", ", keylessViews)}.");
        }
    }

    static IEnumerable<string> GetUnsupportedDataTypes(SelectedDabObject item)
    {
        var unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "geography",
            "geometry",
            "hierarchyid",
            "json",
            "rowversion",
            "timestamp",
            "sql_variant",
            "vector",
            "xml"
        };

        return item.Columns
            .Select(static column => column.DataType)
            .Concat(item.Parameters.Select(static parameter => parameter.DataType))
            .Where(unsupported.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    static Dictionary<string, string> BuildEntityNameMap(IEnumerable<SelectedDabObject> selected)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in selected)
        {
            var baseName = string.Equals(item.Schema, "dbo", StringComparison.OrdinalIgnoreCase)
                ? ToIdentifier(item.Name)
                : ToIdentifier($"{item.Schema}_{item.Name}");

            var entityName = string.IsNullOrWhiteSpace(baseName) ? "Entity" : baseName;
            var suffix = 2;
            while (!used.Add(entityName))
            {
                entityName = $"{baseName}{suffix++}";
            }

            map[item.Key] = entityName;
        }

        return map;
    }

    async Task AddEntityAsync(
        string workDirectory,
        string configPath,
        StringBuilder diagnostics,
        SelectedDabObject item,
        string entityName,
        DataApiOptions options,
        CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "add", entityName,
            "--source", QuoteSqlObjectName(item.Schema, item.Name),
            "--source.type", item.SourceType,
            "--permissions", item.Permission,
            "--rest", ToCliBool(options.RestEnabled),
            "--graphql", ToCliBool(options.GraphQLEnabled),
            "--description", BuildEntityDescription(item),
            "--config", configPath
        };

        if (item.Kind == DabObjectKind.View)
        {
            var keyFields = GetKeyFields(item).ToList();
            if (keyFields.Count > 0)
            {
                args.Add("--source.key-fields");
                args.Add(string.Join(',', keyFields));
            }
        }

        if (item.Kind == DabObjectKind.StoredProcedure)
        {
            args.Add("--graphql.operation");
            args.Add("query");
            if (options.McpEnabled)
            {
                args.Add("--mcp.custom-tool");
                args.Add("true");
            }
        }
        else
        {
            args.Add("--mcp.dml-tools");
            args.Add(ToCliBool(options.McpEnabled));
        }

        var inputParameters = GetInputParameters(item).ToList();
        if (inputParameters.Count > 0)
        {
            args.Add("--parameters.name");
            args.Add(string.Join(',', inputParameters.Select(static parameter => NormalizeParameterName(parameter.Name))));
            args.Add("--parameters.description");
            args.Add(string.Join(',', inputParameters.Select(parameter => BuildParameterDescription(item, parameter))));
            args.Add("--parameters.required");
            args.Add(string.Join(',', inputParameters.Select(static _ => "true")));
        }

        await RunDabAsync(workDirectory, diagnostics, cancellationToken, args.ToArray()).ConfigureAwait(false);
    }

    async Task AddMetadataAsync(
        string workDirectory,
        string configPath,
        StringBuilder diagnostics,
        SelectedDabObject item,
        string entityName,
        CancellationToken cancellationToken)
    {
        if (item.Kind is DabObjectKind.Table or DabObjectKind.View && item.Columns.Count > 0)
        {
            var keyFields = GetKeyFields(item).ToHashSet(StringComparer.OrdinalIgnoreCase);
            await RunDabAsync(workDirectory, diagnostics, cancellationToken,
                "update", entityName,
                "--fields.name", string.Join(',', item.Columns.Select(static column => column.Name)),
                "--fields.description", string.Join(',', item.Columns.Select(column => BuildFieldDescription(item, column, keyFields.Contains(column.Name)))),
                "--fields.primary-key", string.Join(',', item.Columns.Select(column => ToCliBool(column.IsPrimaryKey || keyFields.Contains(column.Name)))),
                "--config", configPath).ConfigureAwait(false);
        }

        if (item.Kind == DabObjectKind.StoredProcedure)
        {
            var inputParameters = GetInputParameters(item).ToList();
            if (inputParameters.Count == 0)
            {
                return;
            }

            await RunDabAsync(workDirectory, diagnostics, cancellationToken,
                "update", entityName,
                "--parameters.name", string.Join(',', inputParameters.Select(static parameter => NormalizeParameterName(parameter.Name))),
                "--parameters.description", string.Join(',', inputParameters.Select(parameter => BuildParameterDescription(item, parameter))),
                "--parameters.required", string.Join(',', inputParameters.Select(static _ => "true")),
                "--config", configPath).ConfigureAwait(false);
        }
    }

    async Task AddRelationshipsAsync(
        string workDirectory,
        string configPath,
        StringBuilder diagnostics,
        DatabaseMetadata metadata,
        IReadOnlyCollection<SelectedDabObject> selected,
        IReadOnlyDictionary<string, string> entityNames,
        CancellationToken cancellationToken)
    {
        var selectedTableKeys = selected
            .Where(static item => item.Kind == DabObjectKind.Table)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usedRelationshipNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var foreignKeyGroups = metadata.ForeignKeys
            .GroupBy(static fk => new { fk.Name, fk.ParentSchema, fk.ParentTable, fk.ReferencedSchema, fk.ReferencedTable })
            .OrderBy(static group => group.Key.ParentSchema)
            .ThenBy(static group => group.Key.ParentTable)
            .ThenBy(static group => group.Key.Name);

        foreach (var group in foreignKeyGroups)
        {
            var childKey = BuildKey("table", group.Key.ParentSchema, group.Key.ParentTable);
            var parentKey = BuildKey("table", group.Key.ReferencedSchema, group.Key.ReferencedTable);
            if (!selectedTableKeys.Contains(childKey) || !selectedTableKeys.Contains(parentKey))
            {
                continue;
            }

            var childEntity = entityNames[childKey];
            var parentEntity = entityNames[parentKey];
            var orderedColumns = group.OrderBy(static fk => fk.ConstraintColumnId).ToList();
            var childToParentFields = string.Join(',', orderedColumns.Select(static fk => $"{fk.ParentColumn}:{fk.ReferencedColumn}"));
            var parentToChildrenFields = string.Join(',', orderedColumns.Select(static fk => $"{fk.ReferencedColumn}:{fk.ParentColumn}"));

            await TryAddRelationshipAsync(workDirectory, configPath, diagnostics, childEntity,
                UniqueRelationshipName(usedRelationshipNames, childEntity, parentEntity), parentEntity, "one", childToParentFields, cancellationToken).ConfigureAwait(false);

            await TryAddRelationshipAsync(workDirectory, configPath, diagnostics, parentEntity,
                UniqueRelationshipName(usedRelationshipNames, parentEntity, ToPluralRelationshipName(childEntity)), childEntity, "many", parentToChildrenFields, cancellationToken).ConfigureAwait(false);
        }
    }

    static async Task RemoveEmptyAutoEntitiesAsync(string configPath, StringBuilder diagnostics, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        var root = JsonNode.Parse(json) as JsonObject;
        if (root is null)
        {
            return;
        }

        if (root["autoentities"] is JsonObject autoEntities && autoEntities.Count == 0)
        {
            root.Remove("autoentities");
            await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
            diagnostics.AppendLine("info: Removed empty autoentities section from generated config.");
        }
    }

    async Task TryAddRelationshipAsync(
        string workDirectory,
        string configPath,
        StringBuilder diagnostics,
        string sourceEntity,
        string relationshipName,
        string targetEntity,
        string cardinality,
        string relationshipFields,
        CancellationToken cancellationToken)
    {
        try
        {
            await AddRelationshipAsync(workDirectory, configPath, diagnostics, sourceEntity,
                relationshipName, targetEntity, cardinality, relationshipFields, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            diagnostics.AppendLine($"warn: Skipped relationship {sourceEntity}.{relationshipName} -> {targetEntity}: {ex.Message}");
        }
    }

    async Task AddRelationshipAsync(
        string workDirectory,
        string configPath,
        StringBuilder diagnostics,
        string sourceEntity,
        string relationshipName,
        string targetEntity,
        string cardinality,
        string relationshipFields,
        CancellationToken cancellationToken)
    {
        await RunDabAsync(workDirectory, diagnostics, cancellationToken,
            "update", sourceEntity,
            "--relationship", relationshipName,
            "--target.entity", targetEntity,
            "--cardinality", cardinality,
            "--relationship.fields", relationshipFields,
            "--config", configPath).ConfigureAwait(false);
    }

    Task RunDabAsync(string workingDirectory, StringBuilder diagnostics, CancellationToken cancellationToken, params string[] arguments)
    {
        return _runDabAsync(workingDirectory, diagnostics, cancellationToken, arguments);
    }

    static async Task RunDabProcessAsync(string workingDirectory, StringBuilder diagnostics, CancellationToken cancellationToken, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dab",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The Data API CLI could not be started.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("The Data API CLI was not found. Install Microsoft.DataApiBuilder or use the SQL Commander container image, which includes it.", ex);
        }

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(output))
            {
                diagnostics.AppendLine(output.Trim());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                diagnostics.AppendLine(error.Trim());
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dab {arguments.FirstOrDefault()} failed with exit code {process.ExitCode}: {error}{output}");
            }
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    DataApiRuntimeResponse BuildStatus(RunningDataApi runtime, string state, bool success)
    {
        var running = state == "running";
        var diagnostics = ScrubSecrets(GetDiagnostics(runtime.Diagnostics), runtime.SecretToScrub);
        return new DataApiRuntimeResponse
        {
            Success = success,
            State = state,
            Running = running,
            BaseUrl = runtime.BaseUrl,
            HealthUrl = CombineUrl(runtime.BaseUrl, "health"),
            SwaggerUrl = CombineUrl(runtime.BaseUrl, "swagger"),
            NitroUrl = CombineUrl(runtime.BaseUrl, "graphql"),
            ConfigFileName = runtime.ConfigFileName,
            Diagnostics = diagnostics,
            StartedAt = runtime.StartedAt
        };
    }

    static async Task WaitForRuntimeReadyAsync(RunningDataApi runtime, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var healthUrl = CombineUrl(runtime.BaseUrl, "health");
        var stopAt = DateTimeOffset.UtcNow.Add(timeout);

        while (!runtime.Process.HasExited && DateTimeOffset.UtcNow < stopAt)
        {
            try
            {
                using var response = await httpClient.GetAsync(healthUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // The runtime may still be binding its listener; keep waiting until it exits or the startup window expires.
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    void ClearRuntime(RunningDataApi runtime)
    {
        lock (_runtimeLock)
        {
            if (ReferenceEquals(_runtime, runtime))
            {
                _runtime = null;
            }
        }

        runtime.Process.Dispose();
        TryDeleteDirectory(runtime.WorkDirectory);
    }

    void RememberRuntimeStatus(DataApiRuntimeResponse? status)
    {
        lock (_runtimeLock)
        {
            _lastRuntimeStatus = status;
        }
    }

    static IEnumerable<string> GetKeyFields(SelectedDabObject item)
    {
        return item.KeyFields;
    }

    static IEnumerable<ParameterMetadata> GetInputParameters(SelectedDabObject item)
    {
        return item.Parameters.Where(static parameter => !string.Equals(parameter.Direction, "Output", StringComparison.OrdinalIgnoreCase));
    }

    static string BuildEntityDescription(SelectedDabObject item)
    {
        var kind = item.Kind switch
        {
            DabObjectKind.StoredProcedure => "stored procedure",
            DabObjectKind.View => "view",
            _ => "table"
        };
        return SanitizeCliListValue($"Exposes SQL {kind} {item.Schema}.{item.Name} through Data API.");
    }

    static string BuildFieldDescription(SelectedDabObject item, ColumnMetadata column, bool isKey)
    {
        var keyText = isKey ? " Primary key." : string.Empty;
        var nullText = column.IsNullable ? " Nullable." : " Required.";
        return SanitizeCliListValue($"Column {column.Name} from {item.Schema}.{item.Name} with SQL type {column.DisplayType}.{keyText}{nullText}");
    }

    static string BuildParameterDescription(SelectedDabObject item, ParameterMetadata parameter)
    {
        var parameterName = NormalizeParameterName(parameter.Name);
        return SanitizeCliListValue($"Parameter {parameterName} for stored procedure {item.Schema}.{item.Name} with SQL type {parameter.DisplayType}. Direction {parameter.Direction}.");
    }

    static string SanitizeCliListValue(string value)
    {
        return value.Replace(',', ';').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    static string NormalizeParameterName(string name)
    {
        return name.Trim().TrimStart('@');
    }

    static string QuoteSqlObjectName(string schema, string name)
    {
        return $"[{schema.Replace("]", "]]", StringComparison.Ordinal)}].[{name.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    static string UniqueRelationshipName(IDictionary<string, HashSet<string>> used, string entityName, string candidate)
    {
        if (!used.TryGetValue(entityName, out var names))
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            used[entityName] = names;
        }

        var baseName = ToCamelCase(candidate);
        var relationshipName = baseName;
        var suffix = 2;
        while (!names.Add(relationshipName))
        {
            relationshipName = $"{baseName}{suffix++}";
        }

        return relationshipName;
    }

    static string ToPluralRelationshipName(string entityName)
    {
        if (entityName.EndsWith("y", StringComparison.OrdinalIgnoreCase)) return entityName[..^1] + "ies";
        if (entityName.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return entityName;
        return entityName + "s";
    }

    static string ToIdentifier(string value)
    {
        var words = value.Split(['_', '-', ' ', '.', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = string.Concat(words.Select(static word => char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : string.Empty)));
        candidate = new string(candidate.Where(static ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        if (string.IsNullOrWhiteSpace(candidate)) return "Entity";
        return char.IsLetter(candidate[0]) || candidate[0] == '_' ? candidate : $"Entity{candidate}";
    }

    static string ToCamelCase(string value)
    {
        var identifier = ToIdentifier(value);
        return string.IsNullOrWhiteSpace(identifier) ? "relationship" : char.ToLowerInvariant(identifier[0]) + identifier[1..];
    }

    static string BuildKey(string type, string schema, string name) => $"{type}:{schema}.{name}";

    static string BuildFileName(AppSettings settings)
    {
        var database = string.IsNullOrWhiteSpace(settings.Database) ? "database" : settings.Database;
        var safeName = new string(database.Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray()).Trim('-');
        return $"data-api.{(string.IsNullOrWhiteSpace(safeName) ? "database" : safeName)}.json";
    }

    static string BuildBaseUrl(int port) => $"http://127.0.0.1:{port}";

    static string CombineUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    static string ToCliBool(bool value) => value ? "true" : "false";

    static string ScrubSecrets(string value, AppSettings settings) => ScrubSecrets(value, settings.Password);

    static string ScrubSecrets(string value, string? secret)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(secret))
        {
            return value;
        }

        return value.Replace(secret, "********", StringComparison.Ordinal);
    }

    static string AppendDiagnostic(string? existing, string message)
    {
        return string.IsNullOrWhiteSpace(existing) ? message : $"{existing.TrimEnd()}{Environment.NewLine}{message}";
    }

    static void AppendProcessOutput(StringBuilder diagnostics, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        lock (diagnostics)
        {
            diagnostics.AppendLine(value.Trim());
        }
    }

    static string GetDiagnostics(StringBuilder diagnostics)
    {
        lock (diagnostics)
        {
            return diagnostics.ToString();
        }
    }

    static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only. The directory lives under the OS temp path.
        }
    }

    sealed record GeneratedDabConfig(string WorkDirectory, string ConfigPath, StringBuilder Diagnostics);

    sealed record DataApiOptions(int Port, bool RestEnabled, bool GraphQLEnabled, bool McpEnabled)
    {
        public static DataApiOptions From(AppSettings settings, bool? restEnabled, bool? graphQLEnabled, bool? mcpEnabled)
        {
            return new DataApiOptions(
                DefaultDataApiPort,
                restEnabled ?? settings.DataApiRestEnabled,
                graphQLEnabled ?? settings.DataApiGraphQLEnabled,
                mcpEnabled ?? settings.DataApiMcpEnabled);
        }
    }

    sealed record RunningDataApi(
        Process Process,
        string WorkDirectory,
        string ConfigPath,
        string ConfigFileName,
        int Port,
        string BaseUrl,
        StringBuilder Diagnostics,
        string SecretToScrub,
        DateTimeOffset StartedAt);

    sealed record SelectedDabObject(
        string Key,
        DabObjectKind Kind,
        string Schema,
        string Name,
        IReadOnlyList<ColumnMetadata> Columns,
        IReadOnlyList<ParameterMetadata> Parameters,
        IReadOnlyList<string> KeyFields)
    {
        public string SourceType => Kind switch
        {
            DabObjectKind.View => "view",
            DabObjectKind.StoredProcedure => "stored-procedure",
            _ => "table"
        };

        public string Permission => Kind == DabObjectKind.StoredProcedure ? "anonymous:execute" : Kind == DabObjectKind.View ? "anonymous:read" : "anonymous:*";

        public static SelectedDabObject FromTable(TableMetadata table) => new(BuildKey("table", table.Schema, table.Name), DabObjectKind.Table, table.Schema, table.Name, table.Columns, [], table.Columns.Where(static column => column.IsPrimaryKey).Select(static column => column.Name).ToArray());
        public static SelectedDabObject FromView(ViewMetadata view, IReadOnlyList<string> requestedKeyFields)
        {
            var availableColumns = view.Columns.Select(static column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keyFields = requestedKeyFields
                .Where(field => availableColumns.Contains(field))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (keyFields.Length == 0)
            {
                keyFields = view.Columns.Where(static column => column.IsPrimaryKey).Select(static column => column.Name).ToArray();
            }

            return new SelectedDabObject(BuildKey("view", view.Schema, view.Name), DabObjectKind.View, view.Schema, view.Name, view.Columns, [], keyFields);
        }
        public static SelectedDabObject FromStoredProcedure(StoredProcedureMetadata procedure) => new(BuildKey("proc", procedure.Schema, procedure.Name), DabObjectKind.StoredProcedure, procedure.Schema, procedure.Name, [], procedure.Parameters, []);
    }

    enum DabObjectKind
    {
        Table,
        View,
        StoredProcedure
    }
}
