# AuthMicroservice

Reusable authentication component for ASP.NET Core 8 — usable **both** as a plug-in library and as a standalone Web API microservice. Similar in spirit to Keycloak but lightweight and idiomatic .NET.

## Features (v1)

- Register / Login / Logout (email + password)
- JWT access tokens + refresh token rotation (hash-stored, revoke on reuse)
- Email verification link + password reset link (SMTP via MailKit; pluggable)
- Change password (revokes existing refresh tokens)
- Account lockout after N failed attempts
- Roles + custom claims (ASP.NET Core Identity underneath)
- Provider-agnostic EF Core — pick **SqlServer / Postgres / Sqlite / InMemory** in `appsettings.json`
- All config from `appsettings.json` / env vars — no hardcoded secrets
- Swagger UI, Dockerfile + docker-compose (with Mailhog), sample consumer, unit + integration tests

## Solution layout

```
AuthMicroservice.sln
├── src/
│   ├── AuthMicroservice.Core/                # reusable library — services, endpoints, DI extensions
│   ├── AuthMicroservice.Migrations.SqlServer # provider-specific EF Core migrations
│   ├── AuthMicroservice.Migrations.Postgres
│   ├── AuthMicroservice.Migrations.Sqlite
│   ├── AuthMicroservice.Migrations.InMemory  # in-memory adapter (tests/demos, not for production)
│   ├── AuthMicroservice.Api/                 # standalone Web API host (Docker target)
│   └── AuthMicroservice.Sample/              # library-mode demo consumer (InMemory, no email)
└── tests/
    ├── AuthMicroservice.UnitTests/           # xUnit + Moq + FluentAssertions
    └── AuthMicroservice.IntegrationTests/    # WebApplicationFactory<Program> + InMemory DB
```

## Consume from your own project (library mode)

Install via NuGet (published as `Kittipot.AuthMicroservice.*` to avoid name collisions on nuget.org — the runtime assembly + `using AuthMicroservice.Core.*` namespaces are unchanged):

```powershell
dotnet add package Kittipot.AuthMicroservice.Core
dotnet add package Kittipot.AuthMicroservice.Migrations.SqlServer   # or .Postgres / .Sqlite / .InMemory
```

Or add a project reference to `AuthMicroservice.Core` (and the migration assemblies for the providers you want to support) if you have this repo checked out locally. Then in `Program.cs`:

```csharp
using AuthMicroservice.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthMicroservice(builder.Configuration);

var app = builder.Build();
app.UseAuthMicroservice();
app.MapAuthMicroservice();
await app.ApplyAuthMicroserviceMigrationsAsync();
app.Run();
```

Add the `AuthMicroservice` section to your `appsettings.json` — see [`src/AuthMicroservice.Api/appsettings.json`](src/AuthMicroservice.Api/appsettings.json) for the full schema, or [`src/AuthMicroservice.Sample/appsettings.json`](src/AuthMicroservice.Sample/appsettings.json) for a zero-config SQLite demo.

## API endpoints (mounted under `RoutePrefix`, default `/auth`)

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/auth/register` | anon | 201 with tokens; sends verification email |
| POST | `/auth/login` | anon | 200 tokens · 401 bad creds · 403 unconfirmed · 423 lockout |
| POST | `/auth/refresh` | anon | Rotates refresh; old refresh replay → 401 |
| POST | `/auth/logout` | auth | Revokes one refresh token |
| POST | `/auth/logout-all` | auth | Revokes all refresh tokens for user |
| POST | `/auth/verify-email` | anon | `{userId, token}` |
| POST | `/auth/resend-verification` | anon | Silent on unknown email |
| POST | `/auth/forgot-password` | anon | Always 200 (guards against enumeration) |
| POST | `/auth/reset-password` | anon | `{email, token, newPassword}` |
| POST | `/auth/change-password` | auth | Requires current password |
| GET | `/auth/me` | auth | User profile with roles + claims |
| GET | `/auth/health` | anon | Liveness |

Errors follow RFC 7807 ProblemDetails with error codes such as `INVALID_CREDENTIALS`, `USER_LOCKED_OUT`, `INVALID_REFRESH_TOKEN`.

## Quick start — standalone via Docker Compose (SQL Server + Mailhog)

```powershell
copy .env.example .env
# edit .env — set a real JWT_KEY (>= 32 chars)

