# AuthMicroservice — Installation Guide

คู่มือติดตั้ง step-by-step ครอบคลุมทั้ง 2 แบบการใช้งาน:

- **Library mode** — install NuGet เข้า .NET project ที่มีอยู่แล้ว → เรียก `AddAuthMicroservice(configuration)` เพื่อผูก auth เข้ากับ app ของคุณเลย
- **Standalone mode** — clone repo แล้ว `docker compose up` ได้ auth microservice แยกออกมาที่ port 8080 พร้อม SQL Server + Mailhog สำหรับ dev

ถ้ายังไม่แน่ใจว่าจะใช้แบบไหน อ่าน [Section 2: เลือก Mode](#2-เลือก-mode-ที่จะใช้) ก่อน

---

## 1. Prerequisites

| ต้องมี | เวอร์ชัน | จำเป็นเมื่อ |
|---|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.x (LTS) | ทั้ง 2 modes |
| Database engine | SQL Server 2019+ / PostgreSQL 13+ / SQLite 3.x / InMemory | เลือก 1 ตัว (SQLite/InMemory ไม่ต้อง install แยก) |
| Docker Desktop | 4.x+ | Standalone mode เท่านั้น |
| SMTP server | any | เฉพาะกรณีเปิด `Email.Enabled=true` — dev ใช้ Mailhog ที่มากับ compose |
| Git | any | Standalone mode (ต้อง clone repo) |

**Windows tip**: PowerShell 5.1+ / PowerShell 7 ใช้ได้ทั้งคู่ ตัวอย่างคำสั่งในเอกสารนี้เป็น PowerShell syntax

---

## 2. เลือก Mode ที่จะใช้

| Scenario | ใช้ Mode ไหน |
|---|---|
| Monolith .NET project อยากมี auth ในตัว | **Library** |
| อยากใช้ auth เดียวกันจากหลาย service (Node, Python, .NET รวมกัน) | **Standalone** |
| ต้อง deploy เป็น container แยก | **Standalone** |
| Prototype / hobby project | **Library** (ใช้ SQLite + `Email.Enabled=false` ได้เลย) |

**Library mode → ข้ามไป [Section 3](#3-path-a-library-mode)**
**Standalone mode → ข้ามไป [Section 4](#4-path-b-standalone-mode)**

---

## 3. Path A: Library Mode

### Step 1 — Install NuGet packages

ใน .NET 8 web project ของคุณ:

```powershell
dotnet add package Synergy.AuthMicroservice.Core

# เลือก 1 provider — ตัวไหนก็ได้ที่ตรงกับ DB ที่จะใช้
dotnet add package Synergy.AuthMicroservice.Migrations.SqlServer
# หรือ
dotnet add package Synergy.AuthMicroservice.Migrations.Postgres
# หรือ
dotnet add package Synergy.AuthMicroservice.Migrations.Sqlite
# หรือ (สำหรับ test/demo เท่านั้น — data หายทุก restart)
dotnet add package Synergy.AuthMicroservice.Migrations.InMemory
```

> Namespaces ยังเป็น `AuthMicroservice.Core.*` (คงเดิม) — เฉพาะ package IDs ที่ขึ้นต้นด้วย `Synergy.`

### Step 2 — แก้ `Program.cs`

Template ที่สั้นที่สุด — 5 บรรทัด (เอามาจาก [src/AuthMicroservice.Sample/Program.cs](../src/AuthMicroservice.Sample/Program.cs)):

```csharp
using AuthMicroservice.Core.Extensions;
using AuthMicroservice.Migrations.Sqlite;   // เปลี่ยนตาม provider ที่ install

var builder = WebApplication.CreateBuilder(args);

// 1. Register services + เลือก provider (fluent chaining)
builder.Services.AddAuthMicroservice(builder.Configuration).UseSqlite();
//                                                          ^^^^^^^^^^
//                          .UseSqlServer() / .UsePostgres() / .UseInMemory()

var app = builder.Build();

// 2. Middleware (auth + authorization)
app.UseAuthMicroservice();

// 3. Mount endpoints (default prefix = /auth)
app.MapAuthMicroservice();

// 4. Apply migrations + seed default roles (Admin, User) ก่อน Run
await app.ApplyAuthMicroserviceMigrationsAsync();

app.Run();
```

**สิ่งที่ได้หลัง Run:**
- Endpoints: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me`, ... (ดู [README](../README.md#api-endpoints-mounted-under-routeprefix-default-auth) — endpoint table)
- Swagger UI ที่ `/swagger` (ถ้า `EnableSwagger=true`)
- Roles `Admin` และ `User` ถูก seed อัตโนมัติ

**ต้องการ config-driven provider dispatch แทน fluent chaining?** ดู pattern ใน [src/AuthMicroservice.Api/Program.cs](../src/AuthMicroservice.Api/Program.cs) — เหมาะเมื่อ app เดียวต้อง support หลาย DB เลือก provider จาก `appsettings.json`

### Step 3 — แก้ `appsettings.json`

Minimum viable config (SQLite, ไม่ใช้ email) — paste ทั้งก้อนได้เลย:

```jsonc
{
  "AuthMicroservice": {
    "Database": {
      "Provider": "Sqlite",                 // SqlServer | Postgres | Sqlite | InMemory
      "ConnectionString": "Data Source=auth.db",
      "AutoMigrate": true,                   // apply migrations ตอน startup
      "SeedDefaults": true                    // seed roles Admin + User
    },
    "Jwt": {
      "Key": "REPLACE_VIA_ENV_VAR_MIN_32_CHARS"
    },
    "Email": {
      "Enabled": false                        // ปิด → /verify-email, /forgot-password endpoints คืน 404
    },
    "TokenLinks": {
      "EmailVerificationBaseUrl": "https://app.example.com/verify-email",
      "PasswordResetBaseUrl": "https://app.example.com/reset-password"
    }
  }
}
```

**Full schema** (JWT lifetime, Identity password rules, lockout, SMTP, external providers): [README — Configuration reference](../README.md#configuration-reference)

**Startup fail-fast checks** (อ้างอิง [AuthMicroserviceOptionsValidator.cs](../src/AuthMicroservice.Core/Configuration/AuthMicroserviceOptionsValidator.cs)):

| ถ้าไม่ตั้งค่า / ตั้งผิด | Error |
|---|---|
| `Jwt.Key` < 32 chars หรือว่าง | `AuthMicroservice:Jwt:Key must be set and at least 32 characters long.` |
| `Email.Enabled=true` แต่ไม่มี `Smtp.Host` / `FromAddress` | error รายบรรทัด |
| External provider `Enabled=true` แต่ไม่มี credentials | error รายบรรทัด |
| `TokenLinks.EmailVerificationBaseUrl` หรือ `PasswordResetBaseUrl` ว่าง | required (ต้องมีทั้งคู่ แม้ปิด Email ก็ตาม) |

### Step 4 — Set secrets ผ่าน environment variables

**อย่าเก็บ secrets ใน `appsettings.json`** — ใช้ double-underscore syntax แทน:

```powershell
# PowerShell — dev machine
$env:AuthMicroservice__Jwt__Key = "your-32-plus-character-random-secret-here"
$env:AuthMicroservice__Database__ConnectionString = "Data Source=auth.db"

# ถ้าเปิด Email
$env:AuthMicroservice__Email__Smtp__Password = "your-smtp-password"
```

Production: set ผ่าน hosting environment (Azure App Service Configuration, AWS Parameter Store, Docker Secrets, ฯลฯ)

### Step 5 — Run

```powershell
dotnet run
```

Log ควรบอก `Now listening on: http://localhost:5xxx` — เปิด `http://localhost:5xxx/swagger` เพื่อทดสอบ

Migrations apply อัตโนมัติเพราะเรียก `ApplyAuthMicroserviceMigrationsAsync()` — **ไม่ต้อง** รัน `dotnet ef database update` เอง

**Quick smoke test:**

```powershell
$body = @{ email="test@example.com"; password="P@ssw0rd!"; fullName="Test" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri "http://localhost:5xxx/auth/register" -ContentType "application/json" -Body $body
```

ควรได้ response 201 พร้อม `accessToken` + `refreshToken`

**Reference implementation ที่รันได้จริง:** [src/AuthMicroservice.Sample/](../src/AuthMicroservice.Sample/) — clone repo แล้ว `dotnet run --project src/AuthMicroservice.Sample` เห็นการใช้งาน end-to-end

---

## 4. Path B: Standalone Mode

รัน AuthMicroservice เป็น service แยกผ่าน Docker Compose — เหมาะกรณีมี client หลาย stack (Node, Python, mobile) ที่ต้อง auth เดียวกัน

### Step 1 — Clone repo

```powershell
git clone https://github.com/<your-org>/AuthMicroServices.git
cd AuthMicroServices
```

### Step 2 — เตรียม `.env`

Copy จาก `.env.example` แล้วเปิดแก้:

```powershell
copy .env.example .env
notepad .env
```

ค่าที่ **ต้องแก้**:

```dotenv
# JWT signing key — ต้องยาว >= 32 chars (ไม่งั้น startup fail)
JWT_KEY=please-generate-a-32-plus-character-random-secret

# SQL Server SA password ที่ compose ใช้
MSSQL_SA_PASSWORD=Your_Strong_P@ssword_2026
```

**Generate JWT key แบบสุ่ม** (PowerShell):

```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
```

### Step 3 — Start containers

```powershell
docker compose up -d --build
```

จะได้ 3 containers:

| Container | Port | หน้าที่ |
|---|---|---|
| `authmicroservice-api` | `8080` | Auth API |
| `authmicroservice-db` (SQL Server 2022) | `1433` | Database |
| `authmicroservice-mailhog` | `8025` (UI), `1025` (SMTP) | Fake SMTP สำหรับ dev |

### Step 4 — Verify

```powershell
# health check
curl http://localhost:8080/auth/health

# Swagger UI
Start-Process http://localhost:8080/swagger

# Mailhog inbox (จะเห็น verification / reset emails ที่ถูกส่งออกไป)
Start-Process http://localhost:8025
```

Migrations + seed roles apply อัตโนมัติตอน container start

### Step 5 — Run end-to-end verification

ดู [Section 6: Verify installation](#6-verify-installation-end-to-end) — 7 คำสั่งครอบคลุม register → verify → login → protected endpoint → refresh → forgot-password → lockout

**หยุด / restart:**

```powershell
docker compose down          # หยุด + ลบ containers (data ใน volume ยังอยู่)
docker compose down -v       # ลบ volume ด้วย (fresh start)
docker compose up -d         # start ใหม่
```

---

## 5. External Providers (optional)

ทั้ง 5 providers **ปิด default** — endpoint คืน 404 (`{PROVIDER}_LOGIN_DISABLED`) จนกว่าจะ enable

**เปิดผ่าน env vars** (ตัวอย่าง Google):

```powershell
$env:AuthMicroservice__ExternalProviders__Google__Enabled = "true"
$env:AuthMicroservice__ExternalProviders__Google__ClientId = "<your>.apps.googleusercontent.com"
```

Provider ที่รองรับ + credentials ที่ต้องมี:

| Provider | ต้องมี | Endpoint |
|---|---|---|
| Google | `ClientId` | `POST /auth/external/google` |
| Microsoft | `ClientId`, `TenantId` (default `common`) | `POST /auth/external/microsoft` |
| Facebook | `AppId`, `AppSecret` | `POST /auth/external/facebook` |
| LINE | `ChannelId` | `POST /auth/external/line` |
| ThaID | `ClientId`, `ClientSecret`, `Authority`, `RedirectUri`, `AllowedReturnUrlPrefixes` | `GET /auth/external/thaid/challenge` → `GET /auth/external/thaid/callback` (redirect flow) |

**วิธีขอ credentials จาก Developer Console ของแต่ละ provider** (step-by-step): [docs/PROVIDER_SETUP.md](PROVIDER_SETUP.md)

**ทดสอบ provider จริงในเบราว์เซอร์**: `test-html/test-{google,microsoft,facebook,line,thaid}.html` — ดู [README — Running the browser test harnesses over HTTPS](../README.md#running-the-browser-test-harnesses-over-https)

---

## 6. Verify installation (end-to-end)

รันสมมติว่า service อยู่ที่ `http://localhost:8080` (standalone) หรือเปลี่ยน port เป็นของ library mode

```powershell
# 1. Register
$reg = @{ email="alice@example.com"; password="P@ssw0rd!"; fullName="Alice" } | ConvertTo-Json
$response = Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/register `
                              -ContentType "application/json" -Body $reg

# 2. เอา verification link จาก Mailhog (http://localhost:8025) แล้ว POST
$ver = @{ userId="<GUID จาก email>"; token="<TOKEN จาก email>" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/verify-email `
                  -ContentType "application/json" -Body $ver

# 3. Login
$login = @{ email="alice@example.com"; password="P@ssw0rd!" } | ConvertTo-Json
$auth = Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/login `
                          -ContentType "application/json" -Body $login

# 4. Protected endpoint
Invoke-RestMethod -Uri http://localhost:8080/auth/me `
                  -Headers @{ Authorization = "Bearer $($auth.accessToken)" }

# 5. Rotate refresh token — refresh token เก่าใช้ไม่ได้อีก
$rf = @{ accessToken=$auth.accessToken; refreshToken=$auth.refreshToken } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/refresh `
                  -ContentType "application/json" -Body $rf

# 6. Forgot password (token จะไปโผล่ Mailhog)
$fp = @{ email="alice@example.com" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/forgot-password `
                  -ContentType "application/json" -Body $fp

# 7. Trigger lockout (default 5 failed attempts → 423 Locked)
1..5 | ForEach-Object {
    try {
        Invoke-RestMethod -Method POST -Uri http://localhost:8080/auth/login `
                          -ContentType "application/json" `
                          -Body (@{ email="alice@example.com"; password="wrong" } | ConvertTo-Json)
    } catch { Write-Host "Attempt $_ : $($_.Exception.Response.StatusCode)" }
}
```

ผ่านหมดทั้ง 7 step = install สำเร็จ พร้อมขึ้น production (หลัง review Jwt.Key + connection string + SMTP + rate limiting)

---

## 7. Troubleshooting

| Symptom | สาเหตุ / วิธีแก้ |
|---|---|
| Startup ตายพร้อม error `AuthMicroservice:Jwt:Key must be set and at least 32 characters long.` | Set `AuthMicroservice__Jwt__Key` env var (ยาว >= 32 chars) |
| Startup ตายพร้อม `Unsupported provider '...'` | `AuthMicroservice:Database:Provider` ต้องเป็น 1 ใน `SqlServer` / `Postgres` / `Sqlite` / `InMemory` (case-sensitive) |
| Startup ตายพร้อม `EmailVerificationBaseUrl is required` | ต้องตั้ง `TokenLinks.EmailVerificationBaseUrl` + `TokenLinks.PasswordResetBaseUrl` ทั้งคู่ **แม้ปิด Email** (validator ตรวจก่อนดู `Email.Enabled`) |
| `/auth/register` คืน 500 + log บอก DB error | เช็ค `ConnectionString` + DB ต้อง running แล้ว (โดยเฉพาะ SQL Server / Postgres) |
| Migrations ไม่ apply แต่ startup ผ่าน | ลืมเรียก `await app.ApplyAuthMicroserviceMigrationsAsync()` ก่อน `app.Run()` |
| External login คืน 409 `EMAIL_EXISTS_UNVERIFIED` | มี local user email เดียวกัน แต่ยังไม่ verify — ยิง `/auth/verify-email` ให้ user นั้นก่อน (กัน account takeover ผ่าน unverified email) |
| Provider endpoint คืน 404 `{PROVIDER}_LOGIN_DISABLED` | ยัง set `ExternalProviders:{Provider}:Enabled` เป็น `true` |
| `docker compose up` — `authmicroservice-db` แล้ว restart loop | SA password ไม่ตรง SQL Server complexity — ใช้ตัวอักษรใหญ่/เล็ก/ตัวเลข/สัญลักษณ์ + ยาว >= 8 |
| Mailhog ไม่มี email | เช็ค `Email.Enabled=true` + `Smtp.Host=mailhog` (ใน compose) / `Smtp.Host=localhost` + `Smtp.Port=1025` (dev machine) |

---

## 8. Next steps

- **Feature list + endpoint table**: [README.md](../README.md)
- **External provider setup ละเอียด (Developer Console → credentials)**: [docs/PROVIDER_SETUP.md](PROVIDER_SETUP.md)
- **Working library-mode consumer เป็นตัวอย่าง**: [src/AuthMicroservice.Sample/](../src/AuthMicroservice.Sample/) — `dotnet run --project src/AuthMicroservice.Sample`
- **Swap email transport เป็น SendGrid / SES / อื่นๆ**: [README — Swapping the email transport](../README.md#swapping-the-email-transport) — ทำผ่าน `services.AddSingleton<IEmailSender, ...>()`
- **สร้าง migration ใหม่ (schema change)**: [README — EF Core migrations](../README.md#ef-core-migrations-per-provider) — ต้องรัน `dotnet ef migrations add` 3 ครั้ง (SqlServer / Postgres / Sqlite)
