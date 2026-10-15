using Asp.Versioning;
using Order.Presentation.Api.OpenApi;
using System.Text.Json.Serialization;

namespace Order.Presentation.Api.Dependencies
{
    public static class ControllerRegistration
    {
        public static void Register(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.ReportApiVersions = true;
                    options.ApiVersionReader = new UrlSegmentApiVersionReader();
                })
                .AddMvc()
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                })
                .AddOpenApi(options => OpenApiTransformers.Configure(options.Document));
        }
    }
}