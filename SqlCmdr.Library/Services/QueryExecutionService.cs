using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;
using SqlCmdr.Infrastructure;
using SqlCmdr.Models;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace SqlCmdr.Services;

public class QueryExecutionService : IQueryExecutionService
{
    readonly ISqlConnectionFactory _connectionFactory;
    readonly ILogger<QueryExecutionService> _logger;
    SqlCommand? _currentCommand;
    readonly object _commandLock = new();

    public QueryExecutionService(ILogger<QueryExecutionService> logger, ISqlConnectionFactory connectionFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public void CancelCurrentQuery()
    {
        lock (_commandLock)
        {
            try
            {
                _currentCommand?.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error cancelling query");
            }
        }
    }

    public async Task<QueryResponse> ExecuteQueryAsync(string connectionString, QueryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        var settings = AppSettings.FromConnectionString(connectionString);
        return await ExecuteQueryAsync(settings, request, cancellationToken);
    }

    public async Task<QueryResponse> ExecuteQueryAsync(AppSettings settings, QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();
        var response = new QueryResponse();
        var totalRows = 0;
        var wasTruncated = false;
        var resultLimit = Math.Max(1, request.ResultLimit ?? 100);

        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(settings, cancellationToken).ConfigureAwait(false);
            connection.InfoMessage += (sender, e) =>
            {
                foreach (SqlError error in e.Errors)
                {
                    response.MessagesInternal.Add(error.Message);
                }
            };

            SqlCommand command;
            lock (_commandLock)
            {
                command = new SqlCommand(request.Sql, connection) { CommandTimeout = Math.Max(0, settings.CommandTimeout) };
                _currentCommand = command;
            }

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                do
                {
                    var resultSet = new ResultSet();
                    resultSet.ColumnsInternal.AddRange(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));

                    var rowCount = 0;
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (rowCount == resultLimit)
                        {
                            wasTruncated = true;
                            break;
                        }

                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[resultSet.Columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        resultSet.RowsInternal.Add(row);
                        rowCount++;
                        totalRows++;
                    }

                    response.ResultSetsInternal.Add(resultSet with { RowCount = rowCount });
                } while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
            }
            finally
            {
                lock (_commandLock)
                {
                    _currentCommand = null;
                }
            }

            stopwatch.Stop();
            return response with 
            { 
                Success = true, 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds, 
                TotalRowsReturned = totalRows, 
                WasTruncated = wasTruncated 
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogDebug("Query was cancelled");
            return response with 
            { 
                Success = false, 
                ErrorMessage = "Query was cancelled", 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds 
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Query execution failed");
            return response with 
            { 
                Success = false, 
                ErrorMessage = ex.Message, 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds 
            };
        }
    }
}
