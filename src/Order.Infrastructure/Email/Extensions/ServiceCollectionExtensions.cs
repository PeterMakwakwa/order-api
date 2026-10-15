using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Constants;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Resilience;
using Order.Domain.Interfaces.Email;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Polly;
using Polly.Retry;
using System.Net.Sockets;

namespace Order.Infrastructure.Email.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEmailSender(this IServiceCollection services, OrderConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ApplicationConfiguration.Smtp.Host))
            {
                return services
                    .AddTransient<IEmailSender, LogOnlyEmailSender>();
            }

            RetryResilienceConfiguration resilienceConfiguration = configuration.ApplicationConfiguration.Resilience.Email;

            services.AddResiliencePipeline(ResiliencePipelineNames.Email, (builder, context) =>
            {
                IObservabilityManager observabilityManager = context.ServiceProvider.GetRequiredService<IObservabilityManager>();

                builder
                    .AddRetry(new RetryStrategyOptions
                    {
                        // Transient only: connection problems and 4xx SMTP replies (e.g. 421 service not available,
                        // 450 mailbox busy). 5xx replies such as an unknown recipient are permanent and not retried.
                        ShouldHandle = new PredicateBuilder()
                            .Handle<SmtpCommandException>(ex => (int)ex.StatusCode is >= 400 and < 500)
                            .Handle<SmtpProtocolException>()
                            .Handle<ServiceNotConnectedException>()
                            .Handle<SocketException>()
                            .Handle<IOException>(),
                        MaxRetryAttempts = resilienceConfiguration.MaxRetryAttempts,
                        Delay = resilienceConfiguration.BaseDelay,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        OnRetry = arguments =>
                        {
                            observabilityManager.LogMessage(string.Format(WarningMessages.EmailSendRetry,
                                arguments.AttemptNumber + 1, (long)arguments.RetryDelay.TotalMilliseconds)).AsWarning(arguments.Outcome.Exception);
                            observabilityManager.RecordMetric(MetricDefinitions.EmailRetryCount).Record();

                            return ValueTask.CompletedTask;
                        }
                    })
                    .AddTimeout(resilienceConfiguration.Timeout);
            });

            return services
                .AddTransient<IEmailSender, SmtpEmailSender>();
        }
    }
}