using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using SqlCmdr.Abstractions;
using SqlCmdr.Infrastructure;
using SqlCmdr.Services;

namespace SqlCmdr.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlCmdr(this IServiceCollection services)
    {
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMetadataService, MetadataService>();
        services.AddSingleton<IDataApiBuilderService, DataApiBuilderService>();
        services.AddScoped<IQueryExecutionService, QueryExecutionService>();
        return services;
    }
}
