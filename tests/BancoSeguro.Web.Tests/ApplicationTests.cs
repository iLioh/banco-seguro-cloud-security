using System.Net;
using System.Text.Json;
using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BancoSeguro.Web.Tests;

public sealed class ApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
        });
    }

    [Fact]
    public async Task Health_Returns200AndHealthyPayload()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HomePage_PresentsBancoSeguroDigitalEntry()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Banco Seguro Digital", html);
        Assert.Contains("Ingresar a mi banca", html);
    }

    [Fact]
    public async Task Dashboard_WithMissingAzureConfiguration_ShowsControlledEmptyState()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/dashboard");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Resumen bancario", html);
        Assert.Contains("Azure SQL no configurado", html);
        Assert.DoesNotContain("Exception", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Movements_WithMissingAzureConfiguration_ShowsControlledEmptyState()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/movements");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Movimientos", html);
        Assert.Contains("Azure SQL no configurado", html);
    }

    [Fact]
    public async Task SecurityPage_ShowsTechnicalStatesWithoutBreakingWhenAzureIsMissing()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/security");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Managed Identity", html);
        Assert.Contains("Azure Key Vault", html);
        Assert.Contains("Azure SQL", html);
        Assert.Contains("API interna no configurada", html);
    }

    [Fact]
    public async Task Dashboard_RendersCustomerAccountsAndRecentMovementsFromService()
    {
        await using var factory = WithDatabaseService(CreateAvailableDatabaseService());
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/dashboard"));

        Assert.Contains("Hola, Cliente Demo Andino", html);
        Assert.Contains("S/ 8,450.75", html);
        Assert.Contains("Cuenta de Ahorros", html);
        Assert.Contains("••••1001", html);
        Assert.Contains("Depósito demo", html);
    }

    [Fact]
    public async Task Movements_RendersMockedDataInDescendingDateOrder()
    {
        await using var factory = WithDatabaseService(CreateAvailableDatabaseService());
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/movements"));

        Assert.Contains("Depósito demo", html);
        Assert.Contains("Pago demo", html);
        Assert.Contains("S/ 500.00", html);
        Assert.True(html.IndexOf("Depósito demo", StringComparison.Ordinal) < html.IndexOf("Pago demo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SqlFailure_DoesNotBreakCustomerPagesOrExposeDetails()
    {
        var sensitiveDetail = $"sensitive-{Guid.NewGuid():N}";
        var status = new ServiceStatus(ServiceState.Error, "Información bancaria temporalmente no disponible");
        var databaseService = new StubDatabaseService(
            BankingSummaryResult.WithoutData(status),
            MovementsResult.WithoutData(status),
            DatabaseResult.WithoutData(status));
        await using var factory = WithDatabaseService(databaseService);
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/dashboard", "/movements" })
        {
            var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(sensitiveDetail, html, StringComparison.Ordinal);
            Assert.DoesNotContain("stack trace", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task KeyVaultFailure_DoesNotBreakSecurityPageOrExposeDetails()
    {
        var sensitiveDetail = $"sensitive-{Guid.NewGuid():N}";
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IKeyVaultService>(_ => new FailedKeyVaultService(sensitiveDetail))));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/security");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No disponible", html);
        Assert.DoesNotContain(sensitiveDetail, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoPublicPage_ReturnsInternalApiTokenValue()
    {
        var sensitiveToken = $"sensitive-{Guid.NewGuid():N}";
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IKeyVaultService>(_ => new AvailableKeyVaultService(sensitiveToken))));
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/", "/dashboard", "/movements", "/security", "/database", "/health" })
        {
            var html = await client.GetStringAsync(path);
            Assert.DoesNotContain(sensitiveToken, html, StringComparison.Ordinal);
        }
    }

    private WebApplicationFactory<Program> WithDatabaseService(IDatabaseService service) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IDatabaseService>(_ => service)));

    private static IDatabaseService CreateAvailableDatabaseService()
    {
        var older = new MovementDemo(1, 1001, "Pago demo", 125.50m, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc), "PEN");
        var newer = new MovementDemo(2, 1001, "Depósito demo", 500m, new DateTime(2026, 9, 11, 14, 30, 0, DateTimeKind.Utc), "PEN");
        var account = new AccountDemo(1001, "Cliente Demo Andino", "Ahorros", 8450.75m, "PEN");
        var status = new ServiceStatus(ServiceState.Connected, "Conexión segura disponible");
        var customer = new CustomerBankingSummary(1, "Cliente Demo Andino", "Personal", [account], [newer, older]);
        return new StubDatabaseService(new(status, customer), new(status, [newer, older]), new(status, [account]));
    }

    private sealed class StubDatabaseService(
        BankingSummaryResult summary,
        MovementsResult movements,
        DatabaseResult accounts) : IDatabaseService
    {
        public Task<DatabaseResult> GetDemoAccountsAsync(CancellationToken cancellationToken = default) => Task.FromResult(accounts);
        public Task<BankingSummaryResult> GetCustomerSummaryAsync(CancellationToken cancellationToken = default) => Task.FromResult(summary);
        public Task<MovementsResult> GetCustomerMovementsAsync(CancellationToken cancellationToken = default) => Task.FromResult(movements);
    }

    private sealed class AvailableKeyVaultService(string token) : IKeyVaultService
    {
        public Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceStatus(ServiceState.Connected, "InternalApiToken disponible: Sí"));

        public Task<string?> GetInternalApiTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(token);
    }

    private sealed class FailedKeyVaultService(string sensitiveDetail) : IKeyVaultService
    {
        public Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceStatus(ServiceState.Error, "Key Vault no disponible"));

        public Task<string?> GetInternalApiTokenAsync(CancellationToken cancellationToken = default)
        {
            _ = sensitiveDetail;
            return Task.FromResult<string?>(null);
        }
    }
}
