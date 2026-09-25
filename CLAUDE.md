# CLAUDE.md

ไฟล์นี้สำหรับ Claude Code / AI assistants ที่เข้ามาช่วยงานใน repo นี้
ดูภาพรวม feature, endpoint table, quick start ที่ [README.md](README.md) และ
คู่มือติดตั้ง OAuth providers ที่ [docs/PROVIDER_SETUP.md](docs/PROVIDER_SETUP.md)

## Overview

.NET 8 reusable auth component — ใช้ได้ทั้งเป็น NuGet library (`Synergy.AuthMicroservice.*`)
และเป็น standalone Web API (Docker) แนวทาง Keycloak-style แต่ lightweight, idiomatic .NET

## Solution layout (9 projects)

- `src/AuthMicroservice.Core/` — reusable library (services, endpoints, DI, DbContext)
- `src/AuthMicroservice.Api/` — standalone Web API host (Docker target, entry point [Program.cs](src/AuthMicroservice.Api/Program.cs))
- `src/AuthMicroservice.Sample/` — library-mode demo (InMemory, no email)
- `src/AuthMicroservice.Migrations.{SqlServer,Postgres,Sqlite,InMemory}/` — provider-specific EF migrations (แยกเป็นคนละ assembly เพื่อให้แต่ละ provider มี migration history ของตัวเอง)
- `tests/AuthMicroservice.UnitTests/` — xUnit + Moq + FluentAssertions
- `tests/AuthMicroservice.IntegrationTests/` — WebApplicationFactory + InMemory DB + Fake* clients

## Common commands (PowerShell)

Build / run / test:

```powershell
dotnet build
dotnet run --project src/AuthMicroservice.Api           # http://localhost:5099 + /swagger
dotnet run --project src/AuthMicroservice.Sample        # http://localhost:5100 (InMemory demo)
dotnet test                                             # unit + integration
dotnet test tests/AuthMicroservice.UnitTests            # unit only
dotnet test --filter "FullyQualifiedName~GoogleExternalLoginTests"  # single test class
```

Docker (standalone with SQL Server + Mailhog):

```powershell
Copy-Item .env.example .env    # แล้วแก้ JWT_KEY ให้ยาว ≥ 32 ตัวอักษร
docker compose up -d --build
```

## EF Core migrations — สำคัญ

Provider เลือกจาก config `AuthMicroservice:Database:Provider` (SqlServer / Postgres / Sqlite / InMemory) ที่ [Program.cs](src/AuthMicroservice.Api/Program.cs) dispatch เข้า `MigrationsAssembly` ต่างกันตาม provider

**Auto-migrate on startup** เปิดอยู่โดย default (`AutoMigrate: true`) — dev แทบไม่ต้องรัน `dotnet ef database update` เอง

**เวลาสร้าง/regenerate migrations สำหรับหลาย provider** ⚠️ อย่าไปแก้ `appsettings.json` เปลี่ยน provider ไปมา ให้ override ด้วย env var ต่อ session แทน:

```powershell
# ยกตัวอย่างสร้าง migration ใหม่สำหรับ Postgres
$env:AuthMicroservice__Database__Provider = "Postgres"
$env:AuthMicroservice__Database__ConnectionString = "Host=localhost;Database=AuthDb;Username=postgres;Password=..."
dotnet ef migrations add <MigrationName> `
  --project src/AuthMicroservice.Migrations.Postgres `
  --startup-project src/AuthMicroservice.Api
```

ทำซ้ำโดยเปลี่ยน `Provider` + `ConnectionString` + `--project` สำหรับ SqlServer / Sqlite (InMemory ไม่ต้อง)
เหตุผล: กัน `appsettings.json` โดน commit แบบ dirty และให้ทุก provider ได้ migration ตรงกัน

## Architecture — จุดหลักที่ควรรู้

- **DI entry point**: `builder.Services.AddAuthMicroservice(config)` ใน [ServiceCollectionExtensions.cs](src/AuthMicroservice.Core/Extensions/ServiceCollectionExtensions.cs) — คืน `IAuthMicroserviceBuilder` chainable
- **Endpoint mapping**: Minimal APIs เท่านั้น (ไม่ใช้ Controller) รวมที่ [AuthEndpoints.cs](src/AuthMicroservice.Core/Endpoints/AuthEndpoints.cs), mount ผ่าน `app.MapAuthMicroservice()` ที่ prefix `AuthMicroservice:RoutePrefix` (default `/auth`)
- **Business logic**: [AuthService.cs](src/AuthMicroservice.Core/Services/AuthService.cs) — endpoint → `IAuthService` → services ย่อย (`IJwtTokenService`, `IRefreshTokenService`, `IOtpService`, `IEmailSender`, `I{Provider}TokenValidator`, `I{Provider}OAuthClient`, `I{Provider}OidcClient`)
- **DbContext**: [AuthDbContext.cs](src/AuthMicroservice.Core/Data/AuthDbContext.cs) — `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` + `RefreshTokens`, `OtpCodes` schema `"auth"`
- **Options pattern**: config bind ไป `AuthMicroserviceOptions` และ sub-options (`DatabaseOptions`, `JwtOptions`, `EmailOptions`, `OtpOptions`, `ExternalProvidersOptions`, `IdentityOptions`, `TokenLinksOptions`, ...)
- **External providers**: แต่ละเจ้ามี pattern คล้ายกัน — `I{Provider}TokenValidator` สำหรับ token flow (Google/Microsoft/Facebook/LINE), `I{Provider}OAuthClient` สำหรับ backend code exchange (Google), `I{Provider}OidcClient` สำหรับ OIDC challenge/callback flow (LINE/ThaID) เพิ่ม provider ใหม่ = ทำ 3 อย่าง: options class + client/validator interface+impl + endpoint

