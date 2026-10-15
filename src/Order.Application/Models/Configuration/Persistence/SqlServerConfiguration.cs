using System.Data.Common;

namespace Order.Application.Models.Configuration.Persistence
{
    /// <summary>
    /// Credentials come from user-secrets / environment variables / a vault, never appsettings.json.
    /// Leaving the username empty uses Windows authentication.
    /// </summary>
    [Serializable]
    public sealed class SqlServerConfiguration
    {
        public required string Server { get; init; }

        public required string Database { get; init; }

        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public bool Encrypt { get; init; } = true;

        public string GetConnectionString()
        {
            DbConnectionStringBuilder builder = new()
            {
                ["Data Source"] = Server,
                ["Initial Catalog"] = Database,
                ["Encrypt"] = Encrypt,
                ["TrustServerCertificate"] = true
            };

            if (string.IsNullOrWhiteSpace(Username))
            {
                builder["Integrated Security"] = true;
            }
            else
            {
                builder["User ID"] = Username;
                builder["Password"] = Password;
            }

            return builder.ConnectionString;
        }
    }
}