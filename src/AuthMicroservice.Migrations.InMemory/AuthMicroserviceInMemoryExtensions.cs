using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Migrations.InMemory;

public static class AuthMicroserviceInMemoryExtensions
{
    /// <summary>
    /// Registers <see cref="AuthDbContext"/> with the EF Core InMemory provider. Intended for tests and demos.
    /// The database name defaults to the value of <c>AuthMicroservice:Database:ConnectionString</c>, then
    /// <paramref name="databaseName"/>, then a stable fallback.
    /// </summary>
    public static IAuthMicroserviceBuilder UseInMemory(
        this IAuthMicroserviceBuilder builder,
        string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddDbContext<AuthDbContext>((sp, opts) =>
        {
            var authOpts = sp.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
            var name = !string.IsNullOrWhiteSpace(databaseName)
                ? databaseName
                : (!string.IsNullOrWhiteSpace(authOpts.Database.ConnectionString)
                    ? authOpts.Database.ConnectionString
                    : "AuthMicroserviceInMemory");

            opts.UseInMemoryDatabase(name!);
        });

        return builder;
    }
}
