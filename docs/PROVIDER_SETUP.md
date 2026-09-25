# Provider Setup Guide

ไฟล์นี้บอก **วิธีขอ credentials จาก Developer Console ของแต่ละ provider** — สำหรับ config schema, endpoint, และ error codes ดูที่ [EXTERNAL_PROVIDERS.md](EXTERNAL_PROVIDERS.md).

Config ทุก provider อยู่ใต้ `AuthMicroservice:ExternalProviders:<Provider>` ใน [src/AuthMicroservice.Api/appsettings.json](../src/AuthMicroservice.Api/appsettings.json) และ override ผ่าน env var ได้ (double-underscore syntax เช่น `AuthMicroservice__ExternalProviders__Google__ClientId`).

## Quick reference

| Provider | Developer Console | Config section | Required keys |
|---|---|---|---|
| Google | [console.cloud.google.com](https://console.cloud.google.com) | `Google` | `ClientId`, `ClientSecret` |
| Microsoft | [portal.azure.com](https://portal.azure.com) → Entra ID | `Microsoft` | `ClientId`, `TenantId` |
| Facebook | [developers.facebook.com/apps](https://developers.facebook.com/apps) | `Facebook` | `AppId`, `AppSecret` |
| LINE | [developers.line.biz/console](https://developers.line.biz/console) | `Line` | `ChannelId` (+ `ChannelSecret`, `RedirectUri`, `AllowedReturnUrlPrefixes` ถ้าใช้ OIDC redirect flow) |
| ThaID | ยื่นเรื่องกับ DOPA (ไม่ใช่ self-service) | `ThaId` | `ClientId`, `ClientSecret`, `RedirectUri` |

---

## 1. Google

- **Developer console**: <https://console.cloud.google.com>
- **Prerequisites**: Google account (ฟรี) — ไม่ต้องผูกบัตรเครดิต
- **Docs**: <https://developers.google.com/identity/gsi/web/guides/overview>

### Steps

1. เข้า Google Cloud Console → **Select a project** → **New Project** → ตั้งชื่อ project
2. เมนู **APIs & Services** → **OAuth consent screen**
   - เลือก **External** (สำหรับผู้ใช้ทั่วไป) หรือ **Internal** (เฉพาะ Google Workspace องค์กร)
   - ใส่ App name, support email, developer contact — บันทึก
3. **APIs & Services** → **Credentials** → **+ Create Credentials** → **OAuth client ID**
4. Application type = **Web application**
5. เพิ่ม **Authorized JavaScript origins** (เช่น `https://localhost:5001`) — dev popup flow ใช้ `redirect_uri=postmessage` (hardcoded ที่ backend) ไม่ต้องตั้ง redirect URI ก็ได้; **production** ที่ใช้ full redirect flow ต้องเพิ่ม **Authorized redirect URIs** ให้ตรงกับ URL ที่ backend รับ callback
6. กด **Create** → copy ทั้ง **Client ID** (`xxxxxxx.apps.googleusercontent.com`) และ **Client secret** (คลิก **Show** หรือ download JSON) — เก็บทั้งคู่ไว้ใช้ที่ backend

### Where to paste

```jsonc
"AuthMicroservice": {
  "ExternalProviders": {
    "Google": {
      "Enabled": true,
      "ClientId": "xxxxxxx.apps.googleusercontent.com",
      "ClientSecret": "<client-secret-from-google-console>"
    }
  }
}
```

> **Note**:
> - v1.3 ใช้ **authorization-code exchange**: frontend ใช้ Google Identity Services **OAuth 2.0 Code Client** (`google.accounts.oauth2.initCodeClient({ ux_mode: 'popup', ... })`) ได้ `authorization_code` แล้ว POST `{ "code": "..." }` เข้า `POST /auth/external/google` — backend แลก code + `ClientSecret` กับ Google เพื่อเอา `id_token` มา validate เอง (ClientSecret **ไม่หลุดไปฝั่ง frontend**)
> - Dev popup flow ใช้ `redirect_uri=postmessage` (hardcoded — ไม่ต้องตั้งใน config); production ที่ใช้ full redirect flow ต้องเพิ่ม redirect URI ใน Google Cloud Console
> - Startup validation ตอนนี้เช็คแค่ `ClientId` เท่านั้น — ถ้าลืม `ClientSecret` app จะ start ผ่านแต่ **fail ตอน user login ครั้งแรก** ด้วย 500 (`GoogleOAuthException: Google authorization-code configuration is incomplete`)

---

## 2. Microsoft (Azure AD / Personal MSA)

- **Developer console**: <https://portal.azure.com> → **Microsoft Entra ID** → **App registrations**
- **Prerequisites**: Azure account (Free tier พอ; ไม่ต้องเปิด paid subscription)
- **Docs**: <https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app>

### Steps

1. Azure Portal → search **Microsoft Entra ID** → **App registrations** → **+ New registration**
2. ใส่ **Name** และเลือก **Supported account types**:
   - *Single tenant* → เฉพาะ tenant คุณเอง (ใช้ tenant GUID)
   - *Multi-tenant* → Azure AD ทุก tenant (`organizations`)
   - *Multi-tenant + personal Microsoft accounts* → รวม MSA (`common`)
   - *Personal only* → `consumers`
3. **Redirect URI** — เลือก **Single-page application (SPA)** และใส่ URL ของ frontend (เช่น `https://localhost:5001`)
4. กด **Register**
5. หน้า **Overview** → copy:
   - **Application (client) ID** (GUID) → `ClientId`
   - **Directory (tenant) ID** (GUID) → `TenantId` (ถ้าเลือก single-tenant)

### Where to paste

```jsonc
"Microsoft": {
  "Enabled": true,
  "ClientId": "12931084-af20-4411-a309-223df631ab48",
  "TenantId": "common"
}
```

> **Note**: MSAL ฝั่ง frontend ต้องตั้ง `authority` ให้ตรง — `https://login.microsoftonline.com/{TenantId}`

---

## 3. Facebook

- **Developer console**: <https://developers.facebook.com/apps>
- **Prerequisites**:
  - Facebook account
  - **ยืนยันตัวตน** ผ่านเบอร์โทรหรือบัตรเครดิต (Facebook บังคับก่อนสร้าง App ได้)
- **Docs**: <https://developers.facebook.com/docs/facebook-login/web>

### Steps

1. <https://developers.facebook.com/apps> → **My Apps** → **Create app**
2. เลือก use case (Consumer / Business) → **Next**
3. **Add products to your app** → **Facebook Login** → **Set up** → **Web**
4. เมนู **App settings** → **Basic** → copy:
   - **App ID** → `AppId`
   - **App secret** (คลิก **Show**, ใส่รหัส Facebook อีกครั้ง) → `AppSecret`
5. **Facebook Login** → **Settings** → เพิ่ม **Valid OAuth Redirect URIs** (เช่น `https://localhost:5001/`)
6. (Optional สำหรับ production) **App Review** → **Permissions and Features** → request `email` scope
   - Development mode (default) — ทดสอบได้ทันทีเฉพาะ role Admin/Developer/Tester ที่เพิ่มไว้ในแอป

### Where to paste

```jsonc
"Facebook": {
  "Enabled": true,
  "AppId": "<app-id>",
  "AppSecret": "<app-secret>",
  "GraphApiVersion": "v18.0"
}
```

> **Note**:
> - `GraphApiVersion` ปรับได้ตาม version ที่ Facebook แนะนำ (ปกติไม่ต้องแตะ)
> - ก่อน launch ต้อง submit **App Review** เพื่อขอ `email` permission ให้ผู้ใช้ทั่วไป — ไม่งั้น backend จะได้ error `FACEBOOK_EMAIL_REQUIRED`

---

## 4. LINE

- **Developer console**: <https://developers.line.biz/console>
- **Prerequisites**: LINE account
- **Docs**: <https://developers.line.biz/en/docs/line-login/> · LIFF: <https://developers.line.biz/en/docs/liff/>

LINE รองรับ **2 flows** — เลือกใช้ตาม client:
- **Mode A — LIFF / token-exchange** (default): เหมาะกับ LIFF app ใน LINE. Frontend เอา `id_token` มา POST ที่ backend — ใช้แค่ `ChannelId`
- **Mode B — OIDC redirect (challenge/callback)**: เหมาะกับ web SPA ปกติ. Server ควบคุม PKCE + state + nonce — ต้องมี `ChannelSecret` + `RedirectUri` + `AllowedReturnUrlPrefixes` เพิ่ม (pattern เดียวกับ ThaID)

### Steps

1. Login → **Create a new provider** (Provider = "เจ้าของ" ของ channels)
2. เข้า provider → **Create a new channel** → **LINE Login**
3. ใส่ Channel name, description, region — กรอกจนเสร็จ
4. **Basic settings** → copy **Channel ID** (ตัวเลข ~10 หลัก)
5. **Basic settings** → copy **Channel secret** (คลิก **Issue** ถ้ายังไม่มี) — **เก็บไว้ใช้กับ Mode B (OIDC) เท่านั้น**, Mode A (LIFF) ไม่ต้อง
6. **LINE Login** tab → **LIFF** → **Add** → ใส่ (**สำหรับ Mode A**):
   - **Endpoint URL** = หน้า frontend ที่จะเปิดใน LIFF browser (เช่น `https://localhost:5001/test-html/test-line.html`)
   - **Scopes**: `openid`, `email`, `profile`
7. Copy **LIFF ID** (สำหรับ frontend `liff.init()`) — Channel ID ใช้ที่ backend
8. **LINE Login** tab → **Callback URL** (**สำหรับ Mode B**):
   - ใส่ URL ของ backend callback เช่น `https://your-domain/auth/external/callback/line` (dev: `https://localhost:5100/auth/external/callback/line`)
   - **ต้องตรงเป๊ะ** กับ `RedirectUri` ใน config (case-sensitive, มี/ไม่มี trailing slash ต่างกัน) — mismatch = LINE ปฏิเสธ callback
   - Production ต้องเป็น HTTPS จริง (LINE ไม่ยอมรับ localhost/http นอก dev)
9. (Optional) **OpenID Connect** → **Email address permission** → **Apply** → ให้ LINE approve ก่อนจะได้ email กลับมา

### Where to paste

**Mode A — LIFF / token-exchange** (minimal):

```jsonc
"Line": {
  "Enabled": true,
  "ChannelId": "<channel-id-from-line-console>",
  "VerifyEndpoint": "https://api.line.me/oauth2/v2.1/verify"
}
```

**Mode B — OIDC redirect** (challenge/callback endpoints เปิดใช้เมื่อตั้ง `ChannelSecret`):

```jsonc
"Line": {
  "Enabled": true,
  "ChannelId": "<channel-id-from-line-console>",
  "ChannelSecret": "<channel-secret-from-line-console>",
  "Authority": "https://access.line.me",
  "TokenEndpoint": "https://api.line.me/oauth2/v2.1/token",
  "RedirectUri": "https://localhost:5100/auth/external/callback/line",
  "AllowedReturnUrlPrefixes": [ "http://localhost:5173", "https://localhost:5100" ],
  "Scopes": "openid profile email",
  "StateLifetimeMinutes": 10
}
```

> **Note**:
> - `VerifyEndpoint` / `Authority` / `TokenEndpoint` เป็น default ของ LINE ปกติไม่ต้องแตะ
> - ไม่ตั้ง `ChannelSecret` = Mode A เท่านั้น (endpoint `POST /auth/external/line`); ตั้ง `ChannelSecret` = เปิด Mode B เพิ่ม (`GET /auth/external/challenge/line` + `/callback/line`) — Mode A ก็ยังใช้ได้ ทั้งคู่ทำงานพร้อมกันได้
> - Startup fail-fast: ถ้าตั้ง `ChannelSecret` แต่ไม่มี `RedirectUri` หรือ `AllowedReturnUrlPrefixes` ว่าง → app start ไม่ผ่าน
> - `AllowedReturnUrlPrefixes` = whitelist ของ frontend URL prefix ที่ยอมให้ redirect กลับหลัง login (open-redirect guard) — mismatch จะได้ 400 `LINE_RETURN_URL_NOT_ALLOWED`
> - ถ้ายังไม่ได้ approve email permission → user ที่ login จะถูก provision ด้วย placeholder `{subject}@line.local` + `EmailConfirmed=false`

---

## 5. ThaID (Thai national digital ID / DOPA)

**สำคัญ: ThaID ไม่ใช่ self-service** — ต้องยื่นเรื่องขอสิทธิ์ใช้งานกับ **กรมการปกครอง (DOPA)** ไม่มี developer console แบบสมัคร-แล้ว-ได้-คีย์ทันที

- **หน่วยงาน**: สำนักบริหารการทะเบียน (สบท.) กรมการปกครอง — <https://www.bora.dopa.go.th>
- **Sandbox authority**: `https://imauthtestc.bora.dopa.go.th/api/v2/oauth2`
- **Production authority**: `https://imauth.bora.dopa.go.th/api/v2/oauth2`

### Steps (real-world workflow)

1. **ติดต่อ DOPA** — ผ่านเว็บ <https://www.bora.dopa.go.th> หรือทำหนังสือราชการถึงสำนักบริหารการทะเบียน แจ้งความประสงค์ขอใช้ ThaID OAuth
2. **ยื่นเอกสาร**:
   - หนังสือขอใช้บริการ ThaID (แบบฟอร์มของ DOPA)
   - วัตถุประสงค์การใช้งาน + scope ข้อมูลที่ต้องการ (`pid`, `given_name`, `family_name`, `email`, `birthdate`, `address`)
   - **Redirect URI** (Callback URL) — เช่น `https://your-domain/auth/external/thaid/callback` (ต้องเป็น HTTPS จริง ไม่ใช่ localhost สำหรับ production)
3. **รับ sandbox credentials** จาก DOPA → ทดสอบกับ authority `https://imauthtestc.bora.dopa.go.th/api/v2/oauth2`
4. **Audit + review** → เมื่อผ่านแล้วขอ **production credentials** → authority `https://imauth.bora.dopa.go.th/api/v2/oauth2`

### Where to paste

```jsonc
"ThaId": {
  "Enabled": true,
  "ClientId": "<from-DOPA>",
  "ClientSecret": "<from-DOPA>",
  "Authority": "https://imauthtestc.bora.dopa.go.th/api/v2/oauth2",
  "RedirectUri": "https://localhost:5100/auth/external/thaid/callback",
  "AllowedReturnUrlPrefixes": [ "http://localhost:5173", "https://localhost:5100" ],
  "Scopes": "openid pid given_name family_name email birthdate address",
  "StateLifetimeMinutes": 10
}
```

> **Note**:
> - `RedirectUri` **ต้องตรงเป๊ะ** กับ URL ที่แจ้ง DOPA ตอนยื่นเรื่อง (case-sensitive, มี/ไม่มี trailing slash ต่างกัน) — mismatch → callback fail
> - `AllowedReturnUrlPrefixes` = whitelist frontend URL ที่ยอมให้ redirect กลับหลัง login (open-redirect guard)
> - `pid` scope = เลขบัตรประชาชน 13 หลัก — ใช้เป็นตัวระบุตัวตนหลัก
> - email เป็น optional — ถ้า DOPA ไม่ส่งกลับ user ถูก provision ด้วย `{pid}@thaid.local` + `EmailConfirmed=false`
> - Flow ต่างจาก 4 provider ข้างบน: browser navigate ไป `GET /auth/external/thaid/challenge?returnUrl=...` (redirect flow) ไม่ใช่ POST token

---

## ทดสอบหลังใส่คีย์เสร็จ

Test harness สำเร็จรูปอยู่ที่ [`test-html/`](../test-html/) — 1 ไฟล์ต่อ 1 provider:

```powershell
# 1. เปิด API backend
dotnet run --project src/AuthMicroservice.Api

# 2. เสิร์ฟหน้า test ผ่าน HTTPS (provider SDK บังคับ https://)
dotnet tool install -g dotnet-serve    # ครั้งแรก
dotnet dev-certs https --trust         # ครั้งแรก
dotnet serve -d c:\Code\AuthMicroServices -p 5001 -S

# 3. เปิด browser
Start-Process https://localhost:5001/test-html/test-google.html
```

รายละเอียดเพิ่มเติมดู [../README.md](../README.md) section *Running the browser test harnesses over HTTPS*.
