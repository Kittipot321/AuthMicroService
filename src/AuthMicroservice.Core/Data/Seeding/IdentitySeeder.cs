using AuthMicroservice.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuthMicroservice.Core.Data.Seeding;

public static class IdentitySeeder
{
    public static readonly string[] DefaultRoles = { "Admin", "User" };

    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("AuthMicroservice.Seeding");

        foreach (var role in DefaultRoles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole(role));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger?.LogWarning("Failed to seed role {Role}: {Errors}", role, errors);
            }
        }
    }
}
