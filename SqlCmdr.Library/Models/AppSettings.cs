namespace SqlCmdr.Models;

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
    public string ToConnectionString() => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource = Server, InitialCatalog = Database, UserID = UserId, Password = Password, TrustServerCertificate = TrustServerCertificate, ConnectTimeout = ConnectionTimeout }.ConnectionString;
    public static AppSettings FromConnectionString(string connectionString, int defaultResultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new AppSettings { DefaultResultLimit = defaultResultLimit };
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return new AppSettings { Server = builder.DataSource, Database = builder.InitialCatalog, UserId = builder.UserID, Password = builder.Password, DefaultResultLimit = defaultResultLimit, TrustServerCertificate = builder.TrustServerCertificate, ConnectionTimeout = builder.ConnectTimeout };
    }
}
