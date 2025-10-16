using Microsoft.Extensions.DependencyInjection;
using SqlCmdr.Abstractions;
using SqlCmdr.Services;

namespace SqlCmdr.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlCmdr(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddScoped<IQueryExecutionService, QueryExecutionService>();
        return services;
    }
}
