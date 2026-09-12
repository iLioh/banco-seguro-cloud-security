using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BancoSeguro.Web.Pages;

public sealed class MovementsModel(IDatabaseService databaseService) : PageModel
{
    public MovementsResult Result { get; private set; } =
        MovementsResult.WithoutData(new(ServiceState.NotConfigured, "Movimientos no disponibles en este entorno"));

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Result = await databaseService.GetCustomerMovementsAsync(cancellationToken);
    }

    public static string MaskAccount(int accountId) => $"••••{accountId % 10000:D4}";

    public static string FormatAmount(decimal amount, string currency) =>
        $"{(currency == "PEN" ? "S/" : currency == "USD" ? "US$" : currency)} {amount:N2}";
}
