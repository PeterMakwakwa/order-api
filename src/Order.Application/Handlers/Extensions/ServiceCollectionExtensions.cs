using Microsoft.Extensions.DependencyInjection;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Outbox;
using Order.Application.Handlers.Payments;
using Order.Application.Handlers.Reconciliation;
using Order.Application.Handlers.Request.V1.Commands;
using Order.Application.Handlers.Request.V1.Queries;
using Order.Application.Helpers;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;

namespace Order.Application.Handlers.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHandlers(this IServiceCollection services)
        {
            return services
                .AddTransient<OrderPaymentProcessor>()
                .AddTransient<IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>, PlaceOrderRequestHandler>()
                .AddTransient<IRequestHandler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse>, GetCustomerOrderHistoryRequestHandler>()
                .AddTransient<IRequestHandler<GetOrderRequest, GetOrderResponse>, GetOrderRequestHandler>()
                .AddHostedServices();
        }

        private static IServiceCollection AddHostedServices(this IServiceCollection services)
        {
            // Tests drive the outbox and reconciliation handlers directly instead of letting them poll in the background.
            if (EnvironmentHelper.IsTestMode())
            {
                return services;
            }

            return services
                .AddHostedService<OrderPlacedOutboxMessageHandler>()
                .AddHostedService<StalledPaymentReconciliationHandler>();
        }
    }
}