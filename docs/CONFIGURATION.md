# Configuration reference

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
      "Google":    { "Enabled": false, "ClientId": "", "ClientSecret": "" },  // ClientSecret required for authorization-code exchange
      "Microsoft": { "Enabled": false, "ClientId": "", "TenantId": "common" },
      "Facebook":  { "Enabled": false, "AppId": "", "AppSecret": "", "GraphApiVersion": "v18.0" },
      "Line":      {
        "Enabled": false,
        "ChannelId": "",                                                    // LINE Login channel ID — token-exchange flow ใช้เป็น aud
        "ChannelSecret": "",                                                // ตั้งค่า = เปิด OIDC redirect flow (challenge/callback endpoints)
        "Authority": "https://access.line.me",                              // LINE authorize base URL
        "TokenEndpoint": "https://api.line.me/oauth2/v2.1/token",
        "VerifyEndpoint": "https://api.line.me/oauth2/v2.1/verify",         // ใช้กับ LIFF token-exchange
        "RedirectUri": "",                                                  // required เมื่อ ChannelSecret ตั้งค่า — ต้องตรงกับที่ลงทะเบียนใน LINE console
        "AllowedReturnUrlPrefixes": [ ],                                    // whitelist ของ frontend returnUrl (open-redirect guard) — required เมื่อ ChannelSecret ตั้งค่า
        "Scopes": "openid profile email",
        "StateLifetimeMinutes": 10
      },
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

## Startup validation (fail-fast)

Startup fails fast if:

- `Jwt.Key` is under 32 chars
- an unknown DB provider is set
- `Email.Enabled=true` without an SMTP host
- an enabled external provider is missing its required credentials:
  - **Google/Microsoft** — need `ClientId` (Microsoft also `TenantId`)
  - **Facebook** — needs `AppId` + `AppSecret`
  - **LINE** — needs `ChannelId` (และถ้า `ChannelSecret` ตั้งค่าเพื่อเปิด OIDC redirect flow ต้องมี `RedirectUri` + `AllowedReturnUrlPrefixes` ≥ 1 entry ด้วย)
  - **ThaID** — needs `ClientId` + `ClientSecret` + `RedirectUri`
- `Identity.Roles` validation:
  - `AdditionalRoles[].Name` ห้ามว่าง / เกิน 256 chars / ชนกับ system role (`Admin`/`User`) / ซ้ำกัน
  - `DefaultRegistrationRole` + ทุก entry ใน `AllowedSelfRegisterRoles` ต้องอ้างถึง role ที่มีอยู่จริง (system หรือ `AdditionalRoles`)

## Related docs

- [OTP_2FA.md](OTP_2FA.md) — OTP / 2FA configuration knobs
- [EXTERNAL_PROVIDERS.md](EXTERNAL_PROVIDERS.md) — per-provider config detail, error mapping, env-var examples
- [PROVIDER_SETUP.md](PROVIDER_SETUP.md) — วิธีขอ credential จาก provider console
- [INSTALLATION.md](INSTALLATION.md) — install + configure end-to-end walkthrough
