namespace Order.Application.Helpers
{
    public static class EnvironmentHelper
    {
        public const string TestModeVariableName = "IS_TEST";

        public const string LocalhostEnvironment = "Localhost";

        /// <summary>
        /// Test mode swaps SQL Server for an in-memory database and does not start the outbox poller.
        /// </summary>
        public static bool IsTestMode()
        {
            return string.Equals(Environment.GetEnvironmentVariable(TestModeVariableName), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }
    }
}