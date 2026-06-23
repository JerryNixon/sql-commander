using SqlCmdr.Models;

namespace SqlCmdr.Abstractions;

public interface IDataApiBuilderService
{
    Task<DataApiBuilderGenerateResponse> GenerateConfigAsync(
        AppSettings settings,
        DatabaseMetadata metadata,
        DataApiBuilderGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<DataApiRuntimeResponse> StartAsync(
        AppSettings settings,
        DatabaseMetadata metadata,
        DataApiBuilderStartRequest request,
        CancellationToken cancellationToken = default);

    Task<DataApiRuntimeResponse> StopAsync(CancellationToken cancellationToken = default);

    DataApiRuntimeResponse GetStatus();
}
