# Provider Setup Guide

ไฟล์นี้บอก **วิธีขอ credentials จาก Developer Console ของแต่ละ provider** — สำหรับ config schema, endpoint, และ error codes ดูที่ [../README.md](../README.md) section *External login providers*.

Config ทุก provider อยู่ใต้ `AuthMicroservice:ExternalProviders:<Provider>` ใน [src/AuthMicroservice.Api/appsettings.json](../src/AuthMicroservice.Api/appsettings.json) และ override ผ่าน env var ได้ (double-underscore syntax เช่น `AuthMicroservice__ExternalProviders__Google__ClientId`).

## Quick reference

| Provider | Developer Console | Config section | Required keys |
|---|---|---|---|
| Google | [console.cloud.google.com](https://console.cloud.google.com) | `Google` | `ClientId` |
| Microsoft | [portal.azure.com](https://portal.azure.com) → Entra ID | `Microsoft` | `ClientId`, `TenantId` |
| Facebook | [developers.facebook.com/apps](https://developers.facebook.com/apps) | `Facebook` | `AppId`, `AppSecret` |
| LINE | [developers.line.biz/console](https://developers.line.biz/console) | `Line` | `ChannelId` |
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
5. เพิ่ม **Authorized JavaScript origins** (เช่น `https://localhost:5001`) และ **Authorized redirect URIs** (ถ้าใช้ redirect flow)
6. กด **Create** → copy **Client ID** (รูปแบบ `xxxxxxx.apps.googleusercontent.com`)

### Where to paste

```jsonc
"AuthMicroservice": {
  "ExternalProviders": {
    "Google": {
      "Enabled": true,
      "ClientId": "xxxxxxx.apps.googleusercontent.com"
    }
  }
}
```

> **Note**: token-exchange flow ที่ใช้ในโปรเจกต์นี้ **ไม่ต้องใช้ ClientSecret** — frontend เอา `id_token` มาส่งเข้า backend ตรง ๆ

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
  "AppId": "1757562765571027",
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

### Steps

1. Login → **Create a new provider** (Provider = "เจ้าของ" ของ channels)
2. เข้า provider → **Create a new channel** → **LINE Login**
3. ใส่ Channel name, description, region — กรอกจนเสร็จ
4. **Basic settings** → copy **Channel ID** (ตัวเลข ~10 หลัก)
5. **LINE Login** tab → **LIFF** → **Add** → ใส่:
   - **Endpoint URL** = หน้า frontend ที่จะเปิดใน LIFF browser (เช่น `https://localhost:5001/test-html/test-line.html`)
   - **Scopes**: `openid`, `email`, `profile`
6. Copy **LIFF ID** (สำหรับ frontend `liff.init()`) — Channel ID ใช้ที่ backend
7. (Optional) **OpenID Connect** → **Email address permission** → **Apply** → ให้ LINE approve ก่อนจะได้ email กลับมา

### Where to paste

```jsonc
"Line": {
  "Enabled": true,
  "ChannelId": "2011682506",
  "VerifyEndpoint": "https://api.line.me/oauth2/v2.1/verify"
}
```

> **Note**:
> - `VerifyEndpoint` เป็น default ของ LINE ปกติไม่ต้องแตะ
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
