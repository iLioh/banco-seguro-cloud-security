using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

var app = builder.Build();

app.UseHttpsRedirection();

// Health check técnico. No expone información sensible.
app.MapGet("/healthz", () =>
    Results.Ok(new
    {
        status = "healthy",
        service = "BancoSeguro.InternalApi"
    }));

// Endpoint protegido consumido por BancoSeguro.Web.
app.MapGet("/health", async (
    HttpRequest request,
    IConfiguration configuration,
    TokenCredential credential,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("Authorization", out var authorization) ||
        !authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Unauthorized();
    }

    var providedToken = authorization.ToString()["Bearer ".Length..].Trim();

    if (string.IsNullOrWhiteSpace(providedToken))
    {
        return Results.Unauthorized();
    }

    var configuredVaultUri = configuration["Azure:KeyVaultUri"];

    if (!Uri.TryCreate(configuredVaultUri, UriKind.Absolute, out var vaultUri) ||
        vaultUri.Scheme != Uri.UriSchemeHttps)
    {
        logger.LogError("Key Vault URI no configurado.");
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Servicio temporalmente no disponible");
    }

    try
    {
        var secretClient = new SecretClient(vaultUri, credential);

        var secret = await secretClient.GetSecretAsync(
            "InternalApiToken",
            cancellationToken: cancellationToken);

        var expectedToken = secret.Value.Value;

        var providedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(providedToken));

        var expectedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(expectedToken));

        if (!CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
        {
            logger.LogWarning("Intento de autenticación inválido contra la API interna.");
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            status = "healthy",
            service = "BancoSeguro.InternalApi",
            authentication = "validated"
        });
    }
    catch (Exception exception)
    {
        logger.LogError(
            "No se pudo validar la autenticación de la API interna. Tipo: {ErrorType}",
            exception.GetType().Name);

        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Servicio temporalmente no disponible");
    }
});

app.Run();

public partial class Program;
