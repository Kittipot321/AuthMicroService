using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Data.Seeding;

public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var options = services.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("AuthMicroservice.Seeding");

        foreach (var name in AuthRoles.System)
        {
            await UpsertRoleAsync(roleManager, name, description: null, isSystem: true, clock, logger).ConfigureAwait(false);
        }

        foreach (var definition in options.Identity.Roles.AdditionalRoles)
        {
            if (AuthRoles.System.Contains(definition.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            await UpsertRoleAsync(roleManager, definition.Name, definition.Description, isSystem: false, clock, logger).ConfigureAwait(false);
        }
    }

    private static async Task UpsertRoleAsync(
        RoleManager<ApplicationRole> roleManager,
        string name,
        string? description,
        bool isSystem,
        IClock clock,
        ILogger? logger)
    {
        var existing = await roleManager.FindByNameAsync(name).ConfigureAwait(false);
        if (existing is null)
        {
            var role = new ApplicationRole(name)
            {
                Description = description,
                IsSystem = isSystem,
                CreatedAtUtc = clock.UtcNow
            };

            var result = await roleManager.CreateAsync(role).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger?.LogWarning("Failed to seed role {Role}: {Errors}", name, errors);
            }
            return;
        }

        if (string.Equals(existing.Description, description, StringComparison.Ordinal))
        {
            return;
        }

        existing.Description = description;
        var updateResult = await roleManager.UpdateAsync(existing).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            logger?.LogWarning("Failed to update role {Role}: {Errors}", name, errors);
        }
    }
}
