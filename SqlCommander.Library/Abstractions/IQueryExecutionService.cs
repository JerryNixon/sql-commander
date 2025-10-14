using SqlCommander.Library.Models;

namespace SqlCommander.Library.Abstractions;

public interface IQueryExecutionService
{
    Task<QueryResponse> ExecuteQueryAsync(string connectionString, QueryRequest request, CancellationToken cancellationToken = default);
    void CancelCurrentQuery();
}
