using Azure.Core;
using Azure.Identity;
using BancoSeguro.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddSingleton<IKeyVaultSecretClient, AzureKeyVaultSecretClient>();
builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();
builder.Services.AddSingleton<ISqlDemoClient, SqlDemoClient>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddHttpClient<IInternalApiService, InternalApiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .ExcludeFromDescription();

app.Run();

public partial class Program;

