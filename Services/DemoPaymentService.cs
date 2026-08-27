using Come.Models;

namespace Come.Services;

public sealed class DemoPaymentService : IPaymentService
{
    public async Task<PaymentReceipt> PayAsync(decimal amount, string paymentMethod, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2200, cancellationToken);
        var stamp = DateTime.Now;
        return new PaymentReceipt($"COME-{stamp:yyMMdd}-{Random.Shared.Next(1000, 9999)}", $"{Random.Shared.Next(10000000, 99999999)}", stamp);
    }
}
