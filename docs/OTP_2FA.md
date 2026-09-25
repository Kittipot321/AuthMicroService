# OTP / 2FA flow

Feature v1.3 เพิ่ม email-based OTP สำหรับ **email verification** (ทดแทน / เสริม link ใน email) และสำหรับ **login two-factor** (สองขั้นตอนเมื่อ user เปิด 2FA)

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
| `Otp:EmailVerification.Enabled` | `true` | ปิด → handler ตอบ 404 `OTP_DISABLED` (route ยังอยู่) |
| `Otp:LoginTwoFactor.Enabled` | `true` | ปิด → handler ตอบ 404 `OTP_DISABLED` — user ที่มี `TwoFactorEnabled=true` login ไม่ผ่าน 2FA แล้ว ระวังก่อนปิด |

## Configuration knobs

```json
"Otp": {
  "CodeLength": 6,               // 4-10
  "ExpirationMinutes": 10,       // > 0
  "MaxAttempts": 5,              // ก่อน mark consumed + คืน OTP_ATTEMPTS_EXCEEDED
  "ResendCooldownSeconds": 60,   // ก่อนขอ code ใหม่ได้ — คืน 429 OTP_COOLDOWN_ACTIVE
  "EmailVerification": { "Enabled": true },
  "LoginTwoFactor":    { "Enabled": true }
}
```

Full config schema ดูที่ [docs/CONFIGURATION.md](CONFIGURATION.md)

Browser test harness: [`test-html/test-otp.html`](../test-html/test-otp.html) — เปิดใน browser หลัง `dotnet run` เพื่อทดสอบ flow ทั้งหมดโดยไม่ต้องเขียน curl
