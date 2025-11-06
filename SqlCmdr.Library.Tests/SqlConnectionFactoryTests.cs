using System;
using System.Linq;
using System.Reflection;
using Azure.Core;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Moq;
using SqlCmdr.Infrastructure;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "SqlConnectionFactory")]
public class SqlConnectionFactoryTests
{
    [Fact]
    public async Task AccessTokenCallback_WithNoResource_UsesAzureSqlDefaultScope()
    {
        // Arrange
        var token = new AccessToken("abc", DateTimeOffset.UtcNow.AddMinutes(5));
        TokenRequestContext capturedContext = default;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token)
            .Callback<TokenRequestContext, CancellationToken>((ctx, _) => capturedContext = ctx);

        var factory = new SqlConnectionFactory(credential.Object);
        var callback = GetAccessTokenCallback(factory);

        // Act
        var result = await callback(CreateAuthenticationParameters(null), CancellationToken.None);

        // Assert
        capturedContext.Scopes.Should().ContainSingle()
            .Which.Should().Be("https://database.windows.net/.default");
        result.AccessToken.Should().Be(token.Token);
    }

    [Fact]
    public async Task AccessTokenCallback_AppendsDefaultSuffix_WhenMissing()
    {
        // Arrange
        var token = new AccessToken("xyz", DateTimeOffset.UtcNow.AddMinutes(10));
        TokenRequestContext capturedContext = default;
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential
            .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token)
            .Callback<TokenRequestContext, CancellationToken>((ctx, _) => capturedContext = ctx);

        var factory = new SqlConnectionFactory(credential.Object);
        var callback = GetAccessTokenCallback(factory);

        var parameters = CreateAuthenticationParameters("https://custom.database.windows.net");

        // Act
        var result = await callback(parameters, CancellationToken.None);

        // Assert
        capturedContext.Scopes.Should().ContainSingle()
            .Which.Should().Be("https://custom.database.windows.net/.default");
        result.AccessToken.Should().Be(token.Token);
    }

    static SqlAuthenticationParameters CreateAuthenticationParameters(string? resource)
    {
        var ctor = typeof(SqlAuthenticationParameters)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("SqlAuthenticationParameters constructor not found.");

        var ctorParameters = ctor.GetParameters();
        var args = new object?[ctorParameters.Length];

        for (var i = 0; i < ctorParameters.Length; i++)
        {
            var parameter = ctorParameters[i];
            if (string.Equals(parameter.Name, "resource", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Name, "resourceUri", StringComparison.OrdinalIgnoreCase))
            {
                args[i] = resource;
                continue;
            }

            if (parameter.ParameterType == typeof(SqlAuthenticationMethod))
            {
                args[i] = SqlAuthenticationMethod.ActiveDirectoryDefault;
                continue;
            }

            if (parameter.ParameterType == typeof(string))
            {
                args[i] = string.Empty;
                continue;
            }

            if (parameter.ParameterType == typeof(Guid))
            {
                args[i] = Guid.Empty;
                continue;
            }

            if (parameter.ParameterType == typeof(int))
            {
                args[i] = 0;
                continue;
            }

            args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
        }

        return (SqlAuthenticationParameters)ctor.Invoke(args);
    }

    static Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> GetAccessTokenCallback(SqlConnectionFactory factory)
    {
        var field = typeof(SqlConnectionFactory)
            .GetField("_accessTokenCallback", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Access token callback field not found.");

        return (Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>>)field.GetValue(factory)!;
    }
}