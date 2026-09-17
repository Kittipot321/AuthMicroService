using AuthMicroservice.Core.Extensions;
using AuthMicroservice.Migrations.InMemory;
using AuthMicroservice.Migrations.Sqlite;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthMicroservice(builder.Configuration).UseSqlite();

var app = builder.Build();

app.UseAuthMicroservice();
app.MapAuthMicroservice();

app.MapGet("/", () => "AuthMicroservice.Sample — call POST /auth/register to start.")
    .AllowAnonymous();

app.MapGet("/whoami", (HttpContext http) => Results.Ok(new
{
    userId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
    email = http.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
    roles = http.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray()
}))
    .RequireAuthorization();

app.MapGet("/admin-only", () => Results.Ok(new { message = "You are an Admin." }))
    .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

await app.ApplyAuthMicroserviceMigrationsAsync();

app.Run();
