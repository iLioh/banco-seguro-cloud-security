namespace BancoSeguro.Web.Models;

public enum ServiceState
{
    Running,
    Configured,
    Connected,
    Available,
    NotConfigured,
    NotAvailable,
    Error
}

public sealed record ServiceStatus(ServiceState State, string Message)
{
    public bool IsSuccess => State is ServiceState.Running or ServiceState.Configured or ServiceState.Connected or ServiceState.Available;
}

public sealed record AccountDemo(int AccountId, string CustomerName, string AccountType, decimal Balance, string Currency);

public sealed record MovementDemo(
    long MovementId,
    int AccountId,
    string OperationType,
    decimal Amount,
    DateTime OperationDate,
    string Currency);

public sealed record CustomerBankingSummary(
    int CustomerId,
    string CustomerName,
    string Segment,
    IReadOnlyList<AccountDemo> Accounts,
    IReadOnlyList<MovementDemo> RecentMovements);

public sealed record DatabaseResult(ServiceStatus Status, IReadOnlyList<AccountDemo> Accounts)
{
    public static DatabaseResult WithoutData(ServiceStatus status) => new(status, Array.Empty<AccountDemo>());
}

public sealed record BankingSummaryResult(ServiceStatus Status, CustomerBankingSummary? Customer)
{
    public static BankingSummaryResult WithoutData(ServiceStatus status) => new(status, null);
}

public sealed record MovementsResult(ServiceStatus Status, IReadOnlyList<MovementDemo> Movements)
{
    public static MovementsResult WithoutData(ServiceStatus status) => new(status, Array.Empty<MovementDemo>());
}