## Configuration

Full schema อยู่ที่ [src/AuthMicroservice.Api/appsettings.json](src/AuthMicroservice.Api/appsettings.json)
Sections หลัก: `Database`, `Jwt`, `Email`, `Otp`, `ExternalProviders.{Google,Microsoft,Facebook,Line,ThaId}`, `Identity`, `TokenLinks`, `RoutePrefix`, `EnableSwagger`

⚠️ **อย่า commit secret จริง** ลง `appsettings.json` (JWT key, SMTP password, OAuth ClientSecret) — ใช้ env var หรือ user-secrets override

## Testing patterns

**Unit tests** ([tests/AuthMicroservice.UnitTests/](tests/AuthMicroservice.UnitTests/)) — Moq สำหรับ dependencies, InMemory DbContext ผ่าน static `Build()` helper ต่อ test class

**Integration tests** ([tests/AuthMicroservice.IntegrationTests/](tests/AuthMicroservice.IntegrationTests/)) — ใช้ [AuthApiFactory.cs](tests/AuthMicroservice.IntegrationTests/Infrastructure/AuthApiFactory.cs) เป็น `WebApplicationFactory<Program>` ที่:
- บังคับใช้ InMemory provider ด้วย DB name unique per run (`AuthMicroservice.Tests.{GUID}`)
- Replace real services ด้วย `Fake*` (FakeEmailSender, FakeGoogleOAuthClient, FakeMicrosoftTokenValidator, FakeLineTokenValidator, FakeLineOidcClient, FakeThaIdOidcClient) — Fake มี method ให้ pre-register (`RegisterCode`, `RegisterToken`) และเก็บประวัติเรียก
- อ่าน `appsettings.Test.json`

**เพิ่ม external provider ใหม่** ⇒ ต้องเพิ่ม `Fake{Provider}...` และ replace ที่ `AuthApiFactory` ด้วย ไม่งั้น integration test จะไปเรียกของจริง

## Coding conventions

- `net8.0`, `Nullable: enable`, `ImplicitUsings: enable` — บังคับผ่าน [Directory.Build.props](Directory.Build.props)
- `TreatWarningsAsErrors: false` (ไม่ strict) — แต่พยายามไม่ทิ้ง warning ใหม่
- CS1591 (missing XML doc) ถูก suppress — ไม่ต้องบังคับใส่ `///` เว้นแต่ที่ public API สำคัญ
- ไม่มี `.editorconfig` — ยึด default C# / `dotnet format`
- Central package versions: [Directory.Packages.props](Directory.Packages.props) (`ManagePackageVersionsCentrally=true`) — เวลาเพิ่ม package ต้องประกาศเวอร์ชันที่ไฟล์นี้ด้วย

## Gotchas ที่มักโดน

1. **AutoMigrate + InMemory** — InMemory ไม่มี migration ต้องเช็ค provider ก่อนเรียก `Database.Migrate()`; ของเดิมจัดการไว้ใน `ApplyAuthMicroserviceMigrationsAsync()` แล้ว
2. **RefreshToken reuse detection** — token เดิมที่ถูก rotate แล้ว ถ้าโดน replay จะ revoke ทั้ง chain (ดู `RefreshTokenService`); เวลาเขียน test ต้องระวังลำดับ
3. **OTP cooldown** — endpoints ที่ส่ง OTP มี rate-limit ต่อ email; test ต้อง advance time (มี `IClock` abstraction) หรือใช้ email ใหม่
4. **RFC 7807 error codes** — errors ใช้ ProblemDetails พร้อม `code` เฉพาะ (`INVALID_CREDENTIALS`, `TWO_FACTOR_REQUIRED`, `OTP_COOLDOWN_ACTIVE`, ...); ดู endpoint table ที่ [README.md](README.md)

## Docs อื่นๆ

- [docs/INSTALLATION.md](docs/INSTALLATION.md) — library mode + Docker Compose full guide
- [docs/PROVIDER_SETUP.md](docs/PROVIDER_SETUP.md) — วิธีขอ credential ของแต่ละ OAuth provider
- [docs/PACKAGING.md](docs/PACKAGING.md) — วิธี build .nupkg (5 packages: Core + 4 migrations)
