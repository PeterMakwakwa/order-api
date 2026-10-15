using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Order.Application.Constants;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Email;
using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Infrastructure.Email.Exceptions;
using Polly;
using Polly.Registry;

namespace Order.Infrastructure.Email
{
    /// <summary>
    /// Sends email over SMTP with MailKit inside the "email" Polly pipeline (retry with back-off + timeout).
    /// An email with a DeduplicationKey gets the Message-ID "{key}@{sender domain}" on every send, including re-sends.
    /// The client and message are created per send and disposed. With EnableSsl, TLS is required: implicit TLS on
    /// port 465, STARTTLS otherwise (never the "when available" downgrade).
    /// </summary>
    internal sealed class SmtpEmailSender(IObservabilityManager observabilityManager,
        OrderConfiguration configuration,
        ResiliencePipelineProvider<string> resiliencePipelineProvider) : IEmailSender
    {
        private const int ImplicitTlsPort = 465;

        private readonly SmtpConfiguration _smtpConfiguration = configuration.ApplicationConfiguration.Smtp;

        public async Task SendAsync(EmailMessage emailMessage, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            ResiliencePipeline resiliencePipeline = resiliencePipelineProvider.GetPipeline(ResiliencePipelineNames.Email);

            try
            {
                await resiliencePipeline.ExecuteAsync(async token => await SendOnceAsync(emailMessage, token), cancellationToken);

                observabilityManager
                    .RecordMetric(MetricDefinitions.EmailSentCount)
                    .Record();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.EmailFailedCount)
                    .Record();

                throw new EmailDeliveryException(ErrorMessages.EmailDeliveryErrorMessage, ex);
            }
        }

        private async Task SendOnceAsync(EmailMessage emailMessage, CancellationToken cancellationToken)
        {
            using (SmtpClient smtpClient = new())
            using (MimeMessage mimeMessage = BuildMimeMessage(emailMessage))
            {
                await smtpClient.ConnectAsync(_smtpConfiguration.Host, _smtpConfiguration.Port, GetSecureSocketOptions(), cancellationToken);

                if (!string.IsNullOrWhiteSpace(_smtpConfiguration.Username))
                {
                    await smtpClient.AuthenticateAsync(_smtpConfiguration.Username, _smtpConfiguration.Password, cancellationToken);
                }

                await smtpClient.SendAsync(mimeMessage, cancellationToken);
                await smtpClient.DisconnectAsync(quit: true, cancellationToken);
            }
        }

        internal MimeMessage BuildMimeMessage(EmailMessage emailMessage)
        {
            MailboxAddress from = MailboxAddress.Parse(_smtpConfiguration.FromAddress);
            MimeMessage mimeMessage = new()
            {
                Subject = emailMessage.Subject,
                Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = emailMessage.Body }
            };
            mimeMessage.From.Add(from);
            mimeMessage.To.Add(MailboxAddress.Parse(emailMessage.To));

            if (emailMessage.DeduplicationKey != null)
            {
                mimeMessage.MessageId = $"{emailMessage.DeduplicationKey}@{from.Domain}";
            }

            return mimeMessage;
        }

        private SecureSocketOptions GetSecureSocketOptions()
        {
            if (!_smtpConfiguration.EnableSsl)
            {
                return SecureSocketOptions.None;
            }

            return _smtpConfiguration.Port == ImplicitTlsPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }
    }
}