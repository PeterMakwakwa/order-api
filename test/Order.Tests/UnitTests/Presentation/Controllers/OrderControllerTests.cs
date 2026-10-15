using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Order.Application.Constants;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Application.Models.Response.V1;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments.Models;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Order.Tests.UnitTests.Presentation.Controllers.Base;
using System.Net;
using System.Net.Http.Json;

namespace Order.Tests.UnitTests.Presentation.Controllers
{
    public sealed class OrderControllerTests : ApiUnitTestBase
    {
        [Test]
        public async Task PlaceOrderAsync_ValidRequest_Returns201WithLocationAndEchoesCorrelationId()
        {
            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            PlaceOrderResponse? body = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo($"/api/v1/Order/{body!.OrderId}").IgnoreCase);
                Assert.That(response.Headers.Contains(ResponseHeaderKeys.IdempotentReplayed), Is.False);
                Assert.That(body.Id, Is.EqualTo(CorrelationId));
                Assert.That(body.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(body.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(response.Headers.GetValues(RequestHeaderKeys.CorrelationId).Single(), Is.EqualTo(CorrelationId));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_CustomerIdInBody_IsIgnoredInFavourOfToken()
        {
            // Assemble: the body tries to place the order for customer 7; the token is customer 42's.
            HttpResponseMessage response = await HttpClient!.SendAsync(new HttpRequestMessage(HttpMethod.Post, OrderRoute)
            {
                Headers = { { RequestHeaderKeys.IdempotencyKey, Guid.NewGuid().ToString() } },
                Content = new StringContent("""
                    {"customerId":7,"customerEmail":"customer@example.com","items":[{"productId":1,"quantity":1}],"paymentMethod":"CreditCard"}
                    """, System.Text.Encoding.UTF8, "application/json")
            });
            PlaceOrderResponse? placed = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);

            // Act
            GetOrderResponse? order = await HttpClient!.GetFromJsonAsync<GetOrderResponse>($"{OrderRoute}/{placed!.OrderId}", JsonOptions);

            // Assert
            Assert.That(order!.CustomerId, Is.EqualTo(PlaceOrderRequestBuilder.DefaultCustomerId));
        }

        [Test]
        public async Task PlaceOrderAsync_DiscountInBody_IsIgnored()
        {
            // Assemble: the legacy "discount" field is no longer part of the contract.
            HttpResponseMessage response = await HttpClient!.SendAsync(new HttpRequestMessage(HttpMethod.Post, OrderRoute)
            {
                Headers = { { RequestHeaderKeys.IdempotencyKey, Guid.NewGuid().ToString() } },
                Content = new StringContent("""
                    {"customerEmail":"customer@example.com","items":[{"productId":1,"quantity":1}],"paymentMethod":"CreditCard","discount":0.99}
                    """, System.Text.Encoding.UTF8, "application/json")
            });

            // Act
            PlaceOrderResponse? placed = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);

            // Assert
            Assert.That(placed!.Total, Is.EqualTo(DefaultProducts.Keyboard.Price));
        }

        [Test]
        public async Task PlaceOrderAsync_MissingCorrelationId_Returns400()
        {
            // Assemble
            HttpClient!.DefaultRequestHeaders.Remove(RequestHeaderKeys.CorrelationId);

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(problem!.Title, Does.Contain(RequestHeaderKeys.CorrelationId));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_MissingIdempotencyKey_Returns400WithoutCharging()
        {
            // Act
            HttpResponseMessage response = await PostOrderAsync(sendIdempotencyKey: false);

            // Assert
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(problem!.Errors.Keys, Has.Some.Contains(RequestHeaderKeys.IdempotencyKey));
            }

            await PaymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [Test]
        public async Task PlaceOrderAsync_IdempotencyKeyTooLong_Returns400()
        {
            // Act
            HttpResponseMessage response = await PostOrderAsync(idempotencyKey: new string('k', 101));

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PlaceOrderAsync_RetriedWithSameIdempotencyKey_ReplaysWithoutChargingTwice()
        {
            // Assemble
            string idempotencyKey = Guid.NewGuid().ToString();
            HttpResponseMessage first = await PostOrderAsync(idempotencyKey: idempotencyKey);
            PlaceOrderResponse? firstBody = await first.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);

            // Act
            HttpResponseMessage retry = await PostOrderAsync(idempotencyKey: idempotencyKey);

            // Assert
            PlaceOrderResponse? retryBody = await retry.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                Assert.That(retry.Headers.GetValues(ResponseHeaderKeys.IdempotentReplayed).Single(), Is.EqualTo("true"));
                Assert.That(retry.Headers.Location, Is.EqualTo(first.Headers.Location));
                Assert.That(retryBody, Is.EqualTo(firstBody));
            }

            await PaymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task PlaceOrderAsync_SameIdempotencyKeyDifferentRequest_Returns422()
        {
            // Assemble
            string idempotencyKey = Guid.NewGuid().ToString();
            await PostOrderAsync(idempotencyKey: idempotencyKey);

            // Act
            HttpResponseMessage response = await PostOrderAsync(new PlaceOrderRequestBuilder().WithPromotionCode(null).Build(), idempotencyKey);

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
                Assert.That(problem!.Title, Does.Contain(RequestHeaderKeys.IdempotencyKey));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_InvalidRequest_Returns400WithValidationErrors()
        {
            // Act
            HttpResponseMessage response = await PostOrderAsync(new PlaceOrderRequestBuilder().WithItems([]).Build());

            // Assert
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(problem!.Errors.Keys, Does.Contain(nameof(PlaceOrderRequest.Items)));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_UnknownProductAndPromotion_Returns400WithBothErrors()
        {
            // Assemble
            PlaceOrderRequest request = new PlaceOrderRequestBuilder()
                .WithItems([new OrderItem { ProductId = DefaultProducts.UnknownProductId, Quantity = 1 }])
                .WithPromotionCode(DefaultPromotions.UnknownCode)
                .Build();

            // Act
            HttpResponseMessage response = await PostOrderAsync(request);

            // Assert
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(problem!.Errors.Keys, Is.EquivalentTo(new[] { nameof(PlaceOrderRequest.Items), nameof(PlaceOrderRequest.PromotionCode) }));
            }

            await PaymentGateway.DidNotReceiveWithAnyArgs().ChargeAsync(default!, default);
        }

        [TestCase(PaymentOutcome.Rejected, HttpStatusCode.PaymentRequired)]
        [TestCase(PaymentOutcome.Duplicate, HttpStatusCode.Conflict)]
        [TestCase(PaymentOutcome.ProviderError, HttpStatusCode.ServiceUnavailable)]
        public async Task PlaceOrderAsync_PaymentNotAccepted_MapsToStatusCode(PaymentOutcome outcome, HttpStatusCode expectedStatusCode)
        {
            // Assemble
            PaymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Failed(outcome, "Card declined"));

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode));
                Assert.That(problem!.Status, Is.EqualTo((int)expectedStatusCode));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_UnexpectedFailure_Returns500WithoutLeakingDetails()
        {
            // Assemble
            PaymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns<Task<PaymentResult>>(_ => throw new InvalidOperationException("secret connection string"));

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            string body = await response.Content.ReadAsStringAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(body, Does.Not.Contain("secret"));
            }
        }

