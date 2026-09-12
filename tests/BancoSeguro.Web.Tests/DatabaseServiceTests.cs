using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.Extensions.Configuration;

namespace BancoSeguro.Web.Tests;

public sealed class DatabaseServiceTests
{
    [Fact]
    public async Task ConnectionFailure_IsSafeAndControlled()
    {
        var sensitiveMessage = $"sensitive-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Server"] = "sql-demo.database.windows.net",
                ["Database:Database"] = "sqldb-bancoseguro"
            })
            .Build();
        var logger = new CapturingLogger<DatabaseService>();
        var service = new DatabaseService(configuration, new ThrowingSqlClient(sensitiveMessage), logger);

        var result = await service.GetDemoAccountsAsync();

        Assert.Equal(ServiceState.Error, result.Status.State);
        Assert.Empty(result.Accounts);
        Assert.DoesNotContain(sensitiveMessage, result.Status.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, string.Join(" ", logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfiguration_DoesNotAttemptConnection()
    {
        var client = new CountingSqlClient();
        var service = new DatabaseService(new ConfigurationBuilder().Build(), client, new CapturingLogger<DatabaseService>());

        var result = await service.GetDemoAccountsAsync();

        Assert.Equal(ServiceState.NotConfigured, result.Status.State);
        Assert.Equal(0, client.CallCount);
    }

    private sealed class ThrowingSqlClient(string message) : ISqlDemoClient
    {
        public Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private sealed class CountingSqlClient : ISqlDemoClient
    {
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<AccountDemo>>(Array.Empty<AccountDemo>());
        }
    }
}
