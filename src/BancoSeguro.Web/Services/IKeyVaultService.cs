using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public interface IKeyVaultService
{
    Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<string?> GetInternalApiTokenAsync(CancellationToken cancellationToken = default);
}

