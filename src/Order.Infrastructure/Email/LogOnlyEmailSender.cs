using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;

namespace Order.Infrastructure.Email
{
    /// <summary>
    /// Used when no SMTP host is configured (local development): logs the email instead of sending it.
    /// </summary>
    internal sealed class LogOnlyEmailSender(IObservabilityManager observabilityManager) : IEmailSender
    {
        public Task SendAsync(EmailMessage emailMessage, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(string.Format(InfoMessages.EmailLogged, emailMessage.To, emailMessage.Subject, emailMessage.Body)).AsInfo();

            observabilityManager
                .RecordMetric(MetricDefinitions.EmailSentCount)
                .Record();

            return Task.CompletedTask;
        }
    }
}