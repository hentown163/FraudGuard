using System.Diagnostics;

namespace GlobalPaymentFraudDetection.Infrastructure;

public static class TracingExtensions
{
    public static Activity? StartFraudDetectionActivity(this ActivitySource source, string operationName, string? transactionId = null)
    {
        var activity = source.StartActivity(operationName, ActivityKind.Internal);
        
        if (activity != null && !string.IsNullOrEmpty(transactionId))
        {
            activity.SetTag(DistributedTracing.Tags.TransactionId, transactionId);
        }
        
        return activity;
    }

    public static Activity? AddFraudTags(this Activity? activity, string userId, decimal amount, double fraudScore)
    {
        if (activity == null) return null;

        activity.SetTag(DistributedTracing.Tags.UserId, userId);
        activity.SetTag(DistributedTracing.Tags.Amount, amount);
        activity.SetTag(DistributedTracing.Tags.FraudScore, fraudScore);

        return activity;
    }

    public static Activity? AddDecisionTag(this Activity? activity, string decision, string riskLevel)
    {
        if (activity == null) return null;

        activity.SetTag(DistributedTracing.Tags.Decision, decision);
        activity.SetTag(DistributedTracing.Tags.RiskLevel, riskLevel);

        return activity;
    }

    public static Activity? RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null) return null;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.RecordException(exception);

        return activity;
    }

    public static void RecordFraudEvent(this Activity? activity, string eventName, Dictionary<string, object>? attributes = null)
    {
        if (activity == null) return;

        var tags = new ActivityTagsCollection();
        
        if (attributes != null)
        {
            foreach (var attr in attributes)
            {
                tags.Add(attr.Key, attr.Value);
            }
        }

        activity.AddEvent(new ActivityEvent(eventName, tags: tags));
    }
}
