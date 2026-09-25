# Changelog

All notable changes to **AuthMicroservice** are documented in this file.

## v1.3.2 — 2026-09-25

- **Google login: token-exchange → authorization-code flow** (⚠️ breaking): `POST /auth/external/google` เปลี่ยน request body จาก `{ "idToken": "..." }` เป็น `{ "code": "..." }` — frontend ต้อง migrate จาก GSI credential response ไปใช้ `google.accounts.oauth2.initCodeClient({ ux_mode: 'popup', ... })` (popup flow) ที่คืน authorization code. Backend แลก code กับ `https://oauth2.googleapis.com/token` ด้วย `redirect_uri=postmessage` เอา `id_token` มา validate ผ่าน Google JWKS เอง — `ClientSecret` ไม่หลุดไปฝั่ง frontend.
- **Config**: เพิ่ม `AuthMicroservice:ExternalProviders:Google:ClientSecret` (required เมื่อ `Enabled=true`) — startup validate เฉพาะ `ClientId`; ถ้าลืม `ClientSecret` จะ **fail ตอน login ครั้งแรก** ด้วย 500 (`GoogleOAuthException: Google authorization-code configuration is incomplete`).
- **Service seam**: `IGoogleOAuthClient` + `GoogleOAuthClient` (ใหม่) รับหน้าที่ code exchange + delegate JWKS validation ต่อให้ `IGoogleTokenValidator` เดิม. Integration tests เปลี่ยน seam จาก `FakeGoogleTokenValidator` → `FakeGoogleOAuthClient` (register code → user info mapping).
- **Error mapping**: `INVALID_GOOGLE_TOKEN` (401) ครอบทั้ง code-exchange failure (network/HTTP non-2xx จาก Google token endpoint) และ id_token validation failure หลัง exchange — ไม่ต้องเพิ่ม code ใหม่. `GOOGLE_EMAIL_NOT_VERIFIED` / `GOOGLE_LOGIN_DISABLED` ยังเหมือนเดิม.
- **Provider setup docs**: [docs/PROVIDER_SETUP.md](docs/PROVIDER_SETUP.md) อัปเดตขั้นตอนหยิบ **Client secret** จาก Google Cloud Console + อธิบาย dev popup flow (`redirect_uri=postmessage` hardcoded — ไม่ต้องตั้ง redirect URI ใน console) vs production full-redirect flow.

## v1.3.1 — 2026-09-25

- **LINE OIDC redirect flow**: LINE รองรับ 2 flows แล้ว — LIFF token-exchange เดิม (`POST /auth/external/line`) และ OIDC redirect flow (`GET /auth/external/challenge/line` + `GET /auth/external/callback/line`) pattern เดียวกับ ThaID. เปิดโดยตั้ง `AuthMicroservice:ExternalProviders:Line:ChannelSecret` — validator บังคับ `RedirectUri` + `AllowedReturnUrlPrefixes` ≥ 1 entry เมื่อตั้ง ChannelSecret (open-redirect guard). Config ใหม่: `ChannelSecret`, `Authority` (default `https://access.line.me`), `TokenEndpoint`, `RedirectUri`, `AllowedReturnUrlPrefixes`, `Scopes` (default `openid profile email`), `StateLifetimeMinutes` (default 10). Nonce validated จาก `id_token` เทียบกับที่เก็บใน state store. Error codes ใหม่: `INVALID_LINE_STATE` (400), `LINE_RETURN_URL_NOT_ALLOWED` (400). Endpoint toggles ใหม่: `Endpoints:ExternalLineChallenge`, `Endpoints:ExternalLineCallback` (auto-hide จาก Swagger คล้าย ThaID).
- **Login 2FA resend endpoint**: เพิ่ม `POST /auth/login/2fa/resend {email}` — ให้ client ขอ OTP ใหม่ได้หลัง `/auth/login` ตอบ 202 โดยไม่ต้องเริ่ม login ซ้ำ. Silent success ทุกกรณี (กัน account enumeration); คืน 429 `OTP_COOLDOWN_ACTIVE` ถ้ายิงถี่เกิน `ResendCooldownSeconds`. Toggle: `Endpoints:LoginTwoFactorResend`.
- **Email verify hardening**: `POST /auth/otp/email/verify` เดิม silent success กรณี user verified แล้ว (return 200 OK) — เปลี่ยนเป็นคืน 409 `EMAIL_ALREADY_VERIFIED` เพื่อบอก client ว่าไม่ต้องเรียกซ้ำ. Non-breaking สำหรับ flow ปกติ (user ยังไม่ verify).

