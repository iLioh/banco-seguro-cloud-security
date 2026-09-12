using BancoSeguro.Web.Models;
using Microsoft.Data.SqlClient;

namespace BancoSeguro.Web.Services;

public interface ISqlDemoClient
{
    Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken);
}

public sealed class SqlDemoClient : ISqlDemoClient
{
    public async Task<IReadOnlyList<AccountDemo>> QueryAccountsAsync(string server, string database, CancellationToken cancellationToken)
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

        await using var connection = new SqlConnection(connectionString);
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
}

public sealed class DatabaseService(
    IConfiguration configuration,
    ISqlDemoClient sqlClient,
    ILogger<DatabaseService> logger) : IDatabaseService
{
    public async Task<DatabaseResult> GetDemoAccountsAsync(CancellationToken cancellationToken = default)
    {
        var server = configuration["Database:Server"];
        var database = configuration["Database:Database"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            return DatabaseResult.WithoutData(new(ServiceState.NotConfigured, "Azure SQL no configurado"));
        }

        try
        {
            var accounts = await sqlClient.QueryAccountsAsync(server, database, cancellationToken);
            return new(new(ServiceState.Connected, "Conexión segura disponible"), accounts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("No se pudo consultar Azure SQL. Tipo de error: {ErrorType}", exception.GetType().Name);
            return DatabaseResult.WithoutData(new(ServiceState.Error, "No fue posible conectar con Azure SQL"));
        }
    }
}

