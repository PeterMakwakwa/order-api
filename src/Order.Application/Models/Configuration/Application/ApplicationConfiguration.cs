using Order.Application.Models.Configuration.Email;
using Order.Application.Models.Configuration.Observability;
using Order.Application.Models.Configuration.Outbox;
using Order.Application.Models.Configuration.Payments;
using Order.Application.Models.Configuration.Persistence;
using Order.Application.Models.Configuration.RateLimiting;
using Order.Application.Models.Configuration.Reconciliation;
using Order.Application.Models.Configuration.Resilience;

namespace Order.Application.Models.Configuration.Application
{
    [Serializable]
    public sealed class ApplicationConfiguration
    {
        public required ObservabilityConfiguration Observability { get; init; }

        public required Dictionary<string, SqlServerConfiguration> Persistence { get; init; }

        public required OutboxConfiguration Outbox { get; init; }

        public required Dictionary<string, PaymentGatewayConfiguration> PaymentGateways { get; init; }

        public required SmtpConfiguration Smtp { get; init; }

        public required ResilienceConfiguration Resilience { get; init; }

        public RateLimitingConfiguration RateLimiting { get; init; } = new();

        public ReconciliationConfiguration Reconciliation { get; init; } = new();
    }
}