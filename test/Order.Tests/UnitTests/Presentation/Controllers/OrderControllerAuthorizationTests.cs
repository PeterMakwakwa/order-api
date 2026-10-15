using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Order.Application.Models.Response.V1;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Order.Tests.UnitTests.Presentation.Controllers.Base;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace Order.Tests.UnitTests.Presentation.Controllers
{
    /// <summary>
    /// Customers act only as themselves (the customer id comes from the token); support staff can read any customer's orders.
    /// </summary>
    public sealed class OrderControllerAuthorizationTests : ApiUnitTestBase
    {
        private const int OtherCustomerId = 7;

        [Test]
        public async Task PlaceOrderAsync_WithoutToken_Returns401ProblemWithoutCharging()
        {
            // Assemble
            UseToken(null);

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(response.Headers.WwwAuthenticate.ToString(), Does.StartWith("Bearer"));
                Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status401Unauthorized));
            }

            await PaymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task PlaceOrderAsync_TokenSignedWithAnotherKey_Returns401()
        {
            // Assemble
            UseToken(TestTokens.Create(PlaceOrderRequestBuilder.DefaultCustomerId, signingKey: RandomNumberGenerator.GetBytes(32)));

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task PlaceOrderAsync_ExpiredToken_Returns401()
        {
            // Assemble
            UseToken(TestTokens.Create(PlaceOrderRequestBuilder.DefaultCustomerId, expires: DateTime.UtcNow.AddMinutes(-10)));

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task PlaceOrderAsync_TokenWithoutCustomerId_Returns403()
        {
            // Assemble: support staff can read orders but cannot place them.
            UseToken(TestTokens.ForSupport());

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status403Forbidden));
            }

            await PaymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_AnotherCustomer_Returns403()
        {
            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync($"{OrderRoute}/by/customer/{OtherCustomerId}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_AsSupport_ReturnsTheCustomersOrders()
        {
            // Assemble
            await PostOrderAsync();
            UseToken(TestTokens.ForSupport());

            // Act
            GetCustomerOrderHistoryResponse? history = await HttpClient!.GetFromJsonAsync<GetCustomerOrderHistoryResponse>(
                $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}", JsonOptions);

            // Assert
            Assert.That(history!.Orders, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetOrderAsync_AnotherCustomersOrder_Returns404()
        {
            // Assemble
            HttpResponseMessage placeResponse = await PostOrderAsync();
            UseToken(TestTokens.ForCustomer(OtherCustomerId));

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync(placeResponse.Headers.Location);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetOrderAsync_AsSupport_ReturnsOrder()
        {
            // Assemble
            HttpResponseMessage placeResponse = await PostOrderAsync();
            UseToken(TestTokens.ForSupport());

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync(placeResponse.Headers.Location);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}