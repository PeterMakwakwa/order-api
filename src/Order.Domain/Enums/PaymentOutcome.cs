namespace Order.Domain.Enums
{
    /// <summary>
    /// Provider responses: 202 = success, 409 = duplicate order, 400 = bad request, 500 = server error.
    /// Bank transfers don't call a provider and come back as <see cref="Pending"/>.
    /// </summary>
    public enum PaymentOutcome
    {
        Succeeded,
        Pending,
        Duplicate,
        Rejected,
        ProviderError
    }
}