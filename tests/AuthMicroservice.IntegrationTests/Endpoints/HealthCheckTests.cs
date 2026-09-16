using System.Net;
using System.Text.Json;
using AuthMicroservice.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AuthMicroservice.IntegrationTests.Endpoints;

public class HealthCheckTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public HealthCheckTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthy_WhenDbAvailable_AndSkipsSmtpByDefault()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/auth/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        var names = doc.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToArray();

        names.Should().Contain("auth-db");
        names.Should().NotContain("auth-smtp", because: "CheckSmtp defaults to false");
    }

    [Fact]
    public async Task Health_ReturnsUnhealthy_WhenSmtpOptedIn_AndHostUnreachable()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthMicroservice:HealthChecks:CheckSmtp"] = "true",
                    ["AuthMicroservice:HealthChecks:SmtpTimeoutSeconds"] = "1",
                    ["AuthMicroservice:Email:Smtp:Host"] = "127.0.0.1",
                    ["AuthMicroservice:Email:Smtp:Port"] = "1"
                });
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/auth/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().NotBe("Healthy");

        var smtp = doc.RootElement.GetProperty("checks")
            .EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("name").GetString() == "auth-smtp");
        smtp.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        smtp.GetProperty("status").GetString().Should().Be("Unhealthy");
    }
}
