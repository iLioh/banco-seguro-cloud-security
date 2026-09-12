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
    public async Task Application_StartsAndServesHomePage()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Banco Seguro", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingAzureConfiguration_ProducesControlledDashboardState()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/dashboard");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NotConfigured", html);
        Assert.Contains("Key Vault no configurado", html);
        Assert.Contains("Azure SQL no configurado", html);
    }

    [Fact]
    public async Task SecurityPage_NeverReturnsInternalApiTokenValue()
    {
        var sensitiveToken = $"sensitive-{Guid.NewGuid():N}";
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKeyVaultService>(_ => new AvailableKeyVaultService(sensitiveToken));
            }));
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/security");

        Assert.Contains("InternalApiToken disponible", html);
        Assert.Contains("Sí", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain(sensitiveToken, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_NeverReturnsInternalApiTokenValue()
    {
        var sensitiveToken = $"sensitive-{Guid.NewGuid():N}";
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKeyVaultService>(_ => new AvailableKeyVaultService(sensitiveToken));
            }));
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dashboard")).StatusCode);
        Assert.DoesNotContain(sensitiveToken, html, StringComparison.Ordinal);
    }

    private sealed class AvailableKeyVaultService(string token) : IKeyVaultService
    {
        public Task<ServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServiceStatus(ServiceState.Connected, "InternalApiToken disponible: Sí"));

        public Task<string?> GetInternalApiTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(token);
    }
}
