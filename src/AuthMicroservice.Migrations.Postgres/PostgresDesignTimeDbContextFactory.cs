using AuthMicroservice.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthMicroservice.Migrations.Postgres;

public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AuthMicroservice__Database__ConnectionString")
                               ?? "Host=localhost;Port=5432;Database=authdb_design;Username=postgres;Password=postgres";

        var migrationsAssembly = typeof(PostgresMigrationsMarker).Assembly.GetName().Name!;

        var builder = new DbContextOptionsBuilder<AuthDbContext>();
        builder.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(migrationsAssembly));
        return new AuthDbContext(builder.Options);
    }
}