docker compose up -d --build

# health
curl http://localhost:8080/auth/health

# swagger UI
Start-Process http://localhost:8080/swagger

# mailhog UI (dev override)
Start-Process http://localhost:8025
```

## Quick start — library-mode sample (InMemory, no email)

```powershell
dotnet run --project src/AuthMicroservice.Sample
# opens http://localhost:5100/swagger
```

The sample exposes `/whoami` (any authenticated user) and `/admin-only` (`Admin` role) to prove the consumer wiring end-to-end.

## End-to-end verification (PowerShell)

```powershell
# 1. Register
$reg = @{ email="alice@example.com"; password="P@ssw0rd!"; fullName="Alice" } | ConvertTo-Json
$response = curl -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d $reg | ConvertFrom-Json

# 2. Grab the verification link from Mailhog (http://localhost:8025), then:
$ver = @{ userId="<GUID>"; token="<TOKEN>" } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/verify-email -H "Content-Type: application/json" -d $ver

# 3. Login
$login = @{ email="alice@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$auth = curl -X POST http://localhost:8080/auth/login -H "Content-Type: application/json" -d $login | ConvertFrom-Json

# 4. Protected endpoint
curl http://localhost:8080/auth/me -H "Authorization: Bearer $($auth.accessToken)"

# 5. Rotate refresh token (old refresh becomes invalid)
$rf = @{ accessToken=$auth.accessToken; refreshToken=$auth.refreshToken } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/refresh -H "Content-Type: application/json" -d $rf

# 6. Forgot + reset password (token from Mailhog)
curl -X POST http://localhost:8080/auth/forgot-password -H "Content-Type: application/json" -d (@{ email="alice@example.com" } | ConvertTo-Json)

# 7. Trigger lockout (default 5 failed attempts)
1..5 | ForEach-Object { curl -X POST http://localhost:8080/auth/login -H "Content-Type: application/json" -d (@{ email="alice@example.com"; password="wrong" } | ConvertTo-Json) }
```

## Configuration reference

The `AuthMicroservice` config section (bind from any `IConfiguration`):

```jsonc
{
  "AuthMicroservice": {
    "Database": {
      "Provider": "SqlServer",       // SqlServer | Postgres | Sqlite | InMemory
      "ConnectionString": "...",
      "AutoMigrate": true,
      "SeedDefaults": true             // seeds Admin + User roles
    },
    "Jwt": {
      "Issuer": "AuthMicroservice",
      "Audience": "AuthMicroservice.Clients",
      "Key": "REPLACE_FROM_ENV_MIN_32_CHARS",
      "AccessTokenLifetimeMinutes": 15,
      "RefreshTokenLifetimeDays": 7,
      "ClockSkewSeconds": 30
    },
    "Email": {
      "Enabled": true,
      "FromAddress": "no-reply@example.com",
      "FromName": "Auth Service",
      "Smtp": { "Host": "localhost", "Port": 1025, "UseStartTls": false, "UseSsl": false, "Username": "", "Password": "" },
      "Templates": { "VerifyEmailSubject": "Verify your email", "PasswordResetSubject": "Reset your password" }
    },
    "Identity": {
      "Password": { "RequiredLength": 8, "RequireDigit": true, "RequireLowercase": true, "RequireUppercase": true, "RequireNonAlphanumeric": true, "RequiredUniqueChars": 1 },
      "Lockout":  { "AllowedForNewUsers": true, "MaxFailedAccessAttempts": 5, "DefaultLockoutMinutes": 15 },
      "SignIn":   { "RequireConfirmedEmail": true, "RequireConfirmedPhoneNumber": false },
      "User":     { "RequireUniqueEmail": true }
    },
    "TokenLinks": {
      "EmailVerificationBaseUrl": "https://app.example.com/verify-email",
      "PasswordResetBaseUrl": "https://app.example.com/reset-password"
    },
    "RoutePrefix": "/auth",
    "EnableSwagger": true
  }
}
```

Secrets are typically supplied via env vars using double-underscore syntax:

- `AuthMicroservice__Jwt__Key`
- `AuthMicroservice__Database__ConnectionString`
- `AuthMicroservice__Email__Smtp__Password`

Startup fails fast if `Jwt.Key` is under 32 chars, an unknown DB provider is set, or `Email.Enabled=true` without an SMTP host.

## EF Core migrations (per provider)

Migrations are per-provider assemblies. To generate a new migration:

```powershell
# SqlServer
dotnet ef migrations add AddSomething `
  --project src/AuthMicroservice.Migrations.SqlServer `
  --startup-project src/AuthMicroservice.Migrations.SqlServer `
  --context AuthDbContext

# Postgres
dotnet ef migrations add AddSomething `
  --project src/AuthMicroservice.Migrations.Postgres `
  --startup-project src/AuthMicroservice.Migrations.Postgres `
  --context AuthDbContext

# Sqlite
dotnet ef migrations add AddSomething `
  --project src/AuthMicroservice.Migrations.Sqlite `
  --startup-project src/AuthMicroservice.Migrations.Sqlite `
  --context AuthDbContext
```

## Swapping the email transport

`IEmailSender` (`SmtpEmailSender` by default) is the pluggable point. Replace it before `Build()`:

```csharp
builder.Services.AddAuthMicroservice(builder.Configuration);
builder.Services.AddSingleton<IEmailSender, MySendGridEmailSender>();
```

`IEmailService` composes the templates and delegates to `IEmailSender`, so a custom sender inherits verification + reset email flows for free.

## Tests

```powershell
dotnet test
```

- Unit tests: JWT service (claims, expiry, signature), refresh token rotation (hash storage, chain revoke), email templating, request validators.
- Integration tests: WebApplicationFactory + InMemory DB — register/login flow, wrong-password → 401, lockout → 423, refresh rotation + replay-rejection, forgot/reset flow, change-password revoking refresh tokens.

## License

MIT — free to use as a base for your own projects.

## สรุปสิ่งที่สร้าง
Solution: [c:\Code\AuthMicroServices](c:\Code\AuthMicroServices)

- AuthMicroservice.Core — reusable library ครบชุด (domain, EF context, JWT/refresh/email services, minimal-API endpoints, DI extensions)
- Migrations.SqlServer / Postgres / Sqlite — Initial migration ถูก generate แล้วทั้ง 3 provider
- AuthMicroservice.Api — standalone host ~20 บรรทัดใน Program.cs + Serilog + Swagger
- AuthMicroservice.Sample — library-mode demo (SQLite, no email, /whoami + /admin-only)
- UnitTests — JWT service, refresh rotation, email templates, validators
- IntegrationTests — full register→verify→login→refresh→forgot/reset→change→lockout ผ่าน WebApplicationFactory + InMemory DB + FakeEmailSender
- Dockerfile + docker-compose.yml + override — SQL Server 2022 + Mailhog stack
- README.md — quickstart, endpoint table, config reference, verification steps


## Config contract (สิ่งที่ผู้ใช้ library ต้องรู้)

```
builder.Services.AddAuthMicroservice(builder.Configuration);
app.UseAuthMicroservice();
app.MapAuthMicroservice();
await app.ApplyAuthMicroserviceMigrationsAsync();
```

Provider เลือกใน appsettings.json ที่ key AuthMicroservice:Database:Provider — startup fail-fast ถ้า JWT key < 32 chars หรือ SMTP host หายเมื่อ Email.Enabled=true

### ข้อสังเกตสำคัญ

1. Refresh token เก็บเป็น SHA-256 hash ไม่เก็บ raw + มี rotation chain (revoke ทั้ง chain ถ้าเจอ replay)
2. Options ผูกแบบ lazy ผ่าน IConfigureOptions<IdentityOptions> + IConfigureOptions<JwtBearerOptions> เพื่อให้ test override config ทำงานได้ถูกต้อง (bug ที่แก้ระหว่างทาง)
3. Migrations แยก 3 assembly ต่อ provider พร้อม IDesignTimeDbContextFactory แต่ละตัว — เพิ่ม column ใหม่ต้อง run migrations add 3 ครั้ง (idiomatic ของ EF Core)
4. NU1902 warnings ของ MailKit/MimeKit ที่เห็นทั้ง build เป็น DoS ตอน parse email ขาเข้า — เราใช้ SMTP ส่งอย่างเดียว ไม่กระทบ
5. IEmailSender เปลี่ยนได้ ผ่าน services.AddSingleton<IEmailSender, MySendGridSender>() โดยไม่ต้องแตะ IEmailService (templates + link building ยัง reuse ได้)

ทดสอบขั้นถัดไป (optional): dotnet run --project src/AuthMicroservice.Sample เพื่อยิง Swagger UI ที่ http://localhost:5100/swagger

## Changelog

### v1.0.2 — 2026-09-16

- **InMemory adapter packable**: เพิ่ม `AuthMicroservice.Migrations.InMemory` เป็น NuGet package ตัวที่ 5 (Core + Migrations.{SqlServer, Postgres, Sqlite, InMemory}) — ใช้ `.UseInMemory()` extension สำหรับ tests/demos (ไม่แนะนำสำหรับ production เพราะ data หายทุก restart)
- **`DatabaseProvider.InMemory` enum**: `AuthMicroservice:Database:Provider="InMemory"` ใช้งานได้แล้วใน config-driven dispatch ที่ `AuthMicroservice.Api/Program.cs` — connection string ถ้าใส่จะกลายเป็น database name, ถ้าเว้นว่างจะ fallback เป็น `"AuthMicroserviceInMemory"`
- **Sample switched to InMemory**: `AuthMicroservice.Sample` ใช้ `.UseInMemory()` แทน `.UseSqlite()` — รัน `dotnet run --project src/AuthMicroservice.Sample` ได้เลยโดยไม่ต้องสร้างไฟล์ `sample.db`

### v1.0.1 — 2026-09-14

- **Package metadata**: เพิ่ม `Version`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl` ใน `Directory.Build.props` — พร้อม `dotnet pack` เป็น NuGet ทั้ง 4 packages (Core + Migrations.SqlServer/Postgres/Sqlite)
- **Migrations regenerated**: Initial migration ของทั้ง 3 provider ถูก regenerate ใหม่ให้ตรง schema ปัจจุบัน (SqlServer / Postgres / Sqlite) ผ่าน env-var override workflow (`AuthMicroservice__Database__Provider=...`)
- **NuGet packaging ready**: 4 packable projects พร้อม pack — `AuthMicroservice.Core` + `AuthMicroservice.Migrations.{SqlServer,Postgres,Sqlite}` (build ด้วย `dotnet pack -c Release -o ./artifacts`)

### v1.0.0 — initial release

- Register/Login/Logout, JWT + refresh token rotation, email verification + password reset
- Provider-agnostic EF Core (SqlServer / Postgres / Sqlite / InMemory)
- Standalone API + library-mode consumer + Docker Compose + Sample + Unit/Integration tests

## Future: Next Plan
- v1.1 candidates: 2FA (TOTP), external OAuth providers (Google/Microsoft), rate limiting บน /auth/login + /auth/forgot-password, audit log ของ auth events
- Ops: HealthChecks (DB + SMTP), OpenTelemetry traces, structured logging correlationId

```
ถ้าพร้อมจะเอาไปใช้จริงใน project ใหม่ อย่าลืมเปลี่ยน Jwt__Key และ Database__ConnectionString ผ่าน env var ก่อน deploy — startup จะ fail-fast ถ้าคีย์ต่ำกว่า 32 chars
```