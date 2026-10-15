using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Order.Application.Constants;

namespace Order.Presentation.Api.OpenApi
{
    /// <summary>
    /// Titles the OpenAPI document, documents the required CorrelationId header on every operation and the bearer token
    /// on every operation that requires authorization, so clients generated from the document send both.
    /// </summary>
    internal static class OpenApiTransformers
    {
        private const string ApiTitle = "Orders API";
        private const string CorrelationIdDescription = "Correlation Id";
        private const string BearerSchemeName = "Bearer";
        private const string BearerDescription = "JWT access token with a customer_id claim (and the Support role to read other customers' orders).";

        public static void Configure(OpenApiOptions options)
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = ApiTitle;
                document.Info.Description = $"API version {document.Info.Version}";

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = BearerDescription
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                AddCorrelationIdHeader(operation);

                IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = []
                    });
                }

                return Task.CompletedTask;
            });
        }

        private static void AddCorrelationIdHeader(OpenApiOperation operation)
        {
            operation.Parameters ??= [];

            bool alreadyDocumented = operation.Parameters.Any(parameter => parameter.In == ParameterLocation.Header
                && string.Equals(parameter.Name, RequestHeaderKeys.CorrelationId, StringComparison.OrdinalIgnoreCase));

            if (!alreadyDocumented)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = RequestHeaderKeys.CorrelationId,
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = CorrelationIdDescription,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
        }
    }
}