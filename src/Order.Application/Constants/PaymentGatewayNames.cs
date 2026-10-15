namespace Order.Application.Constants
{
    /// <summary>
    /// Keys of the ApplicationConfiguration:PaymentGateways section, also used as metric dimensions.
    /// </summary>
    public static class PaymentGatewayNames
    {
        public const string Card = "Card";

        public const string PayPal = "PayPal";

        public const string BankTransfer = "BankTransfer";
    }
}