        [Test]
        public async Task GetOrderAsync_FromLocationHeader_ReturnsOrderWithLineItems()
        {
            // Assemble
            HttpResponseMessage placeResponse = await PostOrderAsync();

            // Act
            GetOrderResponse? order = await HttpClient!.GetFromJsonAsync<GetOrderResponse>(placeResponse.Headers.Location, JsonOptions);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order!.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(order.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
                Assert.That(order.Items, Has.Count.EqualTo(2));
            }
        }

        [Test]
        public async Task GetOrderAsync_UnknownOrder_Returns404()
        {
            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync($"{OrderRoute}/{Guid.NewGuid()}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_AfterPlacingOrder_ReturnsOrder()
        {
            // Assemble
            HttpResponseMessage placeResponse = await PostOrderAsync();
            PlaceOrderResponse? placed = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);

            // Act
            GetCustomerOrderHistoryResponse? history = await HttpClient!.GetFromJsonAsync<GetCustomerOrderHistoryResponse>(
                $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}", JsonOptions);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(history!.Orders, Has.Count.EqualTo(1));
                Assert.That(history.Orders[0].Id, Is.EqualTo(placed!.OrderId));
            }
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_InvalidCustomerIdAsSupport_Returns400()
        {
            // Assemble
            UseToken(TestTokens.ForSupport());

            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync($"{OrderRoute}/by/customer/0");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_FollowingNextCursor_ReturnsTheNextPage()
        {
            // Assemble
            PlaceOrderResponse? oldest = await (await PostOrderAsync()).Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);
            PlaceOrderResponse? newest = await (await PostOrderAsync()).Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions);
            string historyRoute = $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}?pageSize=1";

            // Act
            GetCustomerOrderHistoryResponse? firstPage = await HttpClient!.GetFromJsonAsync<GetCustomerOrderHistoryResponse>(historyRoute, JsonOptions);
            GetCustomerOrderHistoryResponse? secondPage = await HttpClient!.GetFromJsonAsync<GetCustomerOrderHistoryResponse>(
                $"{historyRoute}&cursor={Uri.EscapeDataString(firstPage!.NextCursor!)}", JsonOptions);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstPage.Orders.Single().Id, Is.EqualTo(newest!.OrderId));
                Assert.That(firstPage.TotalCount, Is.EqualTo(2));
                Assert.That(secondPage!.Orders.Single().Id, Is.EqualTo(oldest!.OrderId));
                Assert.That(secondPage.NextCursor, Is.Null);
            }
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_InvalidCursor_Returns400()
        {
            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync($"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}?cursor=garbage");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task GetCustomerOrderHistoryAsync_PageSizeTooLarge_Returns400()
        {
            // Act
            HttpResponseMessage response = await HttpClient!.GetAsync(
                $"{OrderRoute}/by/customer/{PlaceOrderRequestBuilder.DefaultCustomerId}?pageSize={GetCustomerOrderHistoryRequest.MaxPageSize + 1}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PlaceOrderAsync_SeveralInvalidProperties_Returns400ListingEveryOne()
        {
            // Assemble
            PlaceOrderRequest request = new PlaceOrderRequestBuilder().WithCustomerEmail("not-an-email").WithItems([]).Build();

            // Act
            HttpResponseMessage response = await PostOrderAsync(request);

            // Assert
            ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(problem!.Errors.Keys, Is.SupersetOf(new[] { nameof(PlaceOrderRequest.CustomerEmail), nameof(PlaceOrderRequest.Items) }));
            }
        }

        [Test]
        public async Task PlaceOrderAsync_BrokenDomainRule_Returns500WithoutDetails()
        {
            // Assemble: validation stops every client input that could break a domain rule, so one reaching the API is a bug.
            PaymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns<PaymentResult>(_ => throw new DomainRuleException("Internal rule detail."));

            // Act
            HttpResponseMessage response = await PostOrderAsync();

            // Assert
            string body = await response.Content.ReadAsStringAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(body, Does.Not.Contain("Internal rule detail."));
            }
        }
    }
}