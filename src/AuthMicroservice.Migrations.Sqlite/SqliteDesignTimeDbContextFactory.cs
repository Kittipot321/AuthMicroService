using AuthMicroservice.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthMicroservice.Migrations.Sqlite;

public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AuthMicroservice__Database__ConnectionString")
                               ?? "Data Source=authmicroservice.design.db";

        var migrationsAssembly = typeof(SqliteMigrationsMarker).Assembly.GetName().Name!;

        var builder = new DbContextOptionsBuilder<AuthDbContext>();
        builder.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(migrationsAssembly));
        return new AuthDbContext(builder.Options);
    }
}
