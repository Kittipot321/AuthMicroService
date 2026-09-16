using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Extensions;
using AuthMicroservice.Migrations.Postgres;
using AuthMicroservice.Migrations.Sqlite;
using AuthMicroservice.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(sp)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddAuthMicroservice(builder.Configuration);

// Config-driven provider dispatch. Runs when AuthDbContext is first resolved,
// so it sees the fully-bound IOptions (including test-time config overrides).
builder.Services.AddDbContext<AuthDbContext>((sp, opts) =>
{
    var db = sp.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value.Database;

    switch (db.Provider)
    {
        case DatabaseProvider.SqlServer:
            RequireCs(db, "SqlServer");
            opts.UseSqlServer(db.ConnectionString!, sql =>
                sql.MigrationsAssembly(typeof(SqlServerMigrationsMarker).Assembly.GetName().Name));
            break;

        case DatabaseProvider.Postgres:
            RequireCs(db, "Postgres");
            opts.UseNpgsql(db.ConnectionString!, npg =>
                npg.MigrationsAssembly(typeof(PostgresMigrationsMarker).Assembly.GetName().Name));
            break;

        case DatabaseProvider.Sqlite:
            RequireCs(db, "Sqlite");
            opts.UseSqlite(db.ConnectionString!, sqlite =>
                sqlite.MigrationsAssembly(typeof(SqliteMigrationsMarker).Assembly.GetName().Name));
            break;

        case DatabaseProvider.InMemory:
            opts.UseInMemoryDatabase(
                string.IsNullOrWhiteSpace(db.ConnectionString) ? "AuthMicroserviceInMemory" : db.ConnectionString);
            break;

        default:
            throw new InvalidOperationException($"Unsupported provider '{db.Provider}'.");
    }
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseAuthMicroservice();
app.MapAuthMicroservice();

await app.ApplyAuthMicroserviceMigrationsAsync();

app.Run();

static void RequireCs(DatabaseOptions db, string providerName)
{
    if (string.IsNullOrWhiteSpace(db.ConnectionString))
    {
        throw new InvalidOperationException(
            $"AuthMicroservice:Database:ConnectionString is required for provider '{providerName}'.");
    }
}

public partial class Program;
