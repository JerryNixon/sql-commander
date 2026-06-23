namespace SqlCmdr.Models;

public enum AuthenticationType
{
    SqlAuthentication,
    AzureDefaultCredential,
    AzureManagedIdentity
}

public record AppSettings
{
    public string Server { get; init; } = string.Empty;
    public string Database { get; init; } = "annalaura";
    public string UserId { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool RememberPassword { get; init; } = true;
    public int DefaultResultLimit { get; init; } = 100;
    public bool TrustServerCertificate { get; init; } = true;
    public int ConnectionTimeout { get; init; } = 30;
    public int CommandTimeout { get; init; } = 300;
    public bool ConfirmActions { get; init; } = false;
    public bool PromptWhenOverwritingEditorContent { get; init; } = false;
    public string Theme { get; init; } = "dark";
    public string Language { get; init; } = "en";
    public string Encrypt { get; init; } = "Mandatory";
    public bool Pooling { get; init; } = true;
    public bool MultipleActiveResultSets { get; init; } = false;
    public string ApplicationName { get; init; } = "SQL Commander";
    public string ConnectionName { get; init; } = string.Empty;
    public bool DataApiRestEnabled { get; init; } = true;
    public bool DataApiGraphQLEnabled { get; init; } = true;
    public bool DataApiMcpEnabled { get; init; } = true;
    public AuthenticationType AuthenticationType { get; init; } = AuthenticationType.SqlAuthentication;
    public string ConnectionString => ToConnectionString(includeCommandTimeout: true);

    public string ToConnectionString(bool includeCommandTimeout = false)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectionTimeout,
            MultipleActiveResultSets = MultipleActiveResultSets,
            Pooling = Pooling,
            ApplicationName = string.IsNullOrWhiteSpace(ApplicationName) ? "SQL Commander" : ApplicationName
        };

        builder["Encrypt"] = NormalizeEncrypt(Encrypt);

        switch (AuthenticationType)
        {
            case AuthenticationType.SqlAuthentication:
                builder.Authentication = Microsoft.Data.SqlClient.SqlAuthenticationMethod.SqlPassword;
                builder.UserID = UserId;
                builder.Password = Password;
                break;
            case AuthenticationType.AzureManagedIdentity:
                builder.Authentication = Microsoft.Data.SqlClient.SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
                builder.Remove("User ID");
                builder.Remove("Password");
                break;
            default:
                builder.Remove("Authentication");
                builder.Remove("User ID");
                builder.Remove("Password");
                break;
        }

        if (!includeCommandTimeout)
        {
            return builder.ConnectionString;
        }

        var displayBuilder = new System.Data.Common.DbConnectionStringBuilder
        {
            ConnectionString = builder.ConnectionString
        };
        displayBuilder["Command Timeout"] = Math.Max(0, CommandTimeout);
        return displayBuilder.ConnectionString;
    }

    public static AppSettings FromConnectionString(string connectionString, int defaultResultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new AppSettings { DefaultResultLimit = defaultResultLimit };
        var values = ParseConnectionStringValues(connectionString);
        var authentication = GetString(values, "Authentication", "Authentication Type");
        var userId = GetString(values, "User ID", "User Id", "UID", "User", "User Name", "Username");
        var password = GetString(values, "Password", "PWD");
        var hasSqlCredentials = !string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(password);

        var authType = InferAuthenticationType(authentication, hasSqlCredentials);

        return new AppSettings
        {
            Server = GetString(values, "Data Source", "Server", "Address", "Addr", "Network Address"),
            Database = GetString(values, "Initial Catalog", "Database"),
            UserId = authType == AuthenticationType.SqlAuthentication ? userId : string.Empty,
            Password = authType == AuthenticationType.SqlAuthentication ? password : string.Empty,
            DefaultResultLimit = defaultResultLimit,
            TrustServerCertificate = GetBool(values, true, "Trust Server Certificate", "TrustServerCertificate"),
            ConnectionTimeout = GetInt(values, 30, "Connect Timeout", "Connection Timeout", "Timeout"),
            CommandTimeout = GetInt(values, 300, "Command Timeout", "CommandTimeout"),
            AuthenticationType = authType,
            Encrypt = NormalizeEncrypt(GetString(values, "Encrypt", "Encryption")),
            Pooling = GetBool(values, true, "Pooling"),
            MultipleActiveResultSets = GetBool(values, false, "Multiple Active Result Sets", "MultipleActiveResultSets", "MARS"),
            ApplicationName = GetString(values, "Application Name", "ApplicationName", "App")
        };
    }

    public AppSettings Normalize()
    {
        return this with
        {
            DefaultResultLimit = Math.Max(1, DefaultResultLimit),
            ConnectionTimeout = Math.Max(0, ConnectionTimeout),
            CommandTimeout = Math.Max(0, CommandTimeout),
            Theme = string.Equals(Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark",
            Language = string.Equals(Language, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en",
            Encrypt = NormalizeEncrypt(Encrypt),
            Database = string.IsNullOrWhiteSpace(Database) ? "annalaura" : Database.Trim(),
            ApplicationName = string.IsNullOrWhiteSpace(ApplicationName) ? "SQL Commander" : ApplicationName.Trim()
        };
    }

    static Dictionary<string, string> ParseConnectionStringValues(string connectionString)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var builder = new System.Data.Common.DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        foreach (string key in builder.Keys)
        {
            values[key] = builder[key]?.ToString() ?? string.Empty;
        }

        return values;
    }

    static AuthenticationType InferAuthenticationType(string authentication, bool hasSqlCredentials)
    {
        if (!string.IsNullOrWhiteSpace(authentication))
        {
            if (authentication.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
                authentication.Contains("msi", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticationType.AzureManagedIdentity;
            }

            if (authentication.Contains("default", StringComparison.OrdinalIgnoreCase) ||
                authentication.Contains("active directory", StringComparison.OrdinalIgnoreCase) ||
                authentication.Contains("entra", StringComparison.OrdinalIgnoreCase) ||
                authentication.Contains("azure", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticationType.AzureDefaultCredential;
            }

            if (authentication.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
                authentication.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticationType.SqlAuthentication;
            }
        }

        return hasSqlCredentials ? AuthenticationType.SqlAuthentication : AuthenticationType.AzureDefaultCredential;
    }

    static string GetString(IReadOnlyDictionary<string, string> values, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (values.TryGetValue(alias, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    static bool GetBool(IReadOnlyDictionary<string, string> values, bool defaultValue, params string[] aliases)
    {
        var value = GetString(values, aliases);
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (bool.TryParse(value, out var parsed)) return parsed;
        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "sspi", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)) return false;
        return defaultValue;
    }

    static int GetInt(IReadOnlyDictionary<string, string> values, int defaultValue, params string[] aliases)
    {
        var value = GetString(values, aliases);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    static string NormalizeEncrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Mandatory";
        if (bool.TryParse(value, out var parsed)) return parsed ? "Mandatory" : "Optional";

        return value.Trim().ToLowerInvariant() switch
        {
            "yes" or "mandatory" or "required" or "true" => "Mandatory",
            "strict" => "Strict",
            "optional" or "no" or "false" => "Optional",
            _ => value.Trim()
        };
    }
}
