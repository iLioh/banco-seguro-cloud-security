using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BancoSeguro.Web.Pages;

public sealed class SecurityModel(
    IKeyVaultService keyVaultService,
    IDatabaseService databaseService,
    IInternalApiService internalApiService) : PageModel
{
    public ServiceStatus ApplicationStatus { get; private set; } = new(ServiceState.Running, "Aplicación operativa");
    public ServiceStatus IdentityStatus { get; private set; } = new(ServiceState.NotAvailable, "Managed Identity no disponible en este entorno");
    public ServiceStatus KeyVaultStatus { get; private set; } = new(ServiceState.NotConfigured, "Key Vault no configurado");
    public ServiceStatus TokenStatus { get; private set; } = new(ServiceState.NotAvailable, "InternalApiToken disponible: No");
    public ServiceStatus DatabaseStatus { get; private set; } = new(ServiceState.NotConfigured, "Azure SQL no configurado");
    public ServiceStatus InternalApiStatus { get; private set; } = new(ServiceState.NotConfigured, "API interna no configurada");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IdentityStatus = HasManagedIdentity()
            ? new(ServiceState.Configured, "System-Assigned Managed Identity configurada")
            : new(ServiceState.NotAvailable, "Managed Identity no disponible en este entorno");

        var keyVaultTask = keyVaultService.GetStatusAsync(cancellationToken);
        var databaseTask = databaseService.GetDemoAccountsAsync(cancellationToken);
        var internalApiTask = internalApiService.GetStatusAsync(cancellationToken);
        await Task.WhenAll(keyVaultTask, databaseTask, internalApiTask);

        KeyVaultStatus = await keyVaultTask;
        TokenStatus = KeyVaultStatus.IsSuccess
            ? new(ServiceState.Available, "InternalApiToken disponible: Sí")
            : new(ServiceState.NotAvailable, "InternalApiToken disponible: No");
        DatabaseStatus = (await databaseTask).Status;
        InternalApiStatus = await internalApiTask;
    }

    private static bool HasManagedIdentity() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MSI_ENDPOINT"));
}
