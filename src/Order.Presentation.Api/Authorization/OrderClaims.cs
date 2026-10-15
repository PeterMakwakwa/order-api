namespace Order.Presentation.Api.Authorization
{
    /// <summary>
    /// The access-token claims and roles the API relies on. Inbound claim mapping is off, so these are the raw JWT names.
    /// </summary>
    public static class OrderClaims
    {
        /// <summary>
        /// The customer the caller acts as. Orders are always placed for, and read by, this customer.
        /// </summary>
        public const string CustomerId = "customer_id";

        public const string Subject = "sub";

        public const string Role = "role";

        /// <summary>
        /// Support staff can read any customer's orders, but cannot place orders for them.
        /// </summary>
        public const string SupportRole = "Support";

        /// <summary>
        /// Requires a token with a valid <see cref="CustomerId"/>; used to place orders.
        /// </summary>
        public const string CustomerPolicy = "Customer";
    }
}