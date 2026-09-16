using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Data.Seeding;
using AuthMicroservice.Core.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAuthMicroservice(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;

        if (options.EnableSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static IEndpointRouteBuilder MapAuthMicroservice(this IEndpointRouteBuilder endpoints)
    {
        return AuthEndpoints.MapAuthMicroservice(endpoints);
    }

    public static async Task ApplyAuthMicroserviceMigrationsAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        if (options.Database.AutoMigrate)
        {
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (options.Database.SeedDefaults)
        {
            await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
