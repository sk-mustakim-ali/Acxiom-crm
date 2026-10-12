using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcxiomCRM.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AcxiomCRM.Tests;

public sealed class ApiAuthorizationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ApiAuthorizationTests(TestApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Api_RequiresAuthentication_AndRejectsInvalidAuthenticatedPayloads()
    {
        using var anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/customers")).StatusCode);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await RegisterSalesExecutiveAsync(client);
        var csrf = await GetCsrfTokenAsync(client);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf);

        using var missingTokenClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await RegisterSalesExecutiveAsync(missingTokenClient);
        using var missingTokenResponse = await missingTokenClient.PostAsJsonAsync("/api/customers", new { customerName = "No token" });
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using var invalidResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            customerName = "",
            email = "bad-email",
            phone = "123",
            companyName = "Invalid Co",
            status = "Active"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Api_CreatesCustomerAuditsItAndReturnsConflictForNormalizedDuplicate()
    {
        using var firstClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var firstEmail = await RegisterSalesExecutiveAsync(firstClient);
        await AddCsrfHeaderAsync(firstClient);
        using (var beforeCreateScope = _factory.Services.CreateScope())
        {
            var beforeCreateDb = beforeCreateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await beforeCreateDb.Customers.CountAsync() == 0,
                $"Expected isolated test DB, got {beforeCreateDb.Database.GetDbConnection().DataSource}");
        }

        using var created = await firstClient.PostAsJsonAsync("/api/customers", new
        {
            customerName = "Example Customer",
            email = "Person@Example.test",
            phone = "+1 (555) 333-4444",
            companyName = "Example Co",
            status = "Active"
        });
        Assert.True(created.StatusCode == HttpStatusCode.Created,
            $"Expected customer creation, got {(int)created.StatusCode}: {await created.Content.ReadAsStringAsync()}");
        var result = await created.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = result.GetProperty("id").GetInt32();

        using var duplicate = await firstClient.PostAsJsonAsync("/api/customers", new
        {
            customerName = "Duplicate",
            email = "person@example.test",
            phone = "15553334444",
            companyName = "Other Co",
            status = "Active"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var secondClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await RegisterSalesExecutiveAsync(secondClient);
        using var outsideScope = await secondClient.GetAsync($"/api/customers/{customerId}");
        Assert.Equal(HttpStatusCode.NotFound, outsideScope.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var firstUser = await users.FindByEmailAsync(firstEmail);
        Assert.NotNull(firstUser);
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.UserId == firstUser!.Id && log.EntityName == "Customer" && log.Action == "Create"));
    }

    [Fact]
    public async Task ExistingCookie_IsInvalidatedAfterRoleChangesAndAccountDeactivation()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = await RegisterSalesExecutiveAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True((await manager.RemoveFromRoleAsync(user!, "SalesExecutive")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user!, "Admin")).Succeeded);
            Assert.True((await manager.UpdateSecurityStampAsync(user!)).Succeeded);
        }

        using var adminPage = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, adminPage.StatusCode);
        Assert.Contains("/Identity/Account/Login", adminPage.Headers.Location?.ToString());

        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user!.IsActive = false;
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
            Assert.True((await manager.UpdateSecurityStampAsync(user)).Succeeded);
        }

        using var dashboard = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.Contains("/Identity/Account/Login", dashboard.Headers.Location?.ToString());
    }

    private async Task<string> RegisterSalesExecutiveAsync(HttpClient client)
    {
        var email = $"sales-{Guid.NewGuid():N}@example.test";
        using var form = await client.GetAsync("/Identity/Account/Register");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        var html = await form.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success, "Registration page must contain an antiforgery token.");

        using var response = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            ["Input.FullName"] = "Test Sales User",
            ["Input.Email"] = email,
            ["Input.Password"] = "Strong!Password123",
            ["Input.ConfirmPassword"] = "Strong!Password123"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return email;
    }

    private static async Task AddCsrfHeaderAsync(HttpClient client)
    {
        var token = await GetCsrfTokenAsync(client);
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return json!.RootElement.GetProperty("token").GetString()!;
    }
}

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AcxiomCRM.Tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(_root, "tests.db")}",
                ["Logging:EventLog:LogLevel:Default"] = "None"
            }));
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(_root, "tests.db")}");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(_root, "tests.db")}"));
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(_root, "keys")));
        });
    }
}
