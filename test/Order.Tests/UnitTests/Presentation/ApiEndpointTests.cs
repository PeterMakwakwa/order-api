using Order.Application.Constants;
using Order.Tests.UnitTests.Presentation.Controllers.Base;
using System.Net;

namespace Order.Tests.UnitTests.Presentation
{
    /// <summary>
    /// Operational endpoints: health, OpenAPI and metrics. None of them need a token or a CorrelationId.
    /// </summary>
    public sealed class ApiEndpointTests : ApiUnitTestBase
    {
        [TestCase("/healthz/live")]
        [TestCase("/healthz/ready")]
        public async Task HealthCheck_WithoutCorrelationIdOrToken_ReturnsHealthyJson(string path)
        {
            // Assemble
            HttpClient!.DefaultRequestHeaders.Remove(RequestHeaderKeys.CorrelationId);
            UseToken(null);

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync(path);

            // Assert
            string body = await response.Content.ReadAsStringAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(body, Does.Contain("Healthy"));
            }
        }

        [Test]
        public async Task OpenApi_Document_DescribesEndpointsHeadersAndBearerAuth()
        {
            // Assemble
            UseToken(null);

            // Act
            string document = await HttpClient!.GetStringAsync("/openapi/v1.json");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(document, Does.Contain("/api/v1/Order"));
                Assert.That(document, Does.Contain("/api/v1/Order/{orderId}"));
                Assert.That(document, Does.Contain("\"title\": \"Orders API\""));
                Assert.That(document, Does.Contain("\"name\": \"CorrelationId\""));
                Assert.That(document, Does.Contain("\"name\": \"Idempotency-Key\""));
                Assert.That(document, Does.Contain("\"scheme\": \"bearer\""));
                Assert.That(document, Does.Contain("\"Bearer\": [ ]"));
                Assert.That(document, Does.Not.Contain("\"discount\""));
            }
        }

        [Test]
        public async Task Metrics_AfterPlacingOrder_ExposesCustomMetrics()
        {
            // Assemble
            HttpResponseMessage placeResponse = await PostOrderAsync();
            Assert.That(placeResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created), await placeResponse.Content.ReadAsStringAsync());

            // Act
            string metrics = await HttpClient!.GetStringAsync("/metrics");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(metrics, Does.Contain("order_api_tests_place_order_success_count"));
                Assert.That(metrics, Does.Contain("order_api_tests_payment_charge_duration_ms"));
                Assert.That(metrics, Does.Contain("order_api_tests_api_request_duration_ms"));
            }
        }
    }
}