using Microsoft.Extensions.Logging;
using Order.Domain.Observability.Interfaces;

namespace Order.Infrastructure.Observability.Managers
{
    public sealed class LogManager(ILogger<LogManager> logger, string className, string memberName, string message) : ILogManager
    {
        private const string LogMessageTemplate = "{ClassName}.{CallerMemberName}: {Message}";

        public void AsError(Exception? exception = null)
        {
            logger.LogError(exception, LogMessageTemplate, className, memberName, message);
        }

        public void AsWarning(Exception? exception = null)
        {
            logger.LogWarning(exception, LogMessageTemplate, className, memberName, message);
        }

        public void AsInfo()
        {
            logger.LogInformation(LogMessageTemplate, className, memberName, message);
        }

        public void AsDebug()
        {
            logger.LogDebug(LogMessageTemplate, className, memberName, message);
        }

        public void AsTrace()
        {
            logger.LogTrace(LogMessageTemplate, className, memberName, message);
        }
    }
}