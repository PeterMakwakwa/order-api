using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using MimeKit;
using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Observability.Metrics;
using Order.Infrastructure.Email;
using Order.Infrastructure.Email.Exceptions;
using Order.Tests.Setup;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Order.Tests.UnitTests.Infrastructure
{
    /// <summary>
    /// Runs the real MailKit sender and "email" Polly pipeline against a port nobody listens on.
    /// appsettings.UnitTest.json: 1 retry, ~no back-off.
    /// </summary>
    public sealed class SmtpEmailSenderTests
    {
        private ServiceProvider? _serviceProvider;

        [SetUp]
        public void Setup()
        {
            _serviceProvider = UnitTestSetup.Setup(settings: new Dictionary<string, string?>
            {
                ["ApplicationConfiguration:Smtp:Host"] = IPAddress.Loopback.ToString(),
                ["ApplicationConfiguration:Smtp:Port"] = GetClosedPort().ToString(CultureInfo.InvariantCulture),
                ["ApplicationConfiguration:Smtp:EnableSsl"] = bool.FalseString
            });
        }

        [TearDown]
        public void Teardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void SendAsync_ServerUnreachable_RetriesThenThrowsEmailDeliveryException()
        {
            // Assemble
            IEmailSender emailSender = _serviceProvider!.GetRequiredService<IEmailSender>();
            using MetricCollector<long> retryCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.EmailRetryCount.Name);
            using MetricCollector<long> failedCollector = new(meterScope: null, UnitTestSetup.ApplicationName, MetricDefinitions.EmailFailedCount.Name);

            // Act
            EmailDeliveryException? exception = Assert.ThrowsAsync<EmailDeliveryException>(() => emailSender.SendAsync(new EmailMessage
            {
                To = "customer@example.com",
                Subject = "Order Confirmation",
                Body = "Your order has been placed."
            }));

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception!.GetBaseException(), Is.InstanceOf<SocketException>());
                Assert.That(retryCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(1));
                Assert.That(failedCollector.GetMeasurementSnapshot(), Has.Count.EqualTo(1));
            }
        }

        private static int GetClosedPort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        [Test]
        public void BuildMimeMessage_WithDeduplicationKey_UsesTheSameMessageIdOnEverySend()
        {
            // Assemble
            SmtpEmailSender emailSender = (SmtpEmailSender)_serviceProvider!.GetRequiredService<IEmailSender>();
            EmailMessage emailMessage = new()
            {
                To = "customer@example.com",
                Subject = "Order Confirmation",
                Body = "Your order has been placed.",
                DeduplicationKey = "order-confirmation.abc123"
            };

            // Act
            using MimeMessage first = emailSender.BuildMimeMessage(emailMessage);
            using MimeMessage resent = emailSender.BuildMimeMessage(emailMessage);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(first.MessageId, Does.StartWith("order-confirmation.abc123@"));
                Assert.That(resent.MessageId, Is.EqualTo(first.MessageId));
            }
        }

        [Test]
        public void BuildMimeMessage_WithoutDeduplicationKey_GetsANewMessageIdPerSend()
        {
            // Assemble
            SmtpEmailSender emailSender = (SmtpEmailSender)_serviceProvider!.GetRequiredService<IEmailSender>();
            EmailMessage emailMessage = new() { To = "customer@example.com", Subject = "Hello", Body = "Hello." };

            // Act
            using MimeMessage first = emailSender.BuildMimeMessage(emailMessage);
            using MimeMessage second = emailSender.BuildMimeMessage(emailMessage);

            // Assert
            Assert.That(second.MessageId, Is.Not.EqualTo(first.MessageId));
        }
    }
}