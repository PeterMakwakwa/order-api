namespace Order.Domain.Observability.Interfaces
{
    public interface ILogManager
    {
        void AsError(Exception? exception = null);

        void AsWarning(Exception? exception = null);

        void AsInfo();

        void AsDebug();

        void AsTrace();
    }
}