# OTP / 2FA flow

Feature v1.3 เพิ่ม email-based OTP สำหรับ **email verification** (ทดแทน / เสริม link ใน email) และสำหรับ **login two-factor** (สองขั้นตอนเมื่อ user เปิด 2FA); v1.4 เพิ่ม **TOTP** (authenticator app / RFC 6238) และ **recovery codes** เผื่อ device หาย — ทั้งสอง method อยู่พร้อมกันได้ user เลือกที่ตอน login

> ภาพรวม API + endpoint table เต็มดูที่ [README.md](../README.md#api-endpoints-mounted-under-routeprefix-default-auth)

## Login 2FA — 2-step flow

เมื่อ user เปิด 2FA แล้ว `POST /auth/login` จะไม่ return tokens ทันที แต่จะตอบ `202 Accepted` + ส่ง OTP ไปยัง email ก่อน จากนั้น client ต้องเรียก `/auth/login/2fa/verify` เพื่อแลก tokens

```
┌──────┐                     ┌────────┐                ┌─────────┐
│Client│                     │  API   │                │  Email  │
└───┬──┘                     └───┬────┘                └────┬────┘
    │  POST /auth/login          │                          │
    │  {email, password}         │                          │
    │───────────────────────────>│                          │
    │                            │  send OTP (6 digits)     │
    │                            │─────────────────────────>│
    │  202 Accepted              │                          │
    │  { email, expiresAt,       │                          │
    │    message }               │                          │
    │<───────────────────────────│                          │
    │                                                       │
    │  (user opens inbox, reads code)                       │
    │                                                       │
    │  POST /auth/login/2fa/verify                          │
    │  {email, code}             │                          │
    │───────────────────────────>│                          │
    │  200 OK                    │                          │
    │  { accessToken,            │                          │
    │    refreshToken, user }    │                          │
    │<───────────────────────────│                          │
```

> **Resend**: ถ้า user ไม่ได้รับ email (หรือ code หมดอายุ) client เรียก `POST /auth/login/2fa/resend {email}` เพื่อขอ code ใหม่ — silent success ทุกกรณี (กัน enumeration) และ respect `ResendCooldownSeconds` — ถ้ายิงถี่เกินได้ 429 `OTP_COOLDOWN_ACTIVE`

**Full example (PowerShell):**

```powershell
# 1. Register (2FA disabled by default)
$reg = @{ email="bob@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$tokens = curl -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d $reg | ConvertFrom-Json

# 2. Enable 2FA (requires bearer token from register/login)
$headers = @{ Authorization = "Bearer $($tokens.accessToken)" }
curl -X POST http://localhost:8080/auth/2fa/enable-request -H @headers
# → grab OTP from Mailhog (http://localhost:8025)
$confirm = @{ code="123456" } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/2fa/enable-confirm -H @headers -H "Content-Type: application/json" -d $confirm

# 3. Login now returns 202 + sends OTP
$login = @{ email="bob@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$pending = curl -X POST http://localhost:8080/auth/login -H "Content-Type: application/json" -d $login
# → status 202 { email, expiresAt, message: "Two-factor verification required." }

# 4. Verify OTP → get tokens
$verify = @{ email="bob@example.com"; code="654321" } | ConvertTo-Json
$auth = curl -X POST http://localhost:8080/auth/login/2fa/verify -H "Content-Type: application/json" -d $verify | ConvertFrom-Json
```

## TOTP (Google Authenticator / Authy / MS Authenticator)

TOTP เป็น 2FA แบบไม่พึ่ง email — user scan QR ในแอป authenticator ครั้งเดียว จากนั้นแอปสร้าง 6-หลักใหม่ทุก 30 วินาที secret ถูก encrypt ด้วย ASP.NET **Data Protection** ก่อนเก็บใน DB → DB dump อย่างเดียวเดา code ไม่ได้

### Enroll flow

```
1. POST /auth/2fa/totp/setup   (Bearer)
   → 200 { otpauthUri, qrCodePngBase64, secretBase32, issuer, accountName, digits, periodSeconds }
     - แสดง QR (base64 PNG) หรือ deep-link `otpauthUri` ให้ user scan
     - `secretBase32` = fallback ให้ copy-paste เมื่อ QR อ่านไม่ได้
     - secret **pending** เก็บใน DB แล้ว แต่ยัง TotpEnabled=false
2. user scan → app แสดง 6-หลัก → POST /auth/2fa/totp/enable-confirm { code }
   → 200 { recoveryCodes: [...10 codes] }  ← เก็บให้ user save ทันที (ครั้งเดียวเท่านั้น)
     - set TotpEnabled=true + TwoFactorEnabled=true + regen SecurityStamp
3. Disable: POST /auth/2fa/totp/disable { password } → 204
     - clear secret, TotpEnabled=false, recompute TwoFactorEnabled จาก EmailTwoFactorEnabled
```

### Login flow เมื่อเปิดหลาย method

```
POST /auth/login { email, password }
  ↓
  ผู้ใช้มี TotpEnabled=true  → 202 { methods: ["totp", "email"?], emailChallengeSent: false, ... }
  ผู้ใช้มีแค่ EmailTwoFactorEnabled → 202 { methods: ["email"], emailChallengeSent: true, ... }  (ส่ง OTP ไปที่ email ทันที)
  ผู้ใช้ไม่มี 2FA           → 200 + tokens (เหมือนเดิม)
  ↓
เลือก method verify:
  TOTP:            POST /auth/login/2fa/totp/verify     { email, code }
  Email OTP:       POST /auth/login/2fa/verify          { email, code }  (call /auth/login/2fa/resend ก่อนถ้ายังไม่มี email)
  Recovery code:   POST /auth/login/2fa/recovery/verify { email, code }
  → 200 { accessToken, refreshToken, user }
```

### Recovery codes

Enable TOTP ครั้งแรก → backend generate 10 codes ให้ user save ไว้ ใช้ครั้งเดียวต่อ code เมื่อทำ authenticator device หาย

```powershell
# regenerate ใหม่ (invalidate ชุดเดิม)
$body = @{ password="P@ssw0rd!" } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/2fa/recovery-codes/generate `
  -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d $body
# → 200 { codes: [...10 fresh codes], generatedAt }
```

### Data Protection

TOTP secret ใน DB ถูก encrypt ผ่าน `IDataProtector` (`purpose = "AuthMicroservice.TotpSecret"`) — ต้อง persist keyring ให้อยู่นานพอที่ decrypt secret ที่เก็บก่อนหน้าได้:

```jsonc
"DataProtection": {
  "ApplicationName": "AuthMicroservice",  // ต้องตรงกันข้าม instances ที่ scale horizontally
  "KeyRingPath": "/var/auth-keys"         // mount เป็น Docker volume; ถ้าเป็น empty → key เก็บที่ default path (dev/tests OK)
}
```

⚠️ Docker: ต้อง mount `/var/auth-keys` เป็น persistent volume ไม่งั้น restart container = decrypt secret เก่าไม่ได้ = user login ผ่าน TOTP ไม่ได้

### PowerShell end-to-end example

```powershell
# 1. Register + login → เก็บ access token
$reg = @{ email="totp@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$tokens = curl -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d $reg | ConvertFrom-Json
$headers = @{ Authorization = "Bearer $($tokens.accessToken)"; "Content-Type" = "application/json" }

# 2. Setup TOTP
$setup = curl -X POST http://localhost:8080/auth/2fa/totp/setup -H @headers | ConvertFrom-Json
# → $setup.otpauthUri = "otpauth://totp/..."
# → $setup.qrCodePngBase64 = "iVBORw0KGgo..."
# → $setup.secretBase32 = "ABCD...XYZ"

# 3. Scan QR ด้วย Google Authenticator (หรือ Authy/MS Authenticator/1Password)
#    หรือ paste secretBase32 ใน 1Password → ได้ code 6 หลัก

# 4. ยืนยัน + รับ recovery codes
$confirm = @{ code = "123456" } | ConvertTo-Json
$enable = curl -X POST http://localhost:8080/auth/2fa/totp/enable-confirm -H @headers -d $confirm | ConvertFrom-Json
# → $enable.recoveryCodes = ["ABCDE-FGHIJ", ...]  เก็บให้ user

# 5. Login ครั้งใหม่ (logout ก่อน)
$login = @{ email="totp@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$pending = curl -i -X POST http://localhost:8080/auth/login -H "Content-Type: application/json" -d $login
# → 202 { methods: ["totp"], emailChallengeSent: false }

# 6. TOTP verify
$verify = @{ email="totp@example.com"; code="654321" } | ConvertTo-Json
$auth = curl -X POST http://localhost:8080/auth/login/2fa/totp/verify -H "Content-Type: application/json" -d $verify | ConvertFrom-Json
```

## Email verification via OTP (alternative to link)

`/auth/otp/email/send` + `/auth/otp/email/verify` เป็นทางเลือกของ `/auth/verify-email` (ลิงก์) เหมาะสำหรับ mobile apps / SPA ที่ไม่อยากจัดการ deep link

```powershell
# 1. After register (unverified user)
$send = @{ email="carol@example.com" } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/otp/email/send -H "Content-Type: application/json" -d $send
# → 200 OK silent success ทุกกรณี (ป้องกัน enumeration) แต่จะส่ง email เฉพาะกรณีที่ user มีจริงและยังไม่ verified

# 2. User กรอก code จาก email
$verify = @{ email="carol@example.com"; code="123456" } | ConvertTo-Json
curl -X POST http://localhost:8080/auth/otp/email/verify -H "Content-Type: application/json" -d $verify
# → 200 OK { "message": "Email verified." }
# → 401 INVALID_OTP / OTP_EXPIRED / OTP_ATTEMPTS_EXCEEDED
# → 429 OTP_COOLDOWN_ACTIVE (ถ้ายิง /send ถี่เกินไป)
# → 409 EMAIL_ALREADY_VERIFIED (verify แล้ว — client ควรข้ามขั้นตอนนี้)
```

## Endpoint toggles

ทุก OTP/2FA endpoint สามารถเปิด/ปิด ได้อิสระผ่าน `AuthMicroservice:Endpoints` (คืน 404 ตอน routing ถ้าปิด) หรือผ่าน `AuthMicroservice:Otp` (คืน 404 `OTP_DISABLED` ที่ handler ถ้าปิด purpose นั้นๆ ทั้ง feature)

| Setting | Default | ผล |
|---|---|---|
| `Endpoints:SendEmailVerificationOtp.Enabled` | `true` | ปิด → route `/auth/otp/email/send` ไม่ถูก map (404) |
| `Endpoints:VerifyEmailOtp.Enabled` | `true` | ปิด → route `/auth/otp/email/verify` ไม่ถูก map |
| `Endpoints:LoginTwoFactorVerify.Enabled` | `true` | ปิด → route `/auth/login/2fa/verify` ไม่ถูก map |
| `Endpoints:LoginTwoFactorResend.Enabled` | `true` | ปิด → route `/auth/login/2fa/resend` ไม่ถูก map |
| `Endpoints:ExternalLineChallenge.{Enabled,ShowInSwagger}` | `true` / `true` | ปิด `Enabled` → route `/auth/external/challenge/line` ไม่ถูก map. ปิด `ShowInSwagger` → route ยังทำงาน แต่ถูก `ExcludeFromDescription` (แนะนำสำหรับ browser-redirect endpoint) |
| `Endpoints:ExternalLineCallback.{Enabled,ShowInSwagger}` | `true` / `true` | เหมือน `ExternalLineChallenge` แต่สำหรับ `/auth/external/callback/line` |
| `Endpoints:TwoFactorEnableRequest.Enabled` | `true` | ปิด → route `/auth/2fa/enable-request` ไม่ถูก map |
| `Endpoints:TwoFactorEnableConfirm.Enabled` | `true` | ปิด → route `/auth/2fa/enable-confirm` ไม่ถูก map |
| `Endpoints:TwoFactorDisable.Enabled` | `true` | ปิด → route `/auth/2fa/disable` ไม่ถูก map |
| `Endpoints:TotpSetup.Enabled` | `true` | ปิด → route `/auth/2fa/totp/setup` ไม่ถูก map |
| `Endpoints:TotpEnableConfirm.Enabled` | `true` | ปิด → route `/auth/2fa/totp/enable-confirm` ไม่ถูก map |
| `Endpoints:TotpDisable.Enabled` | `true` | ปิด → route `/auth/2fa/totp/disable` ไม่ถูก map |
| `Endpoints:LoginTotpVerify.Enabled` | `true` | ปิด → route `/auth/login/2fa/totp/verify` ไม่ถูก map |
| `Endpoints:LoginRecoveryCodeVerify.Enabled` | `true` | ปิด → route `/auth/login/2fa/recovery/verify` ไม่ถูก map |
| `Endpoints:GenerateRecoveryCodes.Enabled` | `true` | ปิด → route `/auth/2fa/recovery-codes/generate` ไม่ถูก map |
| `EmailVerification:Mode` (`Link` \| `Code` \| `Disabled`) | `Link` | Code = เปิด OTP endpoints; Link/Disabled = OTP endpoints ตอบ 404 `OTP_DISABLED` (route ยังอยู่) |
| `Otp:LoginTwoFactor.Enabled` | `true` | ปิด → handler ตอบ 404 `OTP_DISABLED` — user ที่มี `TwoFactorEnabled=true` login ไม่ผ่าน 2FA แล้ว ระวังก่อนปิด |
| `Totp.Enabled` | `true` | ปิด → handler ตอบ 404 `TOTP_DISABLED` (route ยังอยู่); user ที่มี TotpEnabled=true จะ verify ผ่าน TOTP ไม่ได้ |
| `RecoveryCodes.Enabled` | `true` | ปิด → handler ตอบ 404 `RECOVERY_CODES_DISABLED`; enroll TOTP จะไม่ auto-generate codes |

## Configuration knobs

```json
"Otp": {
  "CodeLength": 6,               // 4-10
  "ExpirationMinutes": 10,       // > 0
  "MaxAttempts": 5,              // ก่อน mark consumed + คืน OTP_ATTEMPTS_EXCEEDED
  "ResendCooldownSeconds": 60,   // ก่อนขอ code ใหม่ได้ — คืน 429 OTP_COOLDOWN_ACTIVE
  "LoginTwoFactor":    { "Enabled": true }
},
"EmailVerification": {
  "Mode": "Link",                // Link | Code | Disabled — Code เท่านั้นที่เปิด /auth/otp/email/*; Link/Code = บล็อค login ก่อน confirm; Disabled = ไม่ส่ง + ไม่บล็อค
  "LinkBaseUrl": "https://app.example.com/verify-email"  // required เฉพาะ Mode=Link
},
"Totp": {
  "Enabled": true,
  "Issuer": "AuthMicroservice",     // label ใน authenticator app
  "Digits": 6,                      // Google Authenticator = 6
  "PeriodSeconds": 30,              // step
  "VerificationWindowSteps": 1      // ±1 = ยอมรับ ±30s clock skew
},
"RecoveryCodes": {
  "Enabled": true,
  "Count": 10,                      // codes ต่อชุด
  "Length": 10                      // chars ก่อน dash separator (`XXXXX-XXXXX`)
},
"DataProtection": {
  "ApplicationName": "AuthMicroservice",
  "KeyRingPath": "/var/auth-keys"   // persist path สำหรับ TOTP secret encryption keyring
}
```

Full config schema ดูที่ [docs/CONFIGURATION.md](CONFIGURATION.md)

Browser test harness: [`test-html/test-otp.html`](../test-html/test-otp.html) — เปิดใน browser หลัง `dotnet run` เพื่อทดสอบ flow ทั้งหมดโดยไม่ต้องเขียน curl
