using System.Security.Cryptography;
using System.Text.Json;

namespace Order.Application.Helpers
{
    public static class RequestHashHelper
    {
        /// <summary>
        /// SHA-256 of the request's JSON, as 64 lower-case hex characters. Used to tell a genuine retry (same key,
        /// same request) from a reused idempotency key (same key, different request).
        /// </summary>
        public static string Compute<T>(T request)
        {
            return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        }
    }
}