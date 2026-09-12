using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public interface IDatabaseService
{
    Task<DatabaseResult> GetDemoAccountsAsync(CancellationToken cancellationToken = default);
}

