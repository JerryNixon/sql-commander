using SqlCmdr.Library.Models;

namespace SqlCmdr.Library.Abstractions;

public interface IMetadataService
{
    Task<DatabaseMetadata> GetMetadataAsync(string connectionString);
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString);
}
