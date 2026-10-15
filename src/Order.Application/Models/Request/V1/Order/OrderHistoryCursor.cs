using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Order.Application.Models.Request.V1.Order
{
    /// <summary>
    /// Where a page of the order history ends: the creation date and position (the internal row id, which breaks ties) of
    /// its last order. The next page starts after it, so pages are stable even while new orders arrive. Sent to clients as
    /// an opaque URL-safe string.
    /// </summary>
    public sealed record OrderHistoryCursor
    {
        private const char Separator = ':';

        public required DateTimeOffset CreatedDate { get; init; }

        public required long Position { get; init; }

        public string Encode()
        {
            string value = string.Create(CultureInfo.InvariantCulture, $"{CreatedDate.UtcTicks}{Separator}{Position}");

            return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));
        }

        public static bool TryDecode(string? value, out OrderHistoryCursor? cursor)
        {
            cursor = null;
            if (string.IsNullOrEmpty(value) || !Base64Url.IsValid(value))
            {
                return false;
            }

            string[] parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split(Separator);
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long utcTicks)
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long position)
                || utcTicks > DateTimeOffset.MaxValue.UtcTicks
                || position <= 0)
            {
                return false;
            }

            cursor = new OrderHistoryCursor
            {
                CreatedDate = new DateTimeOffset(utcTicks, TimeSpan.Zero),
                Position = position
            };

            return true;
        }
    }
}