## v1.3.0 — 2026-09-23

- **Email OTP (6-digit)** สำหรับ 2 flows — ทางเลือกคู่ขนานกับ link-based flow เดิม (toggle ได้):
  - **Email verification via OTP** — `POST /auth/otp/email/send` + `POST /auth/otp/email/verify` (แทน/เสริม verify-email link)
  - **Login 2FA** — user ที่ `TwoFactorEnabled=true` เมื่อ login สำเร็จด้วย password จะได้ 202 (`TWO_FACTOR_REQUIRED`) + OTP ส่งไปที่ email → ต้อง `POST /auth/login/2fa/verify {email, code}` เพื่อรับ JWT. Enable/disable ผ่าน `POST /auth/2fa/enable-request` → `POST /auth/2fa/enable-confirm` (auth required) และ `POST /auth/2fa/disable` (require password confirm)
  - Password reset ยังใช้ token-based link flow เดิม (`POST /auth/forgot-password` + `POST /auth/reset-password`) — ไม่มี OTP variant
- **Storage & security**: OTP เก็บใน `auth.OtpCodes` (ไม่เก็บ plaintext) — SHA-256(code + per-code random salt), constant-time compare, per-user+purpose invalidation ก่อน generate ใหม่, per-code MaxAttempts + resend cooldown, silent-success สำหรับ send endpoints (กัน account enumeration). Migration `AddOtpCodes` ครบ 3 provider (SqlServer / Postgres / Sqlite).
- **Config**: ใหม่ `AuthMicroservice:Otp:{CodeLength, ExpirationMinutes, MaxAttempts, ResendCooldownSeconds, {EmailVerification, LoginTwoFactor}.Enabled}` — startup validate `CodeLength ∈ [4,10]`, `ExpirationMinutes > 0`, ฯลฯ. Endpoint toggles ใหม่ใน `AuthMicroservice:Endpoints:*` — ทุก endpoint เปิด/ปิด/hide-from-swagger ได้แยกกันเหมือน pattern เดิม.
- **Error codes ใหม่**: `TWO_FACTOR_REQUIRED` (202), `INVALID_OTP` / `OTP_EXPIRED` / `OTP_ATTEMPTS_EXCEEDED` (401), `OTP_COOLDOWN_ACTIVE` (429), `OTP_DISABLED` (404), `TWOFA_NOT_ENABLED` / `TWOFA_ALREADY_ENABLED` (409).
- **Test harness**: [test-html/test-otp.html](test-html/test-otp.html) — Bootstrap 5 single-page console ครอบ 2 flows พร้อม log JSON response
- **Extending `IEmailService`**: เพิ่ม `SendOtpAsync(user, code, purpose, expiresInMinutes, ct)` + 2 embedded HTML templates (`otp-email-verification.html`, `otp-login-2fa.html`) — replace ได้เหมือน `IEmailSender` เดิม

## v1.2.1 — 2026-09-22

- **Custom roles + assignable role at registration**: new config `AuthMicroservice:Identity:Roles:{DefaultRegistrationRole, AllowedSelfRegisterRoles, AdditionalRoles}` — seed extra roles (Moderator, ContentCreator, ฯลฯ) ผ่าน config โดยไม่ต้องแตะ DB เอง. `POST /auth/register` รับ optional `role` field — ถ้าอยู่ใน `AllowedSelfRegisterRoles` (หรือตรงกับ `DefaultRegistrationRole`) จะ assign role นั้นให้ user ใหม่, ไม่งั้น 400 `INVALID_ROLE`. Role validation happens **ก่อน** create user (fail-fast — no orphan users). External login flows (Google / Microsoft / Facebook / LINE / ThaID) ยังคง assign เฉพาะ `AuthRoles.User` — ไม่รับ `role` parameter.
- **`ApplicationRole` metadata**: เพิ่ม 3 columns — `Description` (nvarchar(256), nullable), `IsSystem` (bit, `true` สำหรับ Admin/User และ `false` สำหรับ custom roles), `CreatedAtUtc` (datetime2). Migration `AddRoleMetadata` generate ครบทั้ง 3 provider (SqlServer / Postgres / Sqlite) — apply อัตโนมัติถ้า `AutoMigrate=true`.
- **Startup fail-fast validation for roles**: `AdditionalRoles[].Name` required + ≤256 chars + ห้ามชนกับ system role + ห้าม duplicate; `AdditionalRoles[].Description` ≤256 chars; `DefaultRegistrationRole` required + ต้องอ้างถึง role ที่มีอยู่จริง; `AllowedSelfRegisterRoles[]` required + ต้องอ้างถึง role ที่มีอยู่จริง + ห้าม duplicate. Error code ใหม่: `INVALID_ROLE` (400).
- **Backward compatible**: `role` field เป็น optional (payload เดิม fallback = `DefaultRegistrationRole = "User"`). Default `AllowedSelfRegisterRoles = []` และ `AdditionalRoles = []` — ถ้าไม่ตั้ง config อะไรเลย พฤติกรรมเหมือนก่อนหน้าทุกอย่าง.

