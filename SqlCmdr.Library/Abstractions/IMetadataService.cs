using SqlCmdr.Models;

namespace SqlCmdr.Abstractions;

public interface IMetadataService
{
    Task<DatabaseMetadata> GetMetadataAsync(string connectionString);
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString);
}
