using BancoSeguro.Web.Models;
using BancoSeguro.Web.Services;
using Microsoft.Extensions.Configuration;

namespace BancoSeguro.Web.Tests;

public sealed class DatabaseServiceTests
{
    [Fact]
    public async Task ConnectionFailure_IsSafeForAllReadModels()
    {
        var sensitiveMessage = $"sensitive-{Guid.NewGuid():N}";
        var logger = new CapturingLogger<DatabaseService>();
        var service = new DatabaseService(ConfiguredDatabase(), new ThrowingSqlClient(sensitiveMessage), logger);

        var accounts = await service.GetDemoAccountsAsync();
        var summary = await service.GetCustomerSummaryAsync();
        var movements = await service.GetCustomerMovementsAsync();

        Assert.Equal(ServiceState.Error, accounts.Status.State);
        Assert.Equal(ServiceState.Error, summary.Status.State);
        Assert.Equal(ServiceState.Error, movements.Status.State);
        Assert.DoesNotContain(sensitiveMessage, accounts.Status.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, summary.Status.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, movements.Status.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, string.Join(" ", logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfiguration_DoesNotAttemptAnyConnection()
    {
        var client = new RecordingSqlClient();
        var service = new DatabaseService(new ConfigurationBuilder().Build(), client, new CapturingLogger<DatabaseService>());

        await service.GetDemoAccountsAsync();
        await service.GetCustomerSummaryAsync();
        await service.GetCustomerMovementsAsync();

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task CustomerSummary_UsesExpectedDemoCustomer()
    {
        var client = new RecordingSqlClient();
        var service = new DatabaseService(ConfiguredDatabase(), client, new CapturingLogger<DatabaseService>());

        var result = await service.GetCustomerSummaryAsync();

        Assert.Equal(ServiceState.Connected, result.Status.State);
        Assert.Equal("Cliente Demo Andino", client.LastCustomerName);
        Assert.Equal("Cliente Demo Andino", result.Customer?.CustomerName);
    }

    [Fact]
    public async Task Movements_AreReturnedNewestFirst()
    {
        var client = new RecordingSqlClient();
        var service = new DatabaseService(ConfiguredDatabase(), client, new CapturingLogger<DatabaseService>());

        var result = await service.GetCustomerMovementsAsync();

        Assert.Equal([2L, 1L], result.Movements.Select(movement => movement.MovementId));
    }

    private static IConfiguration ConfiguredDatabase() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Server"] = "sql-demo.database.windows.net",
            ["Database:Database"] = "sqldb-bancoseguro"
        })
        .Build();

    private sealed class ThrowingSqlClient(string message) : ISqlDemoClient
    {
        public Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken) => throw new InvalidOperationException(message);
        public Task<CustomerBankingSummary?> QueryCustomerSummaryAsync(string server, string database, string customerName, CancellationToken cancellationToken) => throw new InvalidOperationException(message);
        public Task<IReadOnlyList<MovementDemo>> QueryCustomerMovementsAsync(string server, string database, string customerName, CancellationToken cancellationToken) => throw new InvalidOperationException(message);
    }

    private sealed class RecordingSqlClient : ISqlDemoClient
    {
        public int CallCount { get; private set; }
        public string? LastCustomerName { get; private set; }

        public Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<AccountDemo>>(Array.Empty<AccountDemo>());
        }

        public Task<CustomerBankingSummary?> QueryCustomerSummaryAsync(string server, string database, string customerName, CancellationToken cancellationToken)
        {
            CallCount++;
            LastCustomerName = customerName;
            var account = new AccountDemo(1001, customerName, "Ahorros", 8450.75m, "PEN");
            return Task.FromResult<CustomerBankingSummary?>(new(1, customerName, "Personal", [account], []));
        }

        public Task<IReadOnlyList<MovementDemo>> QueryCustomerMovementsAsync(string server, string database, string customerName, CancellationToken cancellationToken)
        {
            CallCount++;
            LastCustomerName = customerName;
            IReadOnlyList<MovementDemo> movements =
            [
                new(1, 1001, "Movimiento anterior", 20m, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), "PEN"),
                new(2, 1001, "Movimiento reciente", 40m, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc), "PEN")
            ];
            return Task.FromResult(movements);
        }
    }
}
