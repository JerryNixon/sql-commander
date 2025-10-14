using SqlCommander.Library.Models;

namespace SqlCommander.Library.Abstractions;

public interface IMetadataService
{
    Task<DatabaseMetadata> GetMetadataAsync(string connectionString);
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString);
}
