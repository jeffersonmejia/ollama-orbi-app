using Microsoft.EntityFrameworkCore;

namespace OrbiApp.Data;

/// <summary>Serializes payment processing for the same database payment record.</summary>
public static class PaymentIdempotencyLock
{
    public static Task AcquirePaymentLockAsync(this ApplicationDbContext context, string scope, long paymentId)
    {
        var lockKey = $"{scope}:{paymentId}";
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
    }
}
