using AuthMicroservice.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthMicroservice.Migrations.SqlServer;

public sealed class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AuthMicroservice__Database__ConnectionString")
                               ?? "Server=(localdb)\\mssqllocaldb;Database=AuthMicroservice.Design;Trusted_Connection=True;TrustServerCertificate=True";

        var migrationsAssembly = typeof(SqlServerMigrationsMarker).Assembly.GetName().Name!;

        var builder = new DbContextOptionsBuilder<AuthDbContext>();
        builder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        return new AuthDbContext(builder.Options);
    }
}
