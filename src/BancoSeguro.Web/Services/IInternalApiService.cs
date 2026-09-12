using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public interface IInternalApiService
{
    Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

