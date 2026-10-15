using Order.Domain.Observability.Messages;

namespace Order.Application.Helpers
{
    public static class DictionaryExtensions
    {
        public static TK GetRequiredValue<TK>(this Dictionary<string, TK>? dictionary, string key)
        {
            return dictionary != null && dictionary.TryGetValue(key, out TK? value)
                ? value
                : throw new KeyNotFoundException(string.Format(ErrorMessages.DictionaryKeyNotFoundErrorMessage, key));
        }
    }
}