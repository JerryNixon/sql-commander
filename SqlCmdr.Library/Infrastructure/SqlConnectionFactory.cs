using Azure.Core;
using Microsoft.Data.SqlClient;
using SqlCmdr.Models;
using System.Collections.Concurrent;

namespace SqlCmdr.Infrastructure;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateOpenConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    const string AzureSqlScope = "https://database.windows.net/.default";
    readonly TokenCredential _tokenCredential;
    readonly Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> _accessTokenCallback;
    static readonly ConcurrentDictionary<string, string> ScopeCache = new();

    public SqlConnectionFactory(TokenCredential tokenCredential)
    {
        _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        _accessTokenCallback = CreateAccessTokenCallback();
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var connection = new SqlConnection(settings.ToConnectionString());

        try
        {
            if (settings.AuthenticationType == AuthenticationType.AzureDefaultCredential)
            {
                connection.AccessTokenCallback = _accessTokenCallback;
            }

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> CreateAccessTokenCallback()
    {
        return async (parameters, token) =>
        {
            var resource = parameters.Resource;
            if (string.IsNullOrEmpty(resource))
            {
                resource = AzureSqlScope;
            }

            var scope = ScopeCache.GetOrAdd(resource, static key =>
                key.EndsWith("/.default", StringComparison.OrdinalIgnoreCase) ? key : string.Concat(key, "/.default"));

            var credentialToken = await _tokenCredential
                .GetTokenAsync(new TokenRequestContext([scope]), token)
                .ConfigureAwait(false);

            return new SqlAuthenticationToken(credentialToken.Token, credentialToken.ExpiresOn);
        };
    }
}