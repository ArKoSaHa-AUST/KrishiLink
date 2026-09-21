using Npgsql;

namespace KrishiLink.DAL;

public static class DatabaseConfiguration
{
    public const string Schema = "krishilink";

    public static string GetConnectionString(IConfiguration configuration, bool forMigrations = false)
    {
        var value = forMigrations
            ? First(configuration.GetConnectionString("MigrationConnection"),
                configuration["DIRECT_URL"], configuration.GetConnectionString("DirectConnection"),
                configuration["DIRECT_DB_URL"])
            : First(configuration.GetConnectionString("SessionConnection"), configuration["DIRECT_URL"],
                configuration.GetConnectionString("DefaultConnection"), configuration["DATABASE_URL"]);

        value ??= forMigrations
            ? First(configuration.GetConnectionString("DefaultConnection"), configuration["DATABASE_URL"])
            : null;
        if (value is null)
            throw new InvalidOperationException(
                "Configure ConnectionStrings__DefaultConnection or DATABASE_URL for PostgreSQL.");

        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                         value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? ParseUri(value)
                : new NpgsqlConnectionStringBuilder(value);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // Connection parsing exceptions can contain credentials; do not include their message.
            throw new InvalidOperationException("Invalid PostgreSQL connection configuration. Check the Dashboard Connect settings.");
        }

        if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database) ||
            string.IsNullOrWhiteSpace(connection.Username) || string.IsNullOrWhiteSpace(connection.Password))
            throw new InvalidOperationException("PostgreSQL host, database, username and password are required.");
        if (connection.Port == 6543)
            throw new InvalidOperationException(
                "KrishiLink requires a direct or session-pooler connection on port 5432. Set DIRECT_URL or ConnectionStrings__SessionConnection; migrations can use ConnectionStrings__MigrationConnection.");

        connection.SslMode = SslMode.VerifyFull;
        connection.Remove("Trust Server Certificate");
        if (string.IsNullOrWhiteSpace(connection.RootCertificate))
            connection.RootCertificate = configuration["Database:RootCertificate"]
                ?? Path.Combine(AppContext.BaseDirectory, "supabase-ca.crt");
        connection.IncludeErrorDetail = false;
        connection.LogParameters = false;
        connection.MaxAutoPrepare = 0;
        connection.MaxPoolSize = 20;
        connection.Timeout = 15;
        connection.CommandTimeout = 60;
        connection.Timezone = "UTC";
        return connection.ConnectionString;
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static NpgsqlConnectionStringBuilder ParseUri(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2)
            throw new FormatException();
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1])
        };
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]).ToLowerInvariant();
            var parameter = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : "";
            switch (key)
            {
                case "pgbouncer" when parameter is "true" or "false":
                    break;
                case "sslmode":
                    // TLS certificate and hostname verification are always required.
                    break;
                default:
                    throw new FormatException();
            }
        }
        return connection;
    }
}
