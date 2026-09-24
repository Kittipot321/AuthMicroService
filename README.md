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
| POST | `/auth/external/google` | anon | Google `id_token` → JWT (auto-provision + auto-link). 404 unless `ExternalProviders:Google:Enabled=true` |
| POST | `/auth/external/microsoft` | anon | Microsoft `id_token` (Azure AD / MSA) → JWT. 404 unless `ExternalProviders:Microsoft:Enabled=true` |
| POST | `/auth/external/facebook` | anon | Facebook `access_token` → JWT. 404 unless `ExternalProviders:Facebook:Enabled=true` |
| POST | `/auth/external/line` | anon | LINE `id_token` (LIFF) → JWT. 404 unless `ExternalProviders:Line:Enabled=true` |
| GET | `/auth/external/thaid/challenge` | anon | `?returnUrl=...` → redirect ไปหน้า login ของ ThaID (สร้าง state + PKCE, เก็บใน state store). 404 unless `ExternalProviders:ThaId:Enabled=true` |
| GET | `/auth/external/thaid/callback` | anon | `?code=&state=` จาก ThaID → verify state, แลก tokens ที่ Authority, ออก JWT + refresh, redirect กลับ `returnUrl` ที่ระบุใน challenge |
| POST | `/auth/otp/email/send` | anon | ส่ง 6-digit OTP ไปยัง email เพื่อ verify — silent success ถ้า email ไม่มี/verified แล้ว |
| POST | `/auth/otp/email/verify` | anon | `{email, code}` — ยืนยัน OTP → set `EmailConfirmed=true` |
| POST | `/auth/login/2fa/verify` | anon | `{email, code}` — ยืนยัน login OTP หลัง `/auth/login` ตอบ 202 (`TWO_FACTOR_REQUIRED`) |
| POST | `/auth/2fa/enable-request` | auth | ส่ง OTP ไป email เพื่อเปิด 2FA |
| POST | `/auth/2fa/enable-confirm` | auth | `{code}` — ยืนยัน OTP → `TwoFactorEnabled=true` |
| POST | `/auth/2fa/disable` | auth | `{password}` — ปิด 2FA (ต้องยืนยัน password) |

