using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Models.Request.V1;
using Order.Application.Validators.Request.V1;

namespace Order.Application.Validators.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddValidators(this IServiceCollection services)
        {
            return services
                .AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderRequestValidator>()
                .AddScoped<IValidator<GetCustomerOrderHistoryRequest>, GetCustomerOrderHistoryRequestValidator>()
                .AddScoped<IValidator<GetOrderRequest>, GetOrderRequestValidator>();
        }
    }
}