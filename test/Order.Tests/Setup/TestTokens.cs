using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Order.Presentation.Api.Authorization;
using System.Globalization;
using System.Security.Cryptography;

namespace Order.Tests.Setup
{
    /// <summary>
    /// Issues real JWTs for the API under test. The signing key is random per test run, so no key is ever committed;
    /// the API validates the tokens through the same Authentication:Schemes:Bearer settings used in every environment.
    /// </summary>
    public static class TestTokens
    {
        public const string Issuer = "order-tests";
        public const string Audience = "order-api-tests";

        private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

        public static IDictionary<string, string?> AuthenticationSettings { get; } = new Dictionary<string, string?>
        {
            ["Authentication:Schemes:Bearer:ValidIssuer"] = Issuer,
            ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
            ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = Issuer,
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(SigningKey),
            ["Authentication:Schemes:Bearer:SigningKeys:0:Length"] = SigningKey.Length.ToString(CultureInfo.InvariantCulture)
        };

        public static string ForCustomer(int customerId)
        {
            return Create(customerId);
        }

        public static string ForSupport()
        {
            return Create(customerId: null, roles: [OrderClaims.SupportRole]);
        }

        /// <param name="customerId">The customer_id claim; omitted when null.</param>
        /// <param name="roles">Role claims.</param>
        /// <param name="signingKey">Defaults to the key the API trusts; pass another to get an invalid signature.</param>
        /// <param name="expires">Defaults to one hour from now.</param>
        public static string Create(int? customerId, string[]? roles = null, byte[]? signingKey = null, DateTime? expires = null)
        {
            Dictionary<string, object> claims = new() { [OrderClaims.Subject] = customerId?.ToString(CultureInfo.InvariantCulture) ?? "support-user" };
            if (customerId != null)
            {
                claims[OrderClaims.CustomerId] = customerId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (roles is { Length: > 0 })
            {
                claims[OrderClaims.Role] = roles;
            }

            DateTime expiry = expires ?? DateTime.UtcNow.AddHours(1);

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Claims = claims,
                NotBefore = expiry.AddHours(-2),
                Expires = expiry,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(signingKey ?? SigningKey), SecurityAlgorithms.HmacSha256)
            });
        }
    }
}