using BancoSeguro.Web.Models;
using Microsoft.Data.SqlClient;

namespace BancoSeguro.Web.Services;

public interface ISqlDemoClient
{
    Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken);
    Task<CustomerBankingSummary?> QueryCustomerSummaryAsync(string server, string database, string customerName, CancellationToken cancellationToken);
    Task<IReadOnlyList<MovementDemo>> QueryCustomerMovementsAsync(string server, string database, string customerName, CancellationToken cancellationToken);
}

public sealed class SqlDemoClient : ISqlDemoClient
{
    public async Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(server, database);
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT TOP (10)
                cta.CuentaId,
                cli.Nombre,
                cta.TipoCuenta,
                cta.Saldo,
                cta.Moneda
            FROM dbo.CuentasDemo AS cta
            INNER JOIN dbo.ClientesDemo AS cli ON cli.ClienteId = cta.ClienteId
            ORDER BY cta.CuentaId;
            """;

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadAccountsAsync(reader, cancellationToken);
    }

    public async Task<CustomerBankingSummary?> QueryCustomerSummaryAsync(
        string server,
        string database,
        string customerName,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(server, database);
        await connection.OpenAsync(cancellationToken);

        const string customerQuery = """
            SELECT TOP (1) ClienteId, Nombre, Segmento
            FROM dbo.ClientesDemo
            WHERE Nombre = @CustomerName;
            """;

        int customerId;
        string resolvedName;
        string segment;
        await using (var customerCommand = new SqlCommand(customerQuery, connection))
        {
            customerCommand.Parameters.AddWithValue("@CustomerName", customerName);
            await using var reader = await customerCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            customerId = reader.GetInt32(0);
            resolvedName = reader.GetString(1);
            segment = reader.GetString(2);
        }

        const string accountsQuery = """
            SELECT CuentaId, @CustomerName, TipoCuenta, Saldo, Moneda
            FROM dbo.CuentasDemo
            WHERE ClienteId = @CustomerId
            ORDER BY CuentaId;
            """;

        IReadOnlyList<AccountDemo> accounts;
        await using (var accountsCommand = new SqlCommand(accountsQuery, connection))
        {
            accountsCommand.Parameters.AddWithValue("@CustomerId", customerId);
            accountsCommand.Parameters.AddWithValue("@CustomerName", resolvedName);
            await using var reader = await accountsCommand.ExecuteReaderAsync(cancellationToken);
            accounts = await ReadAccountsAsync(reader, cancellationToken);
        }

        var movements = await QueryCustomerMovementsAsync(connection, customerId, 5, cancellationToken);
        return new(customerId, resolvedName, segment, accounts, movements);
    }

    public async Task<IReadOnlyList<MovementDemo>> QueryCustomerMovementsAsync(
        string server,
        string database,
        string customerName,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(server, database);
        await connection.OpenAsync(cancellationToken);

        const string customerQuery = """
            SELECT TOP (1) ClienteId
            FROM dbo.ClientesDemo
            WHERE Nombre = @CustomerName;
            """;

        await using var customerCommand = new SqlCommand(customerQuery, connection);
        customerCommand.Parameters.AddWithValue("@CustomerName", customerName);
        var customerId = await customerCommand.ExecuteScalarAsync(cancellationToken);
        if (customerId is not int id)
        {
            return Array.Empty<MovementDemo>();
        }

        return await QueryCustomerMovementsAsync(connection, id, null, cancellationToken);
    }

    private static SqlConnection CreateConnection(string server, string database)
    {
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
            ConnectTimeout = 5,
            PersistSecurityInfo = false
        }.ConnectionString;

        return new(connectionString);
    }

    private static async Task<IReadOnlyList<AccountDemo>> ReadAccountsAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var accounts = new List<AccountDemo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetString(4)));
        }

        return accounts;
    }

    private static async Task<IReadOnlyList<MovementDemo>> QueryCustomerMovementsAsync(
        SqlConnection connection,
        int customerId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var topClause = limit.HasValue ? "TOP (@Limit)" : string.Empty;
        var query = $"""
            SELECT {topClause}
                op.OperacionId,
                op.CuentaId,
                op.TipoOperacion,
                op.Monto,
                op.FechaOperacion,
                cta.Moneda
            FROM dbo.OperacionesDemo AS op
            INNER JOIN dbo.CuentasDemo AS cta ON cta.CuentaId = op.CuentaId
            WHERE cta.ClienteId = @CustomerId
            ORDER BY op.FechaOperacion DESC, op.OperacionId DESC;
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@CustomerId", customerId);
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("@Limit", limit.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var movements = new List<MovementDemo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            movements.Add(new(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDateTime(4),
                reader.GetString(5)));
        }

        return movements;
    }
}

public sealed class DatabaseService(
    IConfiguration configuration,
    ISqlDemoClient sqlClient,
    ILogger<DatabaseService> logger) : IDatabaseService
{
    private const string DemoCustomerName = "Cliente Demo Andino";

    public async Task<DatabaseResult> GetDemoAccountsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetDatabaseConfiguration(out var server, out var database))
        {
            return DatabaseResult.WithoutData(NotConfiguredStatus());
        }

        try
        {
            var accounts = await sqlClient.QueryAccountsAsync(server, database, cancellationToken);
            return new(ConnectedStatus(), accounts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDatabaseFailure(exception);
            return DatabaseResult.WithoutData(ErrorStatus());
        }
    }

    public async Task<BankingSummaryResult> GetCustomerSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetDatabaseConfiguration(out var server, out var database))
        {
            return BankingSummaryResult.WithoutData(NotConfiguredStatus());
        }

        try
        {
            var customer = await sqlClient.QueryCustomerSummaryAsync(server, database, DemoCustomerName, cancellationToken);
            return customer is null
                ? BankingSummaryResult.WithoutData(new(ServiceState.NotAvailable, "El resumen bancario aún no tiene datos disponibles"))
                : new(ConnectedStatus(), customer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDatabaseFailure(exception);
            return BankingSummaryResult.WithoutData(ErrorStatus());
        }
    }

    public async Task<MovementsResult> GetCustomerMovementsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetDatabaseConfiguration(out var server, out var database))
        {
            return MovementsResult.WithoutData(NotConfiguredStatus());
        }

        try
        {
            var movements = await sqlClient.QueryCustomerMovementsAsync(server, database, DemoCustomerName, cancellationToken);
            var orderedMovements = movements
                .OrderByDescending(movement => movement.OperationDate)
                .ThenByDescending(movement => movement.MovementId)
                .ToArray();
            return new(ConnectedStatus(), orderedMovements);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDatabaseFailure(exception);
            return MovementsResult.WithoutData(ErrorStatus());
        }
    }

    private bool TryGetDatabaseConfiguration(out string server, out string database)
    {
        server = configuration["Database:Server"] ?? string.Empty;
        database = configuration["Database:Database"] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(database);
    }

    private void LogDatabaseFailure(Exception exception) =>
        logger.LogWarning("No se pudo consultar Azure SQL. Tipo de error: {ErrorType}", exception.GetType().Name);

    private static ServiceStatus ConnectedStatus() => new(ServiceState.Connected, "Conexión segura disponible");
    private static ServiceStatus NotConfiguredStatus() => new(ServiceState.NotConfigured, "Azure SQL no configurado");
    private static ServiceStatus ErrorStatus() => new(ServiceState.Error, "No fue posible conectar con Azure SQL");
}
