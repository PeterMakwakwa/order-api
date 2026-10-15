using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Order.Application.Constants;
using Order.Application.Models.Request.V1;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Presentation.Api;
using Order.Tests.Builders;
using Order.Tests.Setup;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Order.Tests.UnitTests.Presentation.Controllers.Base
{
    /// <summary>
    /// Hosts the real API (middleware, authentication, controllers, validators, handlers) on an in-memory database,
    /// with the payment gateway substituted. Requests carry a CorrelationId and a token for customer 42 by default.
    /// </summary>
    public abstract class ApiUnitTestBase
    {
        protected const string OrderRoute = "api/v1/order";

        protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private WebApplicationFactory<Program>? _factory;

        protected IPaymentGateway PaymentGateway { get; private set; } = Substitute.For<IPaymentGateway>();

        protected HttpClient? HttpClient { get; private set; }

        protected string CorrelationId { get; private set; } = string.Empty;

        /// <summary>
        /// Configuration overrides for the API under test.
        /// </summary>
        protected virtual IDictionary<string, string?>? Settings => null;

        [SetUp]
        public async Task InitializeAsync()
        {
            PaymentGateway = Substitute.For<IPaymentGateway>();
            PaymentGateway.Name.Returns(PaymentGatewayNames.Card);
            PaymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Succeeded());
            IPaymentGatewayResolver paymentGatewayResolver = Substitute.For<IPaymentGatewayResolver>();
            paymentGatewayResolver.Resolve(Arg.Any<PaymentMethods>()).Returns(PaymentGateway);

            _factory = UnitTestSetup.SetupApplicationFactory(services => services.AddTransient(_ => paymentGatewayResolver), Settings);
            HttpClient = _factory.CreateClient();
            await DefaultDataSetup.AddDefaultDataAsync(_factory.Services);

            CorrelationId = Guid.NewGuid().ToString("N");
            HttpClient.DefaultRequestHeaders.Add(RequestHeaderKeys.CorrelationId, CorrelationId);
            UseToken(TestTokens.ForCustomer(PlaceOrderRequestBuilder.DefaultCustomerId));
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            HttpClient?.Dispose();

            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }
        }

        /// <summary>
        /// Sends subsequent requests with this bearer token, or without one when null.
        /// </summary>
        protected void UseToken(string? token)
        {
            HttpClient!.DefaultRequestHeaders.Authorization = token == null ? null : new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// POSTs an order with the given Idempotency-Key (a new one when null; none when <paramref name="sendIdempotencyKey"/> is false).
        /// </summary>
        protected async Task<HttpResponseMessage> PostOrderAsync(PlaceOrderRequest? request = null, string? idempotencyKey = null, bool sendIdempotencyKey = true)
        {
            using (HttpRequestMessage requestMessage = new(HttpMethod.Post, OrderRoute)
            {
                Content = JsonContent.Create(request ?? new PlaceOrderRequestBuilder().Build(), options: JsonOptions)
            })
            {
                if (sendIdempotencyKey)
                {
                    requestMessage.Headers.Add(RequestHeaderKeys.IdempotencyKey, idempotencyKey ?? Guid.NewGuid().ToString());
                }

                return await HttpClient!.SendAsync(requestMessage);
            }
        }
    }
}