using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Order.Tests.UnitTests.Presentation.Controllers.Base;
using System.Net;
using System.Net.Http.Json;

namespace Order.Tests.UnitTests.Presentation
{
    public sealed class RateLimitingTests : ApiUnitTestBase
    {
        protected override IDictionary<string, string?>? Settings => new Dictionary<string, string?>
        {
            ["ApplicationConfiguration:RateLimiting:PermitLimit"] = "2",
            ["ApplicationConfiguration:RateLimiting:Window"] = "00:01:00"
        };

        [Test]
        public async Task OrderEndpoints_OverTheLimit_Return429WithRetryAfter()
        {
            // Assemble
            string historyRoute = $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}";
            await HttpClient!.GetAsync(historyRoute);
            await HttpClient!.GetAsync(historyRoute);

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync(historyRoute);

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(response.Headers.RetryAfter, Is.Not.Null);
                Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status429TooManyRequests));
            }
        }

        [Test]
        public async Task OrderEndpoints_LimitIsPerCustomer()
        {
            // Assemble: customer 42 uses up the limit.
            string historyRoute = $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}";
            await HttpClient!.GetAsync(historyRoute);
            await HttpClient!.GetAsync(historyRoute);
            UseToken(TestTokens.ForCustomer(7));

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync($"{OrderRoute}/by/customer/7");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task OperationalEndpoints_AreNotRateLimited()
        {
            // Act
            HttpStatusCode[] statusCodes = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(async _ => (await HttpClient!.GetAsync("/healthz/live")).StatusCode));

            // Assert
            Assert.That(statusCodes, Is.All.EqualTo(HttpStatusCode.OK));
        }
    }
}