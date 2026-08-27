using Come.Models;

namespace Come.Services;
public interface IPaymentService
{
    Task<PaymentReceipt> PayAsync(decimal amount, string paymentMethod, CancellationToken cancellationToken = default);
}
