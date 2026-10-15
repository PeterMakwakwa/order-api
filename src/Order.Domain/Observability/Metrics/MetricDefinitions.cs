using Order.Domain.Observability.Models.Metrics.Definitions;

namespace Order.Domain.Observability.Metrics
{
    public static class MetricDefinitions
    {
        static MetricDefinitions()
        {
            HealthCheckSuccessCount = new CounterMetricDefinition("healthcheck.success.count");
            HealthCheckErrorCount = new CounterMetricDefinition("healthcheck.error.count");

            ApiRequestCount = new CounterMetricDefinition("api.request.count");
            ApiRequestDuration = new HistogramMetricDefinition("api.request.duration.ms");
            ApiRateLimitRejectedCount = new CounterMetricDefinition("api.ratelimit.rejected.count");

            PlaceOrderRequestSuccessCount = new CounterMetricDefinition("place.order.success.count");
            PlaceOrderRequestFailedCount = new CounterMetricDefinition("place.order.failed.count");
            PlaceOrderRequestRejectedCount = new CounterMetricDefinition("place.order.rejected.count");
            PlaceOrderRequestReplayedCount = new CounterMetricDefinition("place.order.replayed.count");
            GetOrderRequestSuccessCount = new CounterMetricDefinition("get.order.success.count");
            GetOrderRequestFailedCount = new CounterMetricDefinition("get.order.failed.count");

            GetCustomerOrderHistoryRequestSuccessCount = new CounterMetricDefinition("get.customer.order.history.success.count");
            GetCustomerOrderHistoryRequestFailedCount = new CounterMetricDefinition("get.customer.order.history.failed.count");

            OrderTotalAmount = new CounterMetricDefinition("order.total.amount.cents");
            OrderLineItemCount = new HistogramMetricDefinition("order.lineitem.count");

            PaymentChargeCount = new CounterMetricDefinition("payment.charge.count");
            PaymentChargeDuration = new HistogramMetricDefinition("payment.charge.duration.ms");
            PaymentGatewayRetryCount = new CounterMetricDefinition("payment.gateway.retry.count");
            PaymentGatewayCircuitOpenedCount = new CounterMetricDefinition("payment.gateway.circuit.opened.count");
            PaymentReconciledCount = new CounterMetricDefinition("payment.reconciled.count");

            OutboxMessageProcessedCount = new CounterMetricDefinition("outbox.message.processed.count");
            OutboxMessageFailedCount = new CounterMetricDefinition("outbox.message.failed.count");
            OutboxMessageClaimConflictCount = new CounterMetricDefinition("outbox.message.claim.conflict.count");
            OutboxPendingMessageCount = new GaugeMetricDefinition("outbox.message.pending.count");

            EmailSentCount = new CounterMetricDefinition("email.sent.count");
            EmailFailedCount = new CounterMetricDefinition("email.failed.count");
            EmailRetryCount = new CounterMetricDefinition("email.retry.count");
        }

        public static CounterMetricDefinition HealthCheckSuccessCount { get; }
        public static CounterMetricDefinition HealthCheckErrorCount { get; }

        public static CounterMetricDefinition ApiRequestCount { get; }
        public static HistogramMetricDefinition ApiRequestDuration { get; }

        public static CounterMetricDefinition ApiRateLimitRejectedCount { get; }

        public static CounterMetricDefinition PlaceOrderRequestSuccessCount { get; }
        public static CounterMetricDefinition PlaceOrderRequestFailedCount { get; }
        public static CounterMetricDefinition PlaceOrderRequestRejectedCount { get; }

        public static CounterMetricDefinition PlaceOrderRequestReplayedCount { get; }

        public static CounterMetricDefinition GetOrderRequestSuccessCount { get; }

        public static CounterMetricDefinition GetOrderRequestFailedCount { get; }

        public static CounterMetricDefinition GetCustomerOrderHistoryRequestSuccessCount { get; }
        public static CounterMetricDefinition GetCustomerOrderHistoryRequestFailedCount { get; }

        public static CounterMetricDefinition OrderTotalAmount { get; }
        public static HistogramMetricDefinition OrderLineItemCount { get; }

        public static CounterMetricDefinition PaymentChargeCount { get; }
        public static HistogramMetricDefinition PaymentChargeDuration { get; }
        public static CounterMetricDefinition PaymentGatewayRetryCount { get; }
        public static CounterMetricDefinition PaymentGatewayCircuitOpenedCount { get; }
        public static CounterMetricDefinition PaymentReconciledCount { get; }

        public static CounterMetricDefinition OutboxMessageProcessedCount { get; }
        public static CounterMetricDefinition OutboxMessageFailedCount { get; }

        public static CounterMetricDefinition OutboxMessageClaimConflictCount { get; }
        public static GaugeMetricDefinition OutboxPendingMessageCount { get; }

        public static CounterMetricDefinition EmailSentCount { get; }
        public static CounterMetricDefinition EmailFailedCount { get; }
        public static CounterMetricDefinition EmailRetryCount { get; }
    }
}