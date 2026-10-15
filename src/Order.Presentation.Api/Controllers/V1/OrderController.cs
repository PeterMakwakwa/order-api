using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Order.Application.Constants;
using Order.Application.Handlers.Abstractions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Presentation.Api.Authorization;
using Order.Presentation.Api.Controllers.Base;
using Order.Presentation.Api.Dependencies;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Order.Presentation.Api.Controllers.V1
{
    /// <summary>
    /// Places orders (command) and returns orders (queries) for the authenticated customer.
    /// Customers only see their own orders; support staff can read any customer's.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    [EnableRateLimiting(RateLimitingRegistration.OrdersPolicy)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.TooManyRequests)]
    public sealed class OrderController(IObservabilityManager observabilityManager,
        IRequestHandler<PlaceOrderRequest, PlaceOrderResponse> placeOrderHandler,
        IRequestHandler<GetOrderRequest, GetOrderResponse> getOrderHandler,
        IRequestHandler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse> getCustomerOrderHistoryHandler,
        IValidator<PlaceOrderRequest> placeOrderValidator,
        IValidator<GetOrderRequest> getOrderValidator,
        IValidator<GetCustomerOrderHistoryRequest> getCustomerOrderHistoryValidator) : BaseController(observabilityManager)
    {
        public const int IdempotencyKeyMaxLength = 100;

        private const string GetOrderRouteName = "GetOrder";
        private const string VersionRouteValue = "version";

        /// <summary>
        /// Places an order for the authenticated customer and charges it.
        /// </summary>
        /// <param name="placeOrderRequest">The items, payment method, confirmation email and optional promotion code.</param>
        /// <param name="correlationId">A unique identifier for correlating the order operation.</param>
        /// <param name="idempotencyKey">A unique key per order attempt (e.g. a GUID). Retrying with the same key never charges twice:
        /// the original result is returned with the Idempotent-Replayed header.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The placed order.</returns>
        /// <response code="201">The order was placed (Completed, or AwaitingPayment for bank transfers). Location points to the order.</response>
        /// <response code="400">The request failed validation, refers to unknown products or an invalid promotion code, or a required header is missing.</response>
        /// <response code="402">The payment provider declined the payment.</response>
        /// <response code="409">The provider reported a duplicate order, or a request with the same Idempotency-Key is still in progress.</response>
        /// <response code="422">The Idempotency-Key was already used for a different request.</response>
        /// <response code="503">The payment provider is unavailable.</response>
        [HttpPost]
        [Authorize(Policy = OrderClaims.CustomerPolicy)]
        [ProducesResponseType(typeof(PlaceOrderResponse), (int)HttpStatusCode.Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.PaymentRequired)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
        public async Task<IActionResult> PlaceOrderAsync([FromBody] PlaceOrderRequest placeOrderRequest,
            [FromHeader(Name = RequestHeaderKeys.CorrelationId)] string correlationId,
            [FromHeader(Name = RequestHeaderKeys.IdempotencyKey), Required, StringLength(IdempotencyKeyMaxLength, MinimumLength = 1)] string idempotencyKey,
            CancellationToken cancellationToken)
        {
            ObservabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            User.TryGetCustomerId(out int customerId);

            return await ProcessRequest(placeOrderValidator.ValidateAsync,
                placeOrderHandler.HandleAsync,
                placeOrderRequest with { CustomerId = customerId },
                new Dictionary<string, string>
                {
                    { RequestHeaderKeys.CorrelationId, correlationId },
                    { RequestHeaderKeys.IdempotencyKey, idempotencyKey }
                },
                BuildCreatedResult,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves one order with its line items.
        /// </summary>
        /// <param name="orderId">The unique id of the order.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The order.</returns>
        /// <response code="200">Returns the order.</response>
        /// <response code="400">The order id is invalid.</response>
        /// <response code="404">The order does not exist, or belongs to another customer.</response>
        [HttpGet("{orderId:guid}", Name = GetOrderRouteName)]
        [ProducesResponseType(typeof(GetOrderResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
        {
            ObservabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            // Another customer's order is reported as not found, so order ids cannot be probed.
            if (!User.TryGetReadableCustomerId(out int? customerId))
            {
                return NotFoundProblem(string.Format(ResponseMessages.OrderNotFound, orderId));
            }

            return await ProcessRequest(getOrderValidator.ValidateAsync,
                getOrderHandler.HandleAsync,
                new GetOrderRequest
                {
                    OrderId = orderId,
                    CustomerId = customerId
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Retrieves one page of a customer's order history, newest first.
        /// </summary>
        /// <param name="customerId">The customer whose orders are returned: the caller's own id, unless the caller is support staff.</param>
        /// <param name="cursor">The nextCursor of the previous page; omit for the first (newest) page.</param>
        /// <param name="pageSize">The number of orders per page (1 to 100).</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>One page of the customer's orders, with the total count and the cursor of the next page.</returns>
        /// <response code="200">Returns the page (empty if the customer has no orders).</response>
        /// <response code="400">The customer id, cursor or page size is invalid.</response>
        [HttpGet("by/customer/{customerId:int}")]
        [ProducesResponseType(typeof(GetCustomerOrderHistoryResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetCustomerOrderHistoryAsync(int customerId,
            [FromQuery] string? cursor = null,
            [FromQuery] int pageSize = GetCustomerOrderHistoryRequest.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            ObservabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            if (!User.CanAccessCustomer(customerId))
            {
                return Forbid();
            }

            return await ProcessRequest(getCustomerOrderHistoryValidator.ValidateAsync,
                getCustomerOrderHistoryHandler.HandleAsync,
                new GetCustomerOrderHistoryRequest
                {
                    CustomerId = customerId,
                    Cursor = cursor,
                    PageSize = pageSize
                },
                cancellationToken: cancellationToken);
        }

        private CreatedAtRouteResult BuildCreatedResult(PlaceOrderResponse response)
        {
            if (response.IsReplay)
            {
                Response.Headers[ResponseHeaderKeys.IdempotentReplayed] = bool.TrueString.ToLowerInvariant();
            }

            return CreatedAtRoute(GetOrderRouteName, new
            {
                version = RouteData.Values[VersionRouteValue],
                orderId = response.OrderId
            }, response);
        }
    }
}