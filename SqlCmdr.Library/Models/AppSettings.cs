namespace SqlCmdr.Models;

public enum AuthenticationType
{
    SqlAuthentication,
    AzureDefaultCredential
}

public record AppSettings
{
    public string Server { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int DefaultResultLimit { get; init; } = 100;
    public bool TrustServerCertificate { get; init; } = true;
    public int ConnectionTimeout { get; init; } = 30;
    public bool ConfirmActions { get; init; } = false;
    public string Theme { get; init; } = "dark";
    public AuthenticationType AuthenticationType { get; init; } = AuthenticationType.SqlAuthentication;

    public string ToConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectionTimeout
        };

        builder.Encrypt = true;

        if (AuthenticationType == AuthenticationType.SqlAuthentication)
        {
            builder.Authentication = Microsoft.Data.SqlClient.SqlAuthenticationMethod.SqlPassword;
            builder.UserID = UserId;
            builder.Password = Password;
        }
        else
        {
            builder.Remove("Authentication");
            builder.Remove("User ID");
            builder.Remove("Password");
        }

        return builder.ConnectionString;
    }

    public static AppSettings FromConnectionString(string connectionString, int defaultResultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new AppSettings { DefaultResultLimit = defaultResultLimit };
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        
        // Determine auth type based on connection string metadata
        var hasSqlCredentials = !string.IsNullOrWhiteSpace(builder.UserID) || !string.IsNullOrWhiteSpace(builder.Password);
        var isIntegratedSecurity = builder.IntegratedSecurity;

        AuthenticationType authType = builder.Authentication switch
        {
            Microsoft.Data.SqlClient.SqlAuthenticationMethod.SqlPassword => AuthenticationType.SqlAuthentication,
            Microsoft.Data.SqlClient.SqlAuthenticationMethod.NotSpecified when hasSqlCredentials || isIntegratedSecurity => AuthenticationType.SqlAuthentication,
            Microsoft.Data.SqlClient.SqlAuthenticationMethod.NotSpecified => AuthenticationType.AzureDefaultCredential,
            _ => AuthenticationType.AzureDefaultCredential
        };

        return new AppSettings
        {
            Server = builder.DataSource,
            Database = builder.InitialCatalog,
            UserId = authType == AuthenticationType.SqlAuthentication ? builder.UserID : string.Empty,
            Password = authType == AuthenticationType.SqlAuthentication ? builder.Password : string.Empty,
            DefaultResultLimit = defaultResultLimit,
            TrustServerCertificate = builder.TrustServerCertificate,
            ConnectionTimeout = builder.ConnectTimeout,
            AuthenticationType = authType
        };
    }
}
