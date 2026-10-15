using Microsoft.AspNetCore.Authentication.JwtBearer;
using Order.Presentation.Api.Authorization;

namespace Order.Presentation.Api.Dependencies
{
    public static class AuthenticationRegistration
    {
        /// <summary>
        /// JWT bearer tokens, configured from Authentication:Schemes:Bearer (Authority and ValidAudiences in deployed
        /// environments; issuer and signing keys from dotnet user-jwts locally). Issuer, audience, lifetime and signature
        /// are always validated, so a missing setting rejects every token instead of accepting any.
        /// Empty 401/403/404 responses get problem details bodies like every other error.
        /// </summary>
        public static void Register(IServiceCollection services)
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = OrderClaims.Subject;
                options.TokenValidationParameters.RoleClaimType = OrderClaims.Role;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            });

            services
                .AddAuthorizationBuilder()
                .AddPolicy(OrderClaims.CustomerPolicy, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => context.User.TryGetCustomerId(out _)));

            services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path);
        }
    }
}