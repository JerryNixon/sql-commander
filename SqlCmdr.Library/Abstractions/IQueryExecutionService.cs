using SqlCmdr.Models;

namespace SqlCmdr.Abstractions;

public interface IQueryExecutionService
{
    Task<QueryResponse> ExecuteQueryAsync(string connectionString, QueryRequest request, CancellationToken cancellationToken = default);
    void CancelCurrentQuery();
}