Errors follow RFC 7807 ProblemDetails with error codes such as `INVALID_CREDENTIALS`, `USER_LOCKED_OUT`, `INVALID_REFRESH_TOKEN`, `EMAIL_EXISTS_UNVERIFIED`, `INVALID_ROLE` (400 — role requested at register ไม่อยู่ใน `AllowedSelfRegisterRoles` / ไม่มีใน DB), OTP-related: `TWO_FACTOR_REQUIRED` (202 — login สำเร็จแต่ต้อง verify OTP), `INVALID_OTP` / `OTP_EXPIRED` / `OTP_ATTEMPTS_EXCEEDED` (401), `OTP_COOLDOWN_ACTIVE` (429), `OTP_DISABLED` (404), `TWOFA_NOT_ENABLED` / `TWOFA_ALREADY_ENABLED` (409), and per-provider variants: `INVALID_GOOGLE_TOKEN` / `GOOGLE_EMAIL_NOT_VERIFIED`, `INVALID_MICROSOFT_TOKEN`, `INVALID_FACEBOOK_TOKEN` / `FACEBOOK_EMAIL_REQUIRED`, `INVALID_LINE_TOKEN`, `INVALID_THAID_STATE` (400) / `INVALID_THAID_CODE` (401) / `THAID_RETURN_URL_NOT_ALLOWED` (400), plus `{PROVIDER}_LOGIN_DISABLED` (404) when a provider is not enabled.

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
      "Templates": {
        "VerifyEmailSubject": "Verify your email",
        "PasswordResetSubject": "Reset your password",
        "OtpEmailVerificationSubject": "Your verification code",
        "OtpLoginTwoFactorSubject": "Your login code"
      }
    },
    "Otp": {
      "CodeLength": 6,                       // 4–10 digits
      "ExpirationMinutes": 10,
      "MaxAttempts": 5,                      // per code, before it's invalidated
      "ResendCooldownSeconds": 60,           // between successive generate calls for same user+purpose
      "EmailVerification": { "Enabled": true },
      "LoginTwoFactor":    { "Enabled": true }
    },
    "Identity": {
      "Password": { "RequiredLength": 8, "RequireDigit": true, "RequireLowercase": true, "RequireUppercase": true, "RequireNonAlphanumeric": true, "RequiredUniqueChars": 1 },
      "Lockout":  { "AllowedForNewUsers": true, "MaxFailedAccessAttempts": 5, "DefaultLockoutMinutes": 15 },
      "SignIn":   { "RequireConfirmedEmail": true, "RequireConfirmedPhoneNumber": false },
      "User":     { "RequireUniqueEmail": true },
      "Roles": {
        "DefaultRegistrationRole": "User",              // role ที่ assign ให้ user ใหม่เมื่อ register ไม่ส่ง `role` field
        "AllowedSelfRegisterRoles": [ ],                // whitelist role ที่ client ขอผ่าน POST /auth/register ได้ (นอกเหนือ default)
        "AdditionalRoles": [                            // seed custom roles เพิ่มจาก system roles (Admin, User)
          // { "Name": "Moderator", "Description": "Can moderate user content" }
        ]
      }
    },
    "TokenLinks": {
      "EmailVerificationBaseUrl": "https://app.example.com/verify-email",
      "PasswordResetBaseUrl": "https://app.example.com/reset-password"
    },
    "ExternalProviders": {
      "Google":    { "Enabled": false, "ClientId": "" },
      "Microsoft": { "Enabled": false, "ClientId": "", "TenantId": "common" },
      "Facebook":  { "Enabled": false, "AppId": "", "AppSecret": "", "GraphApiVersion": "v18.0" },
      "Line":      { "Enabled": false, "ChannelId": "", "VerifyEndpoint": "https://api.line.me/oauth2/v2.1/verify" },
      "ThaId":     {
        "Enabled": false,
        "ClientId": "", "ClientSecret": "",
        "Authority": "https://imauthtestc.bora.dopa.go.th/api/v2/oauth2",   // sandbox default; prod = https://imauth.bora.dopa.go.th/api/v2/oauth2
        "RedirectUri": "https://localhost:5100/auth/external/thaid/callback",
        "AllowedReturnUrlPrefixes": [ "http://localhost:5173", "https://localhost:5100" ],
        "Scopes": "openid pid given_name family_name email birthdate address",
        "StateLifetimeMinutes": 10
      }
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

Startup fails fast if `Jwt.Key` is under 32 chars, an unknown DB provider is set, `Email.Enabled=true` without an SMTP host, or any enabled external provider is missing its required credentials — Google/Microsoft need `ClientId` (Microsoft also `TenantId`), Facebook needs `AppId` + `AppSecret`, LINE needs `ChannelId`, ThaID needs `ClientId` + `ClientSecret` + `RedirectUri`. `Identity.Roles` ก็ถูก validate — `AdditionalRoles[].Name` ห้ามว่าง / เกิน 256 chars / ชนกับ system role (`Admin`/`User`) / ซ้ำกัน, และ `DefaultRegistrationRole` + ทุก entry ใน `AllowedSelfRegisterRoles` ต้องอ้างถึง role ที่มีอยู่จริง (system หรือ `AdditionalRoles`).

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

Five providers are supported: **Google**, **Microsoft**, **Facebook**, **LINE**, and **ThaID** (Thai national digital ID). The first four use a **token-exchange** flow — client (SPA / mobile) obtains a provider token via the native SDK, POSTs it to this service, and gets back this service's own JWT + refresh token (no cookie/redirect handshake). **ThaID is the exception**: it uses the classic **OIDC redirect flow** (server-hosted `/challenge` + `/callback`) because ThaID mandates it — so ThaID needs a server-side callback URL registered with DOPA.

> **จะเอาคีย์แต่ละ provider มาจากไหน?** ดู [docs/PROVIDER_SETUP.md](docs/PROVIDER_SETUP.md) — step-by-step guide ตั้งแต่สมัคร Developer Console ของ Google / Microsoft / Facebook / LINE / DOPA จนได้ credentials มาวางใน `appsettings.json`

### Shared behaviour (all providers)

1. Validates the incoming token with the provider (signature/audience/expiry, or provider debug endpoint for opaque tokens).
2. If a link already exists in `AspNetUserLogins` for `(Provider, subject)` → issues tokens.
3. Else if a local user with the same email exists:
   - `EmailConfirmed=true` → auto-links the external identity and issues tokens.
   - `EmailConfirmed=false` → returns `EMAIL_EXISTS_UNVERIFIED` (409). Verify the local account first (via `/auth/verify-email`) before retrying, to prevent account takeover through unverified addresses.
4. Else → auto-provisions a new `ApplicationUser` with `EmailConfirmed=true`, assigns the `User` role, links the external identity, and issues tokens.

Every provider is **disabled by default** in [appsettings.json](src/AuthMicroservice.Api/appsettings.json) — a disabled endpoint responds 404 (`{PROVIDER}_LOGIN_DISABLED`). Startup fail-fast if `Enabled=true` without required credentials.

Ready-to-run browser test harnesses (grab a real token from the provider and POST it — or, for ThaID, kick off the redirect flow) sit under `test-html/`: [test-html/test-google.html](test-html/test-google.html), [test-html/test-microsoft.html](test-html/test-microsoft.html), [test-html/test-facebook.html](test-html/test-facebook.html), [test-html/test-line.html](test-html/test-line.html), [test-html/test-thaid.html](test-html/test-thaid.html).

### Google

- **Endpoint**: `POST /auth/external/google` — body `{ "idToken": "..." }`
- **Config**: `AuthMicroservice:ExternalProviders:Google:{ Enabled, ClientId }`
- **Validation**: Google JWKS — signature, `aud` = `ClientId`, `exp`
- **Provider quirks**: rejects with `GOOGLE_EMAIL_NOT_VERIFIED` (400) if Google's `email_verified` claim is false
- **Errors**: `INVALID_GOOGLE_TOKEN` (401), `GOOGLE_EMAIL_NOT_VERIFIED` (400), `GOOGLE_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Google__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Google__ClientId = "<your>.apps.googleusercontent.com"
```

### Microsoft

- **Endpoint**: `POST /auth/external/microsoft` — body `{ "idToken": "..." }`
- **Config**: `AuthMicroservice:ExternalProviders:Microsoft:{ Enabled, ClientId, TenantId }` — `TenantId` accepts `common` / `organizations` / `consumers` / a specific tenant GUID (default `common`)
- **Validation**: OpenID Connect metadata (`Microsoft.IdentityModel.Protocols.OpenIdConnect`) — signature, `aud` = `ClientId`, `iss` matches the resolved tenant, `exp`
- **Provider quirks**: Microsoft-issued email is treated as verified by default; no separate email-verification step
- **Errors**: `INVALID_MICROSOFT_TOKEN` (401), `MICROSOFT_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Microsoft__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Microsoft__ClientId = "<app-registration-guid>"
$env:AuthMicroservice__ExternalProviders__Microsoft__TenantId = "common"
```

### Facebook

- **Endpoint**: `POST /auth/external/facebook` — body `{ "accessToken": "..." }`
- **Config**: `AuthMicroservice:ExternalProviders:Facebook:{ Enabled, AppId, AppSecret, GraphApiVersion }` (default `GraphApiVersion` = `v18.0`)
- **Validation**: Graph `debug_token` (asserts token was issued to `AppId` and not expired) → then `GET /{version}/me?fields=id,email,name,picture` via `IHttpClientFactory`
- **Provider quirks**: email is an optional scope — if the user did not grant it (or their Facebook account has no email), returns `FACEBOOK_EMAIL_REQUIRED` (400). Facebook does not report a verified-email flag; auto-provisioned users are marked `EmailConfirmed=true` on the trust that Facebook already verified it
- **Errors**: `INVALID_FACEBOOK_TOKEN` (401), `FACEBOOK_EMAIL_REQUIRED` (400), `FACEBOOK_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Facebook__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Facebook__AppId = "<app-id>"
$env:AuthMicroservice__ExternalProviders__Facebook__AppSecret = "<app-secret>"
```

### LINE

- **Endpoint**: `POST /auth/external/line` — body `{ "idToken": "..." }` (obtained from LIFF via `liff.getIDToken()`)
- **Config**: `AuthMicroservice:ExternalProviders:Line:{ Enabled, ChannelId, VerifyEndpoint }` (default endpoint `https://api.line.me/oauth2/v2.1/verify`)
- **Validation**: POST `id_token` + `ChannelId` to LINE verify endpoint — validates `aud` = `ChannelId`, `iss` = `https://access.line.me`, `exp`
- **Provider quirks**: email is optional in the LINE Login scope. If the user's channel/consent does not include email, the user is still auto-provisioned with a synthesized placeholder email `{subject}@line.local` and `EmailConfirmed=false` (same pattern as ThaID). If email *is* returned, it is stored as-is with `EmailConfirmed=true`.
- **Errors**: `INVALID_LINE_TOKEN` (401), `LINE_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Line__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Line__ChannelId = "<line-login-channel-id>"
```

### ThaID (Thai national digital ID / DOPA)

- **Endpoints (redirect flow, no token-exchange)**:
  - `GET /auth/external/thaid/challenge?returnUrl=<frontend-url>` — generates state + PKCE code_verifier, stores them via `IThaIdStateStore` (in-memory by default), then 302-redirects the browser to ThaID's `authorize` endpoint
  - `GET /auth/external/thaid/callback?code=&state=` — invoked by ThaID after the user logs in; verifies state + PKCE, exchanges `code` for tokens at `Authority`, issues this service's JWT + refresh, then 302-redirects back to the original `returnUrl` with the tokens appended
- **Config**: `AuthMicroservice:ExternalProviders:ThaId:{ Enabled, ClientId, ClientSecret, Authority, RedirectUri, AllowedReturnUrlPrefixes, Scopes, StateLifetimeMinutes }`
  - `Authority` default (sandbox) `https://imauthtestc.bora.dopa.go.th/api/v2/oauth2` — for production use `https://imauth.bora.dopa.go.th/api/v2/oauth2`
  - `RedirectUri` **ต้องตรงกับ** URL ที่ลงทะเบียนไว้กับ DOPA (เช่น `https://localhost:5100/auth/external/thaid/callback` ตอน dev)
  - `AllowedReturnUrlPrefixes` = whitelist ของ frontend URL prefix ที่ยอมให้ redirect กลับ (open-redirect guard)
  - `Scopes` default `openid pid given_name family_name email birthdate address`
  - `StateLifetimeMinutes` default `10`
- **Validation**: OIDC metadata จาก `Authority`, PKCE (S256), state verification ผ่าน `IThaIdStateStore`
- **Provider quirks**: email เป็น optional scope — ถ้า ThaID ไม่ส่ง email กลับมา, user ถูก auto-provision ด้วย placeholder `{pid}@thaid.local` + `EmailConfirmed=false` (pattern เดียวกับ LINE). ถ้ามี email ก็ใช้ตามที่ได้ + `EmailConfirmed=true`
- **Errors**: `INVALID_THAID_STATE` (400 — state ไม่ตรง/หมดอายุ), `INVALID_THAID_CODE` (401 — แลก token ไม่ผ่าน), `THAID_RETURN_URL_NOT_ALLOWED` (400 — `returnUrl` ไม่อยู่ใน `AllowedReturnUrlPrefixes`), `THAID_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__ThaId__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__ThaId__ClientId = "<dopa-client-id>"
$env:AuthMicroservice__ExternalProviders__ThaId__ClientSecret = "<dopa-client-secret>"
$env:AuthMicroservice__ExternalProviders__ThaId__RedirectUri = "https://localhost:5100/auth/external/thaid/callback"
```

### Sample request

Same shape for the four token-exchange providers — only the path and the token field name differ (`idToken` for Google/Microsoft/LINE, `accessToken` for Facebook):

```http
POST /auth/external/google
Content-Type: application/json

{ "idToken": "eyJhbGciOi..." }
```

**ThaID is different**: the browser starts by navigating to `GET /auth/external/thaid/challenge?returnUrl=https://myapp/login-callback` (no body); the service handles the rest of the OIDC dance and eventually redirects the browser back to `returnUrl` with the issued tokens.

### Running the browser test harnesses over HTTPS

Provider SDKs (LIFF, Google Identity, MSAL, Facebook Login) require the page to be served over HTTPS — `file://` and plain `http://` won't work. Serve the repo root with [`dotnet-serve`](https://github.com/natemcmaster/dotnet-serve) — dev cert is generated automatically:

```powershell
dotnet tool install -g dotnet-serve                    # one-time install
dotnet dev-certs https --trust                          # one-time trust
dotnet serve -d c:\Code\AuthMicroServices -p 5001 -S    # -S = HTTPS
```

เปิด `https://localhost:5001/test-html/test-line.html` (หรือ `test-google.html` / `test-microsoft.html` / `test-facebook.html` / `test-thaid.html`) เพื่อทดสอบแต่ละ provider.

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

### v1.3.0 — 2026-09-23

- **Email OTP (6-digit)** สำหรับ 2 flows — ทางเลือกคู่ขนานกับ link-based flow เดิม (toggle ได้):
  - **Email verification via OTP** — `POST /auth/otp/email/send` + `POST /auth/otp/email/verify` (แทน/เสริม verify-email link)
  - **Login 2FA** — user ที่ `TwoFactorEnabled=true` เมื่อ login สำเร็จด้วย password จะได้ 202 (`TWO_FACTOR_REQUIRED`) + OTP ส่งไปที่ email → ต้อง `POST /auth/login/2fa/verify {email, code}` เพื่อรับ JWT. Enable/disable ผ่าน `POST /auth/2fa/enable-request` → `POST /auth/2fa/enable-confirm` (auth required) และ `POST /auth/2fa/disable` (require password confirm)
  - Password reset ยังใช้ token-based link flow เดิม (`POST /auth/forgot-password` + `POST /auth/reset-password`) — ไม่มี OTP variant
- **Storage & security**: OTP เก็บใน `auth.OtpCodes` (ไม่เก็บ plaintext) — SHA-256(code + per-code random salt), constant-time compare, per-user+purpose invalidation ก่อน generate ใหม่, per-code MaxAttempts + resend cooldown, silent-success สำหรับ send endpoints (กัน account enumeration). Migration `AddOtpCodes` ครบ 3 provider (SqlServer / Postgres / Sqlite).
- **Config**: ใหม่ `AuthMicroservice:Otp:{CodeLength, ExpirationMinutes, MaxAttempts, ResendCooldownSeconds, {EmailVerification, LoginTwoFactor}.Enabled}` — startup validate `CodeLength ∈ [4,10]`, `ExpirationMinutes > 0`, ฯลฯ. Endpoint toggles ใหม่ใน `AuthMicroservice:Endpoints:*` — ทุก endpoint เปิด/ปิด/hide-from-swagger ได้แยกกันเหมือน pattern เดิม.
- **Error codes ใหม่**: `TWO_FACTOR_REQUIRED` (202), `INVALID_OTP` / `OTP_EXPIRED` / `OTP_ATTEMPTS_EXCEEDED` (401), `OTP_COOLDOWN_ACTIVE` (429), `OTP_DISABLED` (404), `TWOFA_NOT_ENABLED` / `TWOFA_ALREADY_ENABLED` (409).
- **Test harness**: [test-html/test-otp.html](test-html/test-otp.html) — Bootstrap 5 single-page console ครอบ 2 flows พร้อม log JSON response
- **Extending `IEmailService`**: เพิ่ม `SendOtpAsync(user, code, purpose, expiresInMinutes, ct)` + 2 embedded HTML templates (`otp-email-verification.html`, `otp-login-2fa.html`) — replace ได้เหมือน `IEmailSender` เดิม

### v1.2.1 — 2026-09-22

- **Custom roles + assignable role at registration**: new config `AuthMicroservice:Identity:Roles:{DefaultRegistrationRole, AllowedSelfRegisterRoles, AdditionalRoles}` — seed extra roles (Moderator, ContentCreator, ฯลฯ) ผ่าน config โดยไม่ต้องแตะ DB เอง. `POST /auth/register` รับ optional `role` field — ถ้าอยู่ใน `AllowedSelfRegisterRoles` (หรือตรงกับ `DefaultRegistrationRole`) จะ assign role นั้นให้ user ใหม่, ไม่งั้น 400 `INVALID_ROLE`. Role validation happens **ก่อน** create user (fail-fast — no orphan users). External login flows (Google / Microsoft / Facebook / LINE / ThaID) ยังคง assign เฉพาะ `AuthRoles.User` — ไม่รับ `role` parameter.
- **`ApplicationRole` metadata**: เพิ่ม 3 columns — `Description` (nvarchar(256), nullable), `IsSystem` (bit, `true` สำหรับ Admin/User และ `false` สำหรับ custom roles), `CreatedAtUtc` (datetime2). Migration `AddRoleMetadata` generate ครบทั้ง 3 provider (SqlServer / Postgres / Sqlite) — apply อัตโนมัติถ้า `AutoMigrate=true`.
- **Startup fail-fast validation for roles**: `AdditionalRoles[].Name` required + ≤256 chars + ห้ามชนกับ system role + ห้าม duplicate; `AdditionalRoles[].Description` ≤256 chars; `DefaultRegistrationRole` required + ต้องอ้างถึง role ที่มีอยู่จริง; `AllowedSelfRegisterRoles[]` required + ต้องอ้างถึง role ที่มีอยู่จริง + ห้าม duplicate. Error code ใหม่: `INVALID_ROLE` (400).
- **Backward compatible**: `role` field เป็น optional (payload เดิม fallback = `DefaultRegistrationRole = "User"`). Default `AllowedSelfRegisterRoles = []` และ `AdditionalRoles = []` — ถ้าไม่ตั้ง config อะไรเลย พฤติกรรมเหมือนก่อนหน้าทุกอย่าง.

### v1.2.0 — 2026-09-22

- **ThaID external login (Thai national digital ID / DOPA)**: new redirect-based OIDC flow — `GET /auth/external/thaid/challenge?returnUrl=...` เริ่ม flow (สร้าง state + PKCE, redirect ไป ThaID authorize) และ `GET /auth/external/thaid/callback?code=&state=` แลก tokens + ออก JWT/refresh + redirect กลับ `returnUrl`. Config `AuthMicroservice:ExternalProviders:ThaId:{Enabled, ClientId, ClientSecret, Authority, RedirectUri, AllowedReturnUrlPrefixes, Scopes, StateLifetimeMinutes}` — sandbox authority `https://imauthtestc.bora.dopa.go.th/api/v2/oauth2`, prod `https://imauth.bora.dopa.go.th/api/v2/oauth2`. State + PKCE verified via `IThaIdStateStore` (in-memory default). Email เป็น optional scope — user ที่ไม่มี email ถูก auto-provision เป็น `{pid}@thaid.local` + `EmailConfirmed=false`. `AllowedReturnUrlPrefixes` เป็น open-redirect guard. Errors: `INVALID_THAID_STATE` (400), `INVALID_THAID_CODE` (401), `THAID_RETURN_URL_NOT_ALLOWED` (400), `THAID_LOGIN_DISABLED` (404).
- **NuGet package rebrand**: package IDs เปลี่ยน `Kittipot.AuthMicroservice.*` → `Synergy.AuthMicroservice.*` (Core + Migrations.SqlServer/Postgres/Sqlite/InMemory) เพื่อสะท้อน ownership ของ Synergy Software — assembly names และ `using AuthMicroservice.Core.*` namespaces คงเดิม (source-compatible, แต่ผู้ใช้ที่ install จาก NuGet ต้อง `dotnet remove package Kittipot.AuthMicroservice.*` แล้ว `dotnet add package Synergy.AuthMicroservice.*`).
- **Test harnesses reorganized + populated**: ย้าย `test-*.html` จาก repo root → [`test-html/`](test-html/) folder และเติมค่า client identifier ตัวอย่างจริงในแต่ละไฟล์ (Facebook AppId, Google Client ID, Microsoft Client ID, LINE LIFF ID) ให้กดปุ่มแล้วทดสอบได้ทันที + เพิ่ม [test-html/test-thaid.html](test-html/test-thaid.html).
- **Local HTTPS testing docs**: เพิ่มขั้นตอน `dotnet-serve -S` + `dotnet dev-certs https --trust` ใน External login providers section — จำเป็นสำหรับ provider SDK ที่บังคับ HTTPS (LIFF, Google Identity, MSAL, Facebook Login).

### v1.1.1 — 2026-09-21

Adds three more external login providers on top of Google, sharing the same token-exchange flow (auto-link when local email is verified, reject `EMAIL_EXISTS_UNVERIFIED` otherwise, auto-provision new users with `EmailConfirmed=true` + `User` role) and the same fail-fast startup checks.

- **Microsoft external login**: new `POST /auth/external/microsoft` accepting a Microsoft `id_token` (Azure AD or personal MSA). Config `AuthMicroservice:ExternalProviders:Microsoft:{Enabled, ClientId, TenantId}` — `TenantId` accepts `common` / `organizations` / `consumers` / a specific tenant GUID. Validation via OpenID Connect metadata (package: `Microsoft.IdentityModel.Protocols.OpenIdConnect`). Microsoft-issued email is treated as verified by default. Errors: `INVALID_MICROSOFT_TOKEN` (401), `MICROSOFT_LOGIN_DISABLED` (404).
- **Facebook external login**: new `POST /auth/external/facebook` accepting a Facebook `access_token`. Config `AuthMicroservice:ExternalProviders:Facebook:{Enabled, AppId, AppSecret, GraphApiVersion}` (default `v18.0`). Validation via Graph `debug_token` then `GET /me?fields=id,email,name,picture` via `IHttpClientFactory`. Errors: `INVALID_FACEBOOK_TOKEN` (401), `FACEBOOK_EMAIL_REQUIRED` (400 — user did not grant the email scope), `FACEBOOK_LOGIN_DISABLED` (404).
- **LINE external login**: new `POST /auth/external/line` accepting a LINE `id_token` (typically from LIFF `liff.getIDToken()`). Config `AuthMicroservice:ExternalProviders:Line:{Enabled, ChannelId, VerifyEndpoint}` (default endpoint `https://api.line.me/oauth2/v2.1/verify`). `ChannelId` is used as both the verify-endpoint client_id and the expected `aud`; `iss` must equal `https://access.line.me`. Email is optional — when LINE does not return email, users are auto-provisioned with a placeholder `{subject}@line.local` and `EmailConfirmed=false`. Errors: `INVALID_LINE_TOKEN` (401), `LINE_LOGIN_DISABLED` (404).
- **Browser test harnesses**: added [test-google.html](test-google.html), [test-microsoft.html](test-microsoft.html), [test-facebook.html](test-facebook.html), [test-line.html](test-line.html) at repo root — one per provider, uses the provider's native JS SDK / LIFF to obtain a real token and POST it to the corresponding `/auth/external/*` endpoint.

### v1.1.0 — 2026-09-16

- **Google OAuth external login**: new `POST /auth/external/google` endpoint accepting a Google `id_token` and returning the service's JWT + refresh token. Token-exchange flow only (no cookie/redirect). Auto-provisions new users with `EmailConfirmed=true`, auto-links Google identities to existing verified local accounts, and rejects link attempts against unverified local accounts (`EMAIL_EXISTS_UNVERIFIED`) to prevent takeover. Package: `Google.Apis.Auth`. Config: `AuthMicroservice:ExternalProviders:Google:{Enabled, ClientId}` — disabled by default; endpoint returns 404 unless enabled. Startup fail-fast if `Enabled=true` without `ClientId`. Full unit + integration test coverage via `IGoogleTokenValidator` seam.

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
- v1.x candidates: ~~2FA (Email OTP)~~ ✅ / TOTP (authenticator apps) / SMS OTP, external OAuth providers (~~Google~~ ✅ / ~~Microsoft~~ ✅ / ~~Facebook~~ ✅ / ~~LINE~~ ✅ / ~~ThaID~~ ✅), rate limiting บน /auth/login + /auth/forgot-password, audit log ของ auth events
- Ops: HealthChecks (DB + SMTP), OpenTelemetry traces, structured logging correlationId

```
ถ้าพร้อมจะเอาไปใช้จริงใน project ใหม่ อย่าลืมเปลี่ยน Jwt__Key และ Database__ConnectionString ผ่าน env var ก่อน deploy — startup จะ fail-fast ถ้าคีย์ต่ำกว่า 32 chars
```
