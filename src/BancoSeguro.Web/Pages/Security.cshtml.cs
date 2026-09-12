using BancoSeguro.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BancoSeguro.Web.Pages;

public sealed class SecurityModel(IKeyVaultService keyVaultService) : PageModel
{
    public bool TokenAvailable { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TokenAvailable = await keyVaultService.GetInternalApiTokenAsync(cancellationToken) is not null;
    }
}