## v1.2.0 — 2026-09-22

- **ThaID external login (Thai national digital ID / DOPA)**: new redirect-based OIDC flow — `GET /auth/external/thaid/challenge?returnUrl=...` เริ่ม flow (สร้าง state + PKCE, redirect ไป ThaID authorize) และ `GET /auth/external/thaid/callback?code=&state=` แลก tokens + ออก JWT/refresh + redirect กลับ `returnUrl`. Config `AuthMicroservice:ExternalProviders:ThaId:{Enabled, ClientId, ClientSecret, Authority, RedirectUri, AllowedReturnUrlPrefixes, Scopes, StateLifetimeMinutes}` — sandbox authority `https://imauthtestc.bora.dopa.go.th/api/v2/oauth2`, prod `https://imauth.bora.dopa.go.th/api/v2/oauth2`. State + PKCE verified via `IThaIdStateStore` (in-memory default). Email เป็น optional scope — user ที่ไม่มี email ถูก auto-provision เป็น `{pid}@thaid.local` + `EmailConfirmed=false`. `AllowedReturnUrlPrefixes` เป็น open-redirect guard. Errors: `INVALID_THAID_STATE` (400), `INVALID_THAID_CODE` (401), `THAID_RETURN_URL_NOT_ALLOWED` (400), `THAID_LOGIN_DISABLED` (404).
- **NuGet package rebrand**: package IDs เปลี่ยน `Kittipot.AuthMicroservice.*` → `Synergy.AuthMicroservice.*` (Core + Migrations.SqlServer/Postgres/Sqlite/InMemory) เพื่อสะท้อน ownership ของ Synergy Software — assembly names และ `using AuthMicroservice.Core.*` namespaces คงเดิม (source-compatible, แต่ผู้ใช้ที่ install จาก NuGet ต้อง `dotnet remove package Kittipot.AuthMicroservice.*` แล้ว `dotnet add package Synergy.AuthMicroservice.*`).
- **Test harnesses reorganized + populated**: ย้าย `test-*.html` จาก repo root → [`test-html/`](test-html/) folder และเติมค่า client identifier ตัวอย่างจริงในแต่ละไฟล์ (Facebook AppId, Google Client ID, Microsoft Client ID, LINE LIFF ID) ให้กดปุ่มแล้วทดสอบได้ทันที + เพิ่ม [test-html/test-thaid.html](test-html/test-thaid.html).
- **Local HTTPS testing docs**: เพิ่มขั้นตอน `dotnet-serve -S` + `dotnet dev-certs https --trust` ใน External login providers section — จำเป็นสำหรับ provider SDK ที่บังคับ HTTPS (LIFF, Google Identity, MSAL, Facebook Login).

## v1.1.1 — 2026-09-21

Adds three more external login providers on top of Google, sharing the same token-exchange flow (auto-link when local email is verified, reject `EMAIL_EXISTS_UNVERIFIED` otherwise, auto-provision new users with `EmailConfirmed=true` + `User` role) and the same fail-fast startup checks.

- **Microsoft external login**: new `POST /auth/external/microsoft` accepting a Microsoft `id_token` (Azure AD or personal MSA). Config `AuthMicroservice:ExternalProviders:Microsoft:{Enabled, ClientId, TenantId}` — `TenantId` accepts `common` / `organizations` / `consumers` / a specific tenant GUID. Validation via OpenID Connect metadata (package: `Microsoft.IdentityModel.Protocols.OpenIdConnect`). Microsoft-issued email is treated as verified by default. Errors: `INVALID_MICROSOFT_TOKEN` (401), `MICROSOFT_LOGIN_DISABLED` (404).
- **Facebook external login**: new `POST /auth/external/facebook` accepting a Facebook `access_token`. Config `AuthMicroservice:ExternalProviders:Facebook:{Enabled, AppId, AppSecret, GraphApiVersion}` (default `v18.0`). Validation via Graph `debug_token` then `GET /me?fields=id,email,name,picture` via `IHttpClientFactory`. Errors: `INVALID_FACEBOOK_TOKEN` (401), `FACEBOOK_EMAIL_REQUIRED` (400 — user did not grant the email scope), `FACEBOOK_LOGIN_DISABLED` (404).
- **LINE external login**: new `POST /auth/external/line` accepting a LINE `id_token` (typically from LIFF `liff.getIDToken()`). Config `AuthMicroservice:ExternalProviders:Line:{Enabled, ChannelId, VerifyEndpoint}` (default endpoint `https://api.line.me/oauth2/v2.1/verify`). `ChannelId` is used as both the verify-endpoint client_id and the expected `aud`; `iss` must equal `https://access.line.me`. Email is optional — when LINE does not return email, users are auto-provisioned with a placeholder `{subject}@line.local` and `EmailConfirmed=false`. Errors: `INVALID_LINE_TOKEN` (401), `LINE_LOGIN_DISABLED` (404).
- **Browser test harnesses**: added [test-google.html](test-google.html), [test-microsoft.html](test-microsoft.html), [test-facebook.html](test-facebook.html), [test-line.html](test-line.html) at repo root — one per provider, uses the provider's native JS SDK / LIFF to obtain a real token and POST it to the corresponding `/auth/external/*` endpoint.

## v1.1.0 — 2026-09-16

- **Google OAuth external login**: new `POST /auth/external/google` endpoint accepting a Google `id_token` and returning the service's JWT + refresh token. Token-exchange flow only (no cookie/redirect). Auto-provisions new users with `EmailConfirmed=true`, auto-links Google identities to existing verified local accounts, and rejects link attempts against unverified local accounts (`EMAIL_EXISTS_UNVERIFIED`) to prevent takeover. Package: `Google.Apis.Auth`. Config: `AuthMicroservice:ExternalProviders:Google:{Enabled, ClientId}` — disabled by default; endpoint returns 404 unless enabled. Startup fail-fast if `Enabled=true` without `ClientId`. Full unit + integration test coverage via `IGoogleTokenValidator` seam.

## v1.0.2 — 2026-09-16

- **InMemory adapter packable**: เพิ่ม `AuthMicroservice.Migrations.InMemory` เป็น NuGet package ตัวที่ 5 (Core + Migrations.{SqlServer, Postgres, Sqlite, InMemory}) — ใช้ `.UseInMemory()` extension สำหรับ tests/demos (ไม่แนะนำสำหรับ production เพราะ data หายทุก restart)
- **`DatabaseProvider.InMemory` enum**: `AuthMicroservice:Database:Provider="InMemory"` ใช้งานได้แล้วใน config-driven dispatch ที่ `AuthMicroservice.Api/Program.cs` — connection string ถ้าใส่จะกลายเป็น database name, ถ้าเว้นว่างจะ fallback เป็น `"AuthMicroserviceInMemory"`
- **Sample switched to InMemory**: `AuthMicroservice.Sample` ใช้ `.UseInMemory()` แทน `.UseSqlite()` — รัน `dotnet run --project src/AuthMicroservice.Sample` ได้เลยโดยไม่ต้องสร้างไฟล์ `sample.db`

## v1.0.1 — 2026-09-14

- **Package metadata**: เพิ่ม `Version`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl` ใน `Directory.Build.props` — พร้อม `dotnet pack` เป็น NuGet ทั้ง 4 packages (Core + Migrations.SqlServer/Postgres/Sqlite)
- **Migrations regenerated**: Initial migration ของทั้ง 3 provider ถูก regenerate ใหม่ให้ตรง schema ปัจจุบัน (SqlServer / Postgres / Sqlite) ผ่าน env-var override workflow (`AuthMicroservice__Database__Provider=...`)
- **NuGet packaging ready**: 4 packable projects พร้อม pack — `AuthMicroservice.Core` + `AuthMicroservice.Migrations.{SqlServer,Postgres,Sqlite}` (build ด้วย `dotnet pack -c Release -o ./artifacts`)

## v1.0.0 — initial release

- Register/Login/Logout, JWT + refresh token rotation, email verification + password reset
- Provider-agnostic EF Core (SqlServer / Postgres / Sqlite / InMemory)
- Standalone API + library-mode consumer + Docker Compose + Sample + Unit/Integration tests
