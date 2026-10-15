namespace Order.Application.Constants
{
    public static class HealthCheckTags
    {
        /// <summary>
        /// Checks that must pass before the instance takes traffic (/healthz/ready). Liveness (/healthz/live) runs no checks.
        /// </summary>
        public const string Ready = "ready";
    }
}