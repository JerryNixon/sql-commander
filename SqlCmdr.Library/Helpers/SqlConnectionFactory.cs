using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using SqlCmdr.Models;

namespace SqlCmdr.Helpers;

/// <summary>
/// Factory for creating SQL connections with support for both SQL Authentication and Azure Default Credential.
/// </summary>
public static class SqlConnectionFactory
{
    private static readonly DefaultAzureCredential _azureCredential = new();
    private const string AzureSqlScope = "https://database.windows.net/.default";

    /// <summary>
    /// Creates and opens a SQL connection using the specified settings and authentication type.
    /// </summary>
    /// <param name="settings">Application settings containing connection information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An open SqlConnection</returns>
    public static async Task<SqlConnection> CreateAndOpenConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var connectionString = settings.ToConnectionString();
        var connection = new SqlConnection(connectionString);

        try
        {
            // For Azure Default Credential, acquire and set access token
            if (settings.AuthenticationType == AuthenticationType.AzureDefaultCredential)
            {
                var tokenRequestContext = new TokenRequestContext(new[] { AzureSqlScope });
                var accessToken = await _azureCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
                connection.AccessToken = accessToken.Token;
            }

            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates and opens a SQL connection using a connection string and authentication type.
    /// </summary>
    /// <param name="connectionString">SQL connection string</param>
    /// <param name="authenticationType">Type of authentication to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An open SqlConnection</returns>
    public static async Task<SqlConnection> CreateAndOpenConnectionAsync(
        string connectionString,
        AuthenticationType authenticationType,
        CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            // For Azure Default Credential, acquire and set access token
            if (authenticationType == AuthenticationType.AzureDefaultCredential)
            {
                var tokenRequestContext = new TokenRequestContext(new[] { AzureSqlScope });
                var accessToken = await _azureCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
                connection.AccessToken = accessToken.Token;
            }

            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
