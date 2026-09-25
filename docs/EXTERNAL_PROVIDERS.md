# External login providers

Five providers are supported: **Google**, **Microsoft**, **Facebook**, **LINE**, and **ThaID** (Thai national digital ID). The first four use a **token-exchange** flow — client (SPA / mobile) obtains a provider token via the native SDK, POSTs it to this service, and gets back this service's own JWT + refresh token (no cookie/redirect handshake). **ThaID always uses the classic OIDC redirect flow** (server-hosted `/challenge` + `/callback`) because DOPA mandates it. **LINE supports both** — LIFF/token-exchange (default), and OIDC redirect (activated when `Line.ChannelSecret` is set) — so you can pick whichever fits your client (LIFF app vs. web SPA).

> **จะเอาคีย์แต่ละ provider มาจากไหน?** ดู [docs/PROVIDER_SETUP.md](PROVIDER_SETUP.md) — step-by-step guide ตั้งแต่สมัคร Developer Console ของ Google / Microsoft / Facebook / LINE / DOPA จนได้ credentials มาวางใน `appsettings.json`

## Shared behaviour (all providers)

1. Validates the incoming token with the provider (signature/audience/expiry, or provider debug endpoint for opaque tokens).
2. If a link already exists in `AspNetUserLogins` for `(Provider, subject)` → issues tokens.
3. Else if a local user with the same email exists:
   - `EmailConfirmed=true` → auto-links the external identity and issues tokens.
   - `EmailConfirmed=false` → returns `EMAIL_EXISTS_UNVERIFIED` (409). Verify the local account first (via `/auth/verify-email`) before retrying, to prevent account takeover through unverified addresses.
4. Else → auto-provisions a new `ApplicationUser` with `EmailConfirmed=true`, assigns the `User` role, links the external identity, and issues tokens.

Every provider is **disabled by default** in [appsettings.json](../src/AuthMicroservice.Api/appsettings.json) — a disabled endpoint responds 404 (`{PROVIDER}_LOGIN_DISABLED`). Startup fail-fast if `Enabled=true` without required credentials.

Ready-to-run browser test harnesses (grab a real token from the provider and POST it — or, for ThaID, kick off the redirect flow) sit under `test-html/`: [test-html/test-google.html](../test-html/test-google.html), [test-html/test-microsoft.html](../test-html/test-microsoft.html), [test-html/test-facebook.html](../test-html/test-facebook.html), [test-html/test-line.html](../test-html/test-line.html), [test-html/test-thaid.html](../test-html/test-thaid.html).

## Google

- **Endpoint**: `POST /auth/external/google` — body `{ "code": "..." }` (authorization code from Google Identity Services **OAuth 2.0 Code Client**)
- **Config**: `AuthMicroservice:ExternalProviders:Google:{ Enabled, ClientId, ClientSecret }`
- **Validation**: **authorization-code exchange** — backend POST `code` + `ClientId` + `ClientSecret` ไปที่ `https://oauth2.googleapis.com/token` (redirect_uri = `postmessage` สำหรับ popup flow) เพื่อแลก `id_token` แล้ว validate signature/`aud` = `ClientId`/`exp` ผ่าน Google JWKS
- **Provider quirks**: rejects with `GOOGLE_EMAIL_NOT_VERIFIED` (400) if Google's `email_verified` claim is false
- **Errors**: `INVALID_GOOGLE_TOKEN` (401 — code exchange fail หรือ id_token invalid), `GOOGLE_EMAIL_NOT_VERIFIED` (400), `GOOGLE_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Google__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Google__ClientId = "<your>.apps.googleusercontent.com"
$env:AuthMicroservice__ExternalProviders__Google__ClientSecret = "<google-client-secret>"
```

## Microsoft

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

## Facebook

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

## LINE

LINE supports **two flows** — เลือกใช้ตาม client:

**Mode A — LIFF / token-exchange** (default, ไม่ต้องตั้ง `ChannelSecret`)

- **Endpoint**: `POST /auth/external/line` — body `{ "idToken": "..." }` (obtained from LIFF via `liff.getIDToken()`)
- **Config**: `AuthMicroservice:ExternalProviders:Line:{ Enabled, ChannelId, VerifyEndpoint }` (default endpoint `https://api.line.me/oauth2/v2.1/verify`)
- **Validation**: POST `id_token` + `ChannelId` to LINE verify endpoint — validates `aud` = `ChannelId`, `iss` = `https://access.line.me`, `exp`
- **Provider quirks**: email is optional in the LINE Login scope. If the user's channel/consent does not include email, the user is still auto-provisioned with a synthesized placeholder email `{subject}@line.local` and `EmailConfirmed=false` (same pattern as ThaID). If email *is* returned, it is stored as-is with `EmailConfirmed=true`.
- **Errors**: `INVALID_LINE_TOKEN` (401), `LINE_LOGIN_DISABLED` (404)

