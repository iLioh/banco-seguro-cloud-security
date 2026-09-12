using BancoSeguro.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BancoSeguro.Web.Tests;

public sealed class KeyVaultServiceTests
{
    [Fact]
    public async Task AccessFailure_IsHandledWithoutExposingSecretOrExceptionMessage()
    {
        var sensitiveValue = $"sensitive-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:KeyVaultUri"] = "https://demo.vault.azure.net/" })
            .Build();
        var logger = new CapturingLogger<KeyVaultService>();
        var service = new KeyVaultService(configuration, new ThrowingSecretClient(sensitiveValue), logger);

        var status = await service.GetStatusAsync();

        Assert.Equal("Error", status.State.ToString());
        Assert.DoesNotContain(sensitiveValue, status.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, string.Join(" ", logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfiguration_DoesNotCallAzure()
    {
        var client = new CountingSecretClient();
        var service = new KeyVaultService(new ConfigurationBuilder().Build(), client, new CapturingLogger<KeyVaultService>());

        var status = await service.GetStatusAsync();

        Assert.Equal("NotConfigured", status.State.ToString());
        Assert.Equal(0, client.CallCount);
    }

    private sealed class ThrowingSecretClient(string message) : IKeyVaultSecretClient
    {
        public Task<string?> GetSecretValueAsync(Uri vaultUri, string secretName, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private sealed class CountingSecretClient : IKeyVaultSecretClient
    {
        public int CallCount { get; private set; }
        public Task<string?> GetSecretValueAsync(Uri vaultUri, string secretName, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<string?>(null);
        }
    }
}
