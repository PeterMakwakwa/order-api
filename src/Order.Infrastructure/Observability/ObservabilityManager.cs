using Microsoft.Extensions.Logging;
using Order.Application.Models.Configuration;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Models.Metrics.Definitions;
using Order.Infrastructure.Observability.Managers;
using Order.Infrastructure.Observability.Messages;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Order.Infrastructure.Observability
{
    /// <summary>
    /// Logs through ILogger, records metrics on a Meter and starts spans on an ActivitySource, all named after
    /// ApplicationConfiguration:Observability:ApplicationName. Registered as a singleton.
    /// The Meter is created directly (not through IMeterFactory) because prometheus-net only exports unscoped meters.
    /// </summary>
    public sealed class ObservabilityManager : IObservabilityManager, IDisposable
    {
        private readonly ILogger<LogManager> _logger;
        private readonly Meter _meter;
        private readonly MetricManager _metricManager;
        private readonly ActivitySource _activitySource;

        public ObservabilityManager(ILogger<LogManager> logger, OrderConfiguration configuration)
        {
            string applicationName = configuration.ApplicationConfiguration.Observability.ApplicationName;
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new InvalidOperationException(ErrorMessages.ApplicationNameMissingErrorMessage);
            }

            _logger = logger;
            _meter = new Meter(applicationName);
            _metricManager = new MetricManager(_meter);
            _activitySource = new ActivitySource(applicationName);
        }

        public ILogManager LogMessage(string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "")
        {
            return new LogManager(_logger, Path.GetFileNameWithoutExtension(callerFilePath), callerMemberName, message);
        }

        public IMetric RecordMetric(MetricDefinition metric)
        {
            return _metricManager.GetOrCreate(metric);
        }

        public ITraceManager CreateTrace(string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "")
        {
            return new TraceManager(_activitySource, Path.GetFileNameWithoutExtension(callerFilePath), callerMemberName, message);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            _meter.Dispose();
        }
    }
}