using Order.Domain.Commands;

namespace Order.Tests.Setup
{
    public static class DefaultProducts
    {
        public static readonly CreateProduct Keyboard = new() { Id = 1, Name = "Keyboard", Price = 49.99m };

        public static readonly CreateProduct Mouse = new() { Id = 2, Name = "Mouse", Price = 19.99m };

        public static readonly CreateProduct Monitor = new() { Id = 3, Name = "Monitor", Price = 199.00m };

        public const int UnknownProductId = 999;
    }
}