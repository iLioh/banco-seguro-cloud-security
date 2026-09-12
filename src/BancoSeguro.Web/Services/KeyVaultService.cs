using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using BancoSeguro.Web.Models;

namespace BancoSeguro.Web.Services;

public interface IKeyVaultSecretClient
{
    Task<string?> GetSecretValueAsync(Uri vaultUri, string secretName, CancellationToken cancellationToken);
}

public sealed class AzureKeyVaultSecretClient(TokenCredential credential) : IKeyVaultSecretClient
{
    public async Task<string?> GetSecretValueAsync(Uri vaultUri, string secretName, CancellationToken cancellationToken)
    {
        var client = new SecretClient(vaultUri, credential);
        var response = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return response.Value.Value;
    }
}

public sealed class KeyVaultService(
    IConfiguration configuration,
    IKeyVaultSecretClient secretClient,
    ILogger<KeyVaultService> logger) : IKeyVaultService
{
    private const string SecretName = "InternalApiToken";

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetInternalApiTokenAsync(cancellationToken);
        if (token is not null)
        {
            return new(ServiceState.Connected, "InternalApiToken disponible: Sí");
        }

        return HasValidVaultUri(out _)
            ? new(ServiceState.Error, "InternalApiToken disponible: No")
            : new(ServiceState.NotConfigured, "Key Vault no configurado");
    }

    public async Task<string?> GetInternalApiTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!HasValidVaultUri(out var vaultUri))
        {
            return null;
        }

        try
        {
            var value = await secretClient.GetSecretValueAsync(vaultUri, SecretName, cancellationToken);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("No se pudo consultar el secreto de Key Vault. Tipo de error: {ErrorType}", exception.GetType().Name);
            return null;
        }
    }

    private bool HasValidVaultUri(out Uri vaultUri)
    {
        var configuredUri = configuration["Azure:KeyVaultUri"];
        return Uri.TryCreate(configuredUri, UriKind.Absolute, out vaultUri!) && vaultUri.Scheme == Uri.UriSchemeHttps;
    }
}

