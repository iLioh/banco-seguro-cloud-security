using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BancoSeguro.Web.Pages;

public sealed class DatabaseModel(IDatabaseService databaseService) : PageModel
{
    public DatabaseResult Result { get; private set; } = DatabaseResult.WithoutData(new(ServiceState.NotConfigured, "Azure SQL no configurado"));

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Result = await databaseService.GetDemoAccountsAsync(cancellationToken);
    }
}

