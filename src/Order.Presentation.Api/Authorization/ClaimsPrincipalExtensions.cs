using System.Globalization;
using System.Security.Claims;

namespace Order.Presentation.Api.Authorization
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool TryGetCustomerId(this ClaimsPrincipal user, out int customerId)
        {
            customerId = 0;
            string? value = user.FindFirst(OrderClaims.CustomerId)?.Value;

            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out customerId) && customerId > 0;
        }

        /// <summary>
        /// Whose orders the caller may read: null for support staff (anyone's), otherwise the caller's own customer id.
        /// False if the caller is neither.
        /// </summary>
        public static bool TryGetReadableCustomerId(this ClaimsPrincipal user, out int? customerId)
        {
            customerId = null;
            if (user.IsInRole(OrderClaims.SupportRole))
            {
                return true;
            }

            if (user.TryGetCustomerId(out int callerCustomerId))
            {
                customerId = callerCustomerId;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The caller is this customer, or support staff.
        /// </summary>
        public static bool CanAccessCustomer(this ClaimsPrincipal user, int customerId)
        {
            return user.IsInRole(OrderClaims.SupportRole) || (user.TryGetCustomerId(out int callerCustomerId) && callerCustomerId == customerId);
        }
    }
}