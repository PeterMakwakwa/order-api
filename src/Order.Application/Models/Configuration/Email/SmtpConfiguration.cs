namespace Order.Application.Models.Configuration.Email
{
    [Serializable]
    public sealed class SmtpConfiguration
    {
        /// <summary>
        /// Leave empty to log emails instead of sending them (local development).
        /// </summary>
        public string Host { get; init; } = string.Empty;

        public int Port { get; init; } = 587;

        public bool EnableSsl { get; init; } = true;

        public required string FromAddress { get; init; }

        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// Secret: supply through user-secrets / environment variables / a vault, never appsettings.json.
        /// </summary>
        public string Password { get; init; } = string.Empty;
    }
}