using Order.Application.Models.Configuration.Application;

namespace Order.Application.Models.Configuration
{
    /// <summary>
    /// Root of the appsettings model, bound once at start-up and registered as a singleton.
    /// </summary>
    [Serializable]
    public sealed class OrderConfiguration
    {
        public required ApplicationConfiguration ApplicationConfiguration { get; init; }
    }
}