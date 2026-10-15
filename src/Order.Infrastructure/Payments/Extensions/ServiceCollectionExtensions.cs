using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Order.Application.Constants;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Payments;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Infrastructure.Payments.Exceptions;
using Order.Infrastructure.Payments.Gateways;
using Polly;
using System.Net.Http.Headers;

namespace Order.Infrastructure.Payments.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private const string BearerScheme = "Bearer";

        private static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);

        public static IServiceCollection AddPaymentGateways(this IServiceCollection services, OrderConfiguration configuration)
        {
            services
                .AddHttpClient<CardPaymentGateway>(PaymentGatewayNames.Card)
                .ConfigureGateway(PaymentGatewayNames.Card, GetGatewayConfiguration(configuration, PaymentGatewayNames.Card));

            services
                .AddHttpClient<PayPalPaymentGateway>(PaymentGatewayNames.PayPal)
                .ConfigureGateway(PaymentGatewayNames.PayPal, GetGatewayConfiguration(configuration, PaymentGatewayNames.PayPal));

            return services
                .AddTransient<IPaymentGateway>(serviceProvider => serviceProvider.GetRequiredService<CardPaymentGateway>())
                .AddTransient<IPaymentGateway>(serviceProvider => serviceProvider.GetRequiredService<PayPalPaymentGateway>())
                .AddTransient<IPaymentGateway, BankTransferPaymentGateway>()
                .AddTransient<IPaymentGatewayResolver, PaymentGatewayResolver>();
        }

        private static void ConfigureGateway(this IHttpClientBuilder builder, string gatewayName, PaymentGatewayConfiguration gatewayConfiguration)
        {
            builder.ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(gatewayConfiguration.BaseUrl.TrimEnd('/') + "/");

                if (!string.IsNullOrWhiteSpace(gatewayConfiguration.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(BearerScheme, gatewayConfiguration.ApiKey);
                }
            });

            if (gatewayConfiguration.UseMockProvider)
            {
                builder.ConfigurePrimaryHttpMessageHandler(() => new MockPaymentProviderHandler());
            }

            builder.AddResilienceHandler(gatewayName, (pipeline, context) =>
                ConfigureResiliencePipeline(pipeline, context.ServiceProvider.GetRequiredService<IObservabilityManager>(), gatewayName, gatewayConfiguration));
        }

        /// <summary>
        /// Outermost first: overall timeout → retry (network errors, 408, 429 and 5xx, exponential back-off)
        /// → circuit breaker (opens after CircuitBreakerFailureThreshold consecutive failures).
        /// 400 and 409 are business answers and are never retried.
        /// </summary>
        private static void ConfigureResiliencePipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline,
            IObservabilityManager observabilityManager, string gatewayName, PaymentGatewayConfiguration gatewayConfiguration)
        {
            KeyValuePair<string, object?> gatewayTag = new(MetricDimensions.PaymentGateway, gatewayName);

            pipeline
                .AddTimeout(gatewayConfiguration.Timeout)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = gatewayConfiguration.RetryCount,
                    Delay = TimeSpan.FromSeconds(gatewayConfiguration.RetryBackoffBaseSeconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = arguments =>
                    {
                        observabilityManager.LogMessage(string.Format(WarningMessages.PaymentGatewayRetry, gatewayName, arguments.AttemptNumber + 1,
                            (long)arguments.RetryDelay.TotalMilliseconds, (int?)arguments.Outcome.Result?.StatusCode)).AsWarning(arguments.Outcome.Exception);
                        observabilityManager.RecordMetric(MetricDefinitions.PaymentGatewayRetryCount).Record(1, gatewayTag);

                        return ValueTask.CompletedTask;
                    }
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    // Every sampled call failing, with at least the threshold sampled, means N consecutive failures.
                    FailureRatio = 1.0,
                    MinimumThroughput = Math.Max(2, gatewayConfiguration.CircuitBreakerFailureThreshold),
                    SamplingDuration = CircuitBreakerSamplingDuration,
                    BreakDuration = gatewayConfiguration.CircuitBreakerDuration,
                    OnOpened = arguments =>
                    {
                        observabilityManager.LogMessage(string.Format(WarningMessages.PaymentGatewayCircuitOpened, gatewayName,
                            (long)arguments.BreakDuration.TotalMilliseconds, (int?)arguments.Outcome.Result?.StatusCode)).AsWarning(arguments.Outcome.Exception);
                        observabilityManager.RecordMetric(MetricDefinitions.PaymentGatewayCircuitOpenedCount).Record(1, gatewayTag);

                        return ValueTask.CompletedTask;
                    }
                });
        }

        private static PaymentGatewayConfiguration GetGatewayConfiguration(OrderConfiguration configuration, string gatewayName)
        {
            return configuration.ApplicationConfiguration.PaymentGateways.TryGetValue(gatewayName, out PaymentGatewayConfiguration? gatewayConfiguration)
                ? gatewayConfiguration
                : throw new PaymentGatewayConfigurationException(string.Format(ErrorMessages.PaymentGatewayConfigurationMissingErrorMessage, gatewayName));
        }
    }
}