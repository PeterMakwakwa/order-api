namespace Order.Application.Handlers.Abstractions
{
    /// <summary>
    /// Handles one request type. Headers carry cross-cutting values such as the correlation id.
    /// </summary>
    public interface IRequestHandler<T, K>
        where T : class
        where K : class
    {
        public Task<K> HandleAsync(T request, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
    }
}