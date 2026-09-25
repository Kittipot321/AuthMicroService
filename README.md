# AuthMicroservice

Reusable authentication component for ASP.NET Core 8 — usable **both** as a plug-in library and as a standalone Web API microservice. Similar in spirit to Keycloak but lightweight and idiomatic .NET.

## Features (v1)

- Register / Login / Logout (email + password)
- JWT access tokens + refresh token rotation (hash-stored, revoke on reuse)
- Email verification link + password reset link (SMTP via MailKit; pluggable)
- Change password (revokes existing refresh tokens)
- Account lockout after N failed attempts
- Roles + custom claims (ASP.NET Core Identity underneath) — seed custom roles จาก config, สมัครใน role เฉพาะผ่าน `POST /auth/register` ได้ (ต้องอยู่ใน `AllowedSelfRegisterRoles`)
- Provider-agnostic EF Core — pick **SqlServer / Postgres / Sqlite / InMemory** in `appsettings.json`
- Email OTP + login 2FA — ดู [docs/OTP_2FA.md](docs/OTP_2FA.md)
- External login: Google / Microsoft / Facebook / LINE / ThaID — ดู [docs/EXTERNAL_PROVIDERS.md](docs/EXTERNAL_PROVIDERS.md)
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

> **ต้องการ step-by-step guide เต็มรูปแบบ?** ดู [docs/INSTALLATION.md](docs/INSTALLATION.md) — ครอบคลุมทั้ง library mode และ standalone (Docker Compose) พร้อม troubleshooting

Install via NuGet (published as `Synergy.AuthMicroservice.*` to avoid name collisions on nuget.org — the runtime assembly + `using AuthMicroservice.Core.*` namespaces are unchanged):

```powershell
dotnet add package Synergy.AuthMicroservice.Core
dotnet add package Synergy.AuthMicroservice.Migrations.SqlServer   # or .Postgres / .Sqlite / .InMemory
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
| POST | `/auth/register` | anon | 201 with tokens; sends verification email. Optional `role` field — ต้องอยู่ใน `AllowedSelfRegisterRoles` (ไม่งั้น 400 `INVALID_ROLE`) |
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
| POST | `/auth/external/google` | anon | Google `authorization_code` → JWT (backend แลก code เอา `id_token` ต่อกับ Google, auto-provision + auto-link). 404 unless `ExternalProviders:Google:Enabled=true` |
| POST | `/auth/external/microsoft` | anon | Microsoft `id_token` (Azure AD / MSA) → JWT. 404 unless `ExternalProviders:Microsoft:Enabled=true` |
| POST | `/auth/external/facebook` | anon | Facebook `access_token` → JWT. 404 unless `ExternalProviders:Facebook:Enabled=true` |
| POST | `/auth/external/line` | anon | LINE `id_token` (LIFF) → JWT. 404 unless `ExternalProviders:Line:Enabled=true` |
| GET | `/auth/external/challenge/line` | anon | `?returnUrl=...` → redirect ไปหน้า login LINE (สร้าง state + PKCE + nonce). ใช้เมื่อ `Line.ChannelSecret` ตั้งค่า (OIDC redirect flow แทน LIFF token-exchange) |
| GET | `/auth/external/callback/line` | anon | `?code=&state=` จาก LINE → verify state + nonce, แลก token, ออก JWT + refresh, redirect กลับ `returnUrl` |
| GET | `/auth/external/thaid/challenge` | anon | `?returnUrl=...` → redirect ไปหน้า login ของ ThaID (สร้าง state + PKCE, เก็บใน state store). 404 unless `ExternalProviders:ThaId:Enabled=true` |
| GET | `/auth/external/thaid/callback` | anon | `?code=&state=` จาก ThaID → verify state, แลก tokens ที่ Authority, ออก JWT + refresh, redirect กลับ `returnUrl` ที่ระบุใน challenge |
| POST | `/auth/otp/email/send` | anon | ส่ง 6-digit OTP ไปยัง email เพื่อ verify — silent success ถ้า email ไม่มี/verified แล้ว |
| POST | `/auth/otp/email/verify` | anon | `{email, code}` — ยืนยัน OTP → set `EmailConfirmed=true` |
| POST | `/auth/login/2fa/verify` | anon | `{email, code}` — ยืนยัน login OTP หลัง `/auth/login` ตอบ 202 (`TWO_FACTOR_REQUIRED`) |
| POST | `/auth/login/2fa/resend` | anon | `{email}` — ขอส่ง OTP login 2FA ใหม่. Silent success ทุกกรณี (กัน enumeration); คืน 429 `OTP_COOLDOWN_ACTIVE` ถ้ายิงถี่เกิน |
| POST | `/auth/2fa/enable-request` | auth | ส่ง OTP ไป email เพื่อเปิด 2FA |
| POST | `/auth/2fa/enable-confirm` | auth | `{code}` — ยืนยัน OTP → `TwoFactorEnabled=true` |
| POST | `/auth/2fa/disable` | auth | `{password}` — ปิด 2FA (ต้องยืนยัน password) |

Errors follow RFC 7807 ProblemDetails with error codes such as `INVALID_CREDENTIALS`, `USER_LOCKED_OUT`, `INVALID_REFRESH_TOKEN`, `EMAIL_EXISTS_UNVERIFIED`, `INVALID_ROLE` (400 — role requested at register ไม่อยู่ใน `AllowedSelfRegisterRoles` / ไม่มีใน DB), OTP-related: `TWO_FACTOR_REQUIRED` (202 — login สำเร็จแต่ต้อง verify OTP), `INVALID_OTP` / `OTP_EXPIRED` / `OTP_ATTEMPTS_EXCEEDED` (401), `OTP_COOLDOWN_ACTIVE` (429), `OTP_DISABLED` (404), `TWOFA_NOT_ENABLED` / `TWOFA_ALREADY_ENABLED` / `EMAIL_ALREADY_VERIFIED` (409), and per-provider variants: `INVALID_GOOGLE_TOKEN` / `GOOGLE_EMAIL_NOT_VERIFIED`, `INVALID_MICROSOFT_TOKEN`, `INVALID_FACEBOOK_TOKEN` / `FACEBOOK_EMAIL_REQUIRED`, `INVALID_LINE_TOKEN` / `INVALID_LINE_STATE` (400) / `LINE_RETURN_URL_NOT_ALLOWED` (400), `INVALID_THAID_STATE` (400) / `INVALID_THAID_CODE` (401) / `THAID_RETURN_URL_NOT_ALLOWED` (400), plus `{PROVIDER}_LOGIN_DISABLED` (404) when a provider is not enabled.

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

## Frontend demo (React + Vite)

`frontend/` เป็น React + TypeScript dev harness สำหรับสาธิต flow login กับ Sample ครอบคลุม `/auth/login` (+ `202` two-factor branch → `/auth/login/2fa/verify`), `/auth/me`, และ `/auth/logout`. ใช้ Vite dev proxy `/auth` → `http://localhost:5100/auth` จึงไม่ต้องตั้งค่า CORS บน backend

```powershell
dotnet run --project src/AuthMicroservice.Sample   # backend at :5100
cd frontend
npm install
npm run dev                                        # frontend at :5173
```

รายละเอียดใน [frontend/README.md](frontend/README.md)

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

## OTP / 2FA

Email-based OTP รองรับ 2 flows — **email verification** (ทดแทน/เสริม verify-email link) และ **login two-factor** (ส่ง OTP หลัง password ผ่าน สำหรับ user ที่เปิด 2FA). ครบทั้ง flow diagram, PowerShell examples, endpoint toggle table และ config knobs ที่ [docs/OTP_2FA.md](docs/OTP_2FA.md)

## Configuration

Config เต็ม + startup validation rules อยู่ที่ [docs/CONFIGURATION.md](docs/CONFIGURATION.md). Minimum viable config:

```jsonc
{
  "AuthMicroservice": {
    "Database": { "Provider": "SqlServer", "ConnectionString": "..." },
    "Jwt":      { "Key": "REPLACE_FROM_ENV_MIN_32_CHARS" },
    "Email":    { "Enabled": true, "FromAddress": "no-reply@example.com",
                  "Smtp": { "Host": "localhost", "Port": 1025 } }
  }
}
```

Secrets ใช้ env var แบบ double-underscore (`AuthMicroservice__Jwt__Key`, `AuthMicroservice__Database__ConnectionString`, `AuthMicroservice__Email__Smtp__Password`) — อย่า commit secret ลง `appsettings.json`

## Custom roles + assignable role at register

**System roles** — `Admin` และ `User` ถูก seed อัตโนมัติทุกครั้ง (`IsSystem=true`) ห้ามเปลี่ยน/ห้ามใช้ชื่อซ้ำใน custom roles

**Custom roles** — เพิ่ม role ใหม่ผ่าน config `Identity.Roles.AdditionalRoles` โดยไม่ต้องแตะ DB เอง (idempotent upsert — เปลี่ยน `Description` ได้ทุกครั้ง app start):

```jsonc
"AuthMicroservice": {
  "Identity": {
    "Roles": {
      "DefaultRegistrationRole": "User",
      "AllowedSelfRegisterRoles": [ "User", "Moderator" ],
      "AdditionalRoles": [
        { "Name": "Moderator",      "Description": "Can moderate user content" },
        { "Name": "ContentCreator", "Description": "Can create and publish content" }
      ]
    }
  }
}
```

**Role assignment ตอน register** — `POST /auth/register` รับ optional `role` field:

- ไม่ส่ง `role` (หรือส่ง empty) → assign `DefaultRegistrationRole` (default `"User"`)
- ส่ง `role` = `DefaultRegistrationRole` หรือค่าที่อยู่ใน `AllowedSelfRegisterRoles` → assign role นั้น
- ส่ง `role` ที่ไม่ผ่าน whitelist หรือไม่มีใน DB → **400 `INVALID_ROLE`** (validation ก่อน create user — ไม่มี orphan)

```http
POST /auth/register
Content-Type: application/json

{ "email": "mod@example.com", "password": "P@ssw0rd!", "fullName": "Mod", "role": "Moderator" }
```

**`ApplicationRole` metadata** — DB schema เพิ่ม 3 columns (migration `AddRoleMetadata`): `Description` (nvarchar(256), nullable), `IsSystem` (bit, `true` เฉพาะ Admin/User), `CreatedAtUtc` (datetime2). Apply อัตโนมัติเมื่อ `AutoMigrate=true`

**External login flows** (Google / Microsoft / Facebook / LINE / ThaID) ยังคง assign `AuthRoles.User` ให้ user ที่ auto-provision — **ไม่รับ** `role` parameter (ถ้าต้องการควบคุมสิทธิ์ให้ผ่าน admin promote role ทีหลัง)

## External login providers

รองรับ 5 providers ผ่าน 2 patterns:

| Provider | Endpoint | Flow |
|---|---|---|
| Google | `POST /auth/external/google` | authorization-code exchange (backend ถือ `ClientSecret`) |
| Microsoft | `POST /auth/external/microsoft` | id_token exchange (OIDC metadata) |
| Facebook | `POST /auth/external/facebook` | access_token + Graph `debug_token` |
| LINE (Mode A) | `POST /auth/external/line` | LIFF id_token exchange |
| LINE (Mode B) | `GET /auth/external/challenge/line` + `/callback/line` | OIDC redirect (เปิดโดยตั้ง `ChannelSecret`) |
| ThaID | `GET /auth/external/thaid/challenge` + `/callback` | OIDC redirect (server-hosted PKCE — DOPA บังคับ) |

ทั้งหมด **disabled by default**. Config, validation quirks, error mapping, env-var examples และ browser test harness setup ครบที่ [docs/EXTERNAL_PROVIDERS.md](docs/EXTERNAL_PROVIDERS.md). วิธีขอ credential จาก provider console (Google Cloud / Azure Portal / Meta / LINE Developers / DOPA) อยู่ที่ [docs/PROVIDER_SETUP.md](docs/PROVIDER_SETUP.md)

## EF Core migrations (per provider)

> **จะ pack เป็น NuGet เอง?** ดู [docs/PACKAGING.md](docs/PACKAGING.md) — pre-pack checklist, `dotnet pack` workflow, local folder feed สำหรับ smoke test ก่อน publish จริง

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

## Changelog

Version history อยู่ที่ [CHANGELOG.md](CHANGELOG.md) — ล่าสุด **v1.3.2** (2026-09-25) เปลี่ยน Google login เป็น authorization-code flow (⚠️ breaking — frontend ต้อง migrate ไป `initCodeClient`)

## Future: Next Plan
- v1.x candidates: ~~2FA (Email OTP)~~ ✅ / TOTP (authenticator apps) / SMS OTP, external OAuth providers (~~Google~~ ✅ / ~~Microsoft~~ ✅ / ~~Facebook~~ ✅ / ~~LINE~~ ✅ / ~~ThaID~~ ✅), rate limiting บน /auth/login + /auth/forgot-password, audit log ของ auth events
- Ops: HealthChecks (DB + SMTP), OpenTelemetry traces, structured logging correlationId

```
ถ้าพร้อมจะเอาไปใช้จริงใน project ใหม่ อย่าลืมเปลี่ยน Jwt__Key และ Database__ConnectionString ผ่าน env var ก่อน deploy — startup จะ fail-fast ถ้าคีย์ต่ำกว่า 32 chars
```
