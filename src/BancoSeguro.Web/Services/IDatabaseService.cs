using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public interface IDatabaseService
{
    Task<DatabaseResult> GetDemoAccountsAsync(CancellationToken cancellationToken = default);
    Task<BankingSummaryResult> GetCustomerSummaryAsync(CancellationToken cancellationToken = default);
    Task<MovementsResult> GetCustomerMovementsAsync(CancellationToken cancellationToken = default);
}
