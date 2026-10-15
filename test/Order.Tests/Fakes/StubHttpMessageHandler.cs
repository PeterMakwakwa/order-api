using System.Net;

namespace Order.Tests.Fakes
{
    /// <summary>
    /// Returns the queued status codes in order (the last one repeats) and records each request.
    /// </summary>
    public sealed class StubHttpMessageHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statusCodes = new(statusCodes);

        public List<HttpRequestMessage> Requests { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            HttpStatusCode statusCode = _statusCodes.Count > 1 ? _statusCodes.Dequeue() : _statusCodes.Peek();

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(statusCode == HttpStatusCode.BadRequest ? "Card declined" : string.Empty)
            });
        }
    }
}