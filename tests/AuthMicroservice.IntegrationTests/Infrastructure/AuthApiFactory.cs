using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailSender EmailSender { get; } = new();

    public FakeGoogleTokenValidator GoogleTokenValidator { get; } = new();

    public FakeMicrosoftTokenValidator MicrosoftTokenValidator { get; } = new();

    public FakeLineTokenValidator LineTokenValidator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            var testSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json");
            config.AddJsonFile(testSettings, optional: false, reloadOnChange: false);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthMicroservice:Database:ConnectionString"] = $"AuthMicroservice.Tests.{Guid.NewGuid():N}"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator>(GoogleTokenValidator);

            services.RemoveAll<IMicrosoftTokenValidator>();
            services.AddSingleton<IMicrosoftTokenValidator>(MicrosoftTokenValidator);

            services.RemoveAll<ILineTokenValidator>();
            services.AddSingleton<ILineTokenValidator>(LineTokenValidator);
        });
    }
}
