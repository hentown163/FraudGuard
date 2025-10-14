using System.Diagnostics;

namespace GlobalPaymentFraudDetection.Infrastructure;

public static class DistributedTracing
{
    public static readonly ActivitySource ActivitySource = new ActivitySource("GlobalPaymentFraudDetection");
    
    public const string ServiceName = "GlobalPaymentFraudDetection";
    public const string ServiceVersion = "1.0.0";

    public static class Tags
    {
        public const string UserId = "fraud.user_id";
        public const string TransactionId = "fraud.transaction_id";
        public const string FraudScore = "fraud.score";
        public const string Decision = "fraud.decision";
        public const string Amount = "fraud.amount";
        public const string PaymentGateway = "fraud.payment_gateway";
        public const string RiskLevel = "fraud.risk_level";
    }

    public static class Events
    {
        public const string FraudDetected = "fraud.detected";
        public const string ManualReviewTriggered = "fraud.manual_review";
        public const string AlertSent = "fraud.alert_sent";
        public const string ModelInferenceFailed = "ml.inference_failed";
    }
}