```powershell
$env:AuthMicroservice__ExternalProviders__Line__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Line__ChannelId = "<line-login-channel-id>"
```

**Mode B — OIDC redirect (challenge/callback)** — เปิดโดยตั้ง `ChannelSecret`

Pattern เดียวกับ ThaID — เหมาะกับ web SPA ที่ไม่ใช่ LIFF app หรือกรณีอยากให้ server ควบคุม PKCE / state / nonce เอง

- **Endpoints (redirect flow, no token-exchange)**:
  - `GET /auth/external/challenge/line?returnUrl=<frontend-url>` — สร้าง state + PKCE code_verifier + nonce, เก็บผ่าน `ILineStateStore` (in-memory default), แล้ว 302-redirect ไปหน้า authorize ของ LINE
  - `GET /auth/external/callback/line?code=&state=` — เรียกโดย LINE หลัง user login: verify state, แลก `code` เอา `id_token` ที่ `TokenEndpoint`, ตรวจ nonce, ออก JWT + refresh, แล้ว 302 กลับ `returnUrl` (พร้อม tokens ต่อท้ายเป็น URL fragment)
- **Config**: `AuthMicroservice:ExternalProviders:Line:{ Enabled, ChannelId, ChannelSecret, Authority, TokenEndpoint, RedirectUri, AllowedReturnUrlPrefixes, Scopes, StateLifetimeMinutes }`
  - `Authority` default `https://access.line.me`
  - `TokenEndpoint` default `https://api.line.me/oauth2/v2.1/token`
  - `RedirectUri` **ต้องตรงกับ** Callback URL ที่ลงทะเบียนใน LINE Developers console
  - `AllowedReturnUrlPrefixes` = whitelist ของ frontend URL prefix ที่ยอมให้ redirect กลับ (open-redirect guard)
  - `Scopes` default `openid profile email`
  - `StateLifetimeMinutes` default `10`
- **Validation**: OIDC — id_token verify กับ LINE, PKCE (S256), state + nonce ตรวจสอบผ่าน `ILineStateStore`
- **Errors (นอกเหนือจาก Mode A)**: `INVALID_LINE_STATE` (400 — state ไม่ตรง/หมดอายุ), `LINE_RETURN_URL_NOT_ALLOWED` (400 — `returnUrl` ไม่อยู่ใน `AllowedReturnUrlPrefixes`), `INVALID_LINE_TOKEN` (401 — code exchange fail หรือ nonce mismatch)

```powershell
$env:AuthMicroservice__ExternalProviders__Line__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Line__ChannelId = "<line-login-channel-id>"
$env:AuthMicroservice__ExternalProviders__Line__ChannelSecret = "<line-channel-secret>"
$env:AuthMicroservice__ExternalProviders__Line__RedirectUri = "https://localhost:5100/auth/external/callback/line"
$env:AuthMicroservice__ExternalProviders__Line__AllowedReturnUrlPrefixes__0 = "http://localhost:5173"
```

## ThaID (Thai national digital ID / DOPA)

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

## Sample request

คล้ายกันทั้ง 4 provider — ต่างกันแค่ path และ field name: **Microsoft/LINE** ใช้ `idToken`, **Facebook** ใช้ `accessToken`, **Google** ใช้ `code` (authorization code — backend แลก `id_token` ต่อกับ Google เอง):

```http
POST /auth/external/google
Content-Type: application/json

{ "code": "4/0Ab_5qll..." }
```

```http
POST /auth/external/microsoft
Content-Type: application/json

{ "idToken": "eyJhbGciOi..." }
```

**ThaID is different**: the browser starts by navigating to `GET /auth/external/thaid/challenge?returnUrl=https://myapp/login-callback` (no body); the service handles the rest of the OIDC dance and eventually redirects the browser back to `returnUrl` with the issued tokens.

## Running the browser test harnesses over HTTPS

Provider SDKs (LIFF, Google Identity, MSAL, Facebook Login) require the page to be served over HTTPS — `file://` and plain `http://` won't work. Serve the repo root with [`dotnet-serve`](https://github.com/natemcmaster/dotnet-serve) — dev cert is generated automatically:

```powershell
dotnet tool install -g dotnet-serve                    # one-time install
dotnet dev-certs https --trust                          # one-time trust
dotnet serve -d c:\Code\AuthMicroServices -p 5001 -S    # -S = HTTPS
```

เปิด `https://localhost:5001/test-html/test-line.html` (หรือ `test-google.html` / `test-microsoft.html` / `test-facebook.html` / `test-thaid.html`) เพื่อทดสอบแต่ละ provider.
