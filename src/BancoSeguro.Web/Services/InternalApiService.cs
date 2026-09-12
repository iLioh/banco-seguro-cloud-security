using System.Net.Http.Headers;
using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public sealed class InternalApiService(
    HttpClient httpClient,
    IConfiguration configuration,
    IKeyVaultService keyVaultService,
    ILogger<InternalApiService> logger) : IInternalApiService
{
    public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configuredUrl = configuration["InternalApi:BaseUrl"];
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return new(ServiceState.NotConfigured, "API interna no configurada");
        }

        var token = await keyVaultService.GetInternalApiTokenAsync(cancellationToken);
        if (token is null)
        {
            return new(ServiceState.NotAvailable, "Token de acceso no disponible");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "health"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode
                ? new(ServiceState.Available, "API interna disponible")
                : new(ServiceState.Error, "API interna no disponible");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("No se pudo consultar la API interna. Tipo de error: {ErrorType}", exception.GetType().Name);
            return new(ServiceState.Error, "API interna no disponible");
        }
    }
}

