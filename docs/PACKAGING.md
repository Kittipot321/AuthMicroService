# AuthMicroservice — Packaging Guide

คู่มือฝั่ง **maintainer** — วิธี build `.nupkg` ออกจาก repo นี้ + วิธี smoke test package กับ local folder feed ก่อน publish จริง

> ถ้าคุณเป็น **consumer** ที่จะ install package ไปใช้ → ข้ามไปที่ [INSTALLATION.md](INSTALLATION.md)

---

## 1. Overview

Repo นี้ผลิต NuGet packages **5 ตัว** — metadata (Version, Authors, License, RepositoryUrl) รวมไว้ที่ [Directory.Build.props](../Directory.Build.props) ตัวเดียว ส่วน `PackageId` + `Description` แยกใน `.csproj` แต่ละตัว

| PackageId | Project | หน้าที่ |
|---|---|---|
| `Synergy.AuthMicroservice.Core` | [src/AuthMicroservice.Core](../src/AuthMicroservice.Core/) | Reusable auth library (JWT, Identity, email, providers) |
| `Synergy.AuthMicroservice.Migrations.SqlServer` | [src/AuthMicroservice.Migrations.SqlServer](../src/AuthMicroservice.Migrations.SqlServer/) | EF Core migrations สำหรับ SQL Server |
| `Synergy.AuthMicroservice.Migrations.Postgres` | [src/AuthMicroservice.Migrations.Postgres](../src/AuthMicroservice.Migrations.Postgres/) | EF Core migrations สำหรับ PostgreSQL |
| `Synergy.AuthMicroservice.Migrations.Sqlite` | [src/AuthMicroservice.Migrations.Sqlite](../src/AuthMicroservice.Migrations.Sqlite/) | EF Core migrations สำหรับ SQLite |
| `Synergy.AuthMicroservice.Migrations.InMemory` | [src/AuthMicroservice.Migrations.InMemory](../src/AuthMicroservice.Migrations.InMemory/) | InMemory adapter (test/demo เท่านั้น) |

**Projects อื่นไม่ถูก pack** (ไม่มี `<IsPackable>true</IsPackable>`):
- [src/AuthMicroservice.Api](../src/AuthMicroservice.Api/) — standalone Web API host
- [src/AuthMicroservice.Sample](../src/AuthMicroservice.Sample/) — library-mode demo consumer
- [tests/*](../tests/) — test projects

Migration packages มี `ProjectReference → AuthMicroservice.Core` — ตอน pack จะถูก convert เป็น NuGet dependency อัตโนมัติ (consumer install migration package ตัวเดียวได้ Core ตามมาด้วย)

---

## 2. Prerequisites

| ต้องมี | เวอร์ชัน |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.x (LTS) — check ด้วย `dotnet --version` |
| Repo state | clone แล้ว, `dotnet restore` ผ่าน |

**Windows tip**: ตัวอย่างคำสั่งในเอกสารนี้เป็น PowerShell (5.1+ / 7 ใช้ได้ทั้งคู่)

---

## 3. Pre-pack checklist

ก่อนรัน `dotnet pack` ทุกครั้ง check 3 ข้อนี้:

### 3.1 Bump version

เปิด [Directory.Build.props](../Directory.Build.props) แล้วอัพ `<Version>` ตาม SemVer (`MAJOR.MINOR.PATCH`):

```xml
<Version>1.0.3</Version>
```

**Guideline สั้นๆ**:
- `PATCH` — bug fix, ไม่เปลี่ยน public API
- `MINOR` — เพิ่ม feature แบบ backward-compatible
- `MAJOR` — breaking change

Package ทั้ง 5 ตัว share version เดียวกันจาก Directory.Build.props (ปัจจุบัน = **1.0.2**)

### 3.2 Review metadata

ค่าที่ควรเช็คก่อน publish ให้คน outside org:

| Field | ค่าปัจจุบัน | หมายเหตุ |
|---|---|---|
| `<Authors>` | Synergy Software | ok |
| `<Company>` | Synergy Software | ok |
| `<PackageLicenseExpression>` | MIT | ok |
| `<RepositoryUrl>` | `https://github.com/your-org/AuthMicroservices` | **placeholder** — ต้องแก้เป็น URL จริงก่อน publish public |
| `<RepositoryType>` | git | ok |

### 3.3 Commit ก่อน pack

แนะนำให้ `git status` clean ก่อน pack เพื่อให้ trace ได้ว่า `.nupkg` version X ตรงกับ commit ไหน (ถ้าจะ auto-generate ผ่าน CI ในอนาคต จะ enforce ผ่าน tag)

---

## 4. Pack commands

### 4.1 Pack ทั้ง solution (แนะนำ)

จะได้ `.nupkg` ครบ 5 ไฟล์ในคำสั่งเดียว:

```powershell
dotnet pack AuthMicroservice.sln -c Release -o .\artifacts
```

### 4.2 Pack เฉพาะ package เดียว

```powershell
dotnet pack .\src\AuthMicroservice.Core\AuthMicroservice.Core.csproj -c Release -o .\artifacts
```

> ระวัง: ถ้า pack เฉพาะ migration package โดยที่ Core version ใหม่ยัง publish ไม่ถึง feed → consumer จะ install ไม่ได้เพราะ dependency ชี้ไป Core version ใหม่

### 4.3 Flag ที่ใช้บ่อย

| Flag | หน้าที่ | เมื่อไหร่ควรใช้ |
|---|---|---|
| `-c Release` | build ด้วย Release configuration | เสมอสำหรับ pack production |
| `-o .\artifacts` | output ไปที่ folder `artifacts/` | เสมอ (folder นี้อยู่ใน [.gitignore](../.gitignore) แล้ว) |
| `--include-symbols` | สร้าง `.snupkg` ควบคู่สำหรับ debug symbols | ถ้าจะให้ consumer debug ลึกถึง library code ได้ |
| `--no-build` | ข้าม build step | เมื่อเพิ่ง `dotnet build -c Release` เสร็จมาแล้ว |
| `/p:Version=1.0.3` | override version จาก CLI | สำหรับ ad-hoc pack ที่ไม่อยาก commit Directory.Build.props |

---

## 5. Verify output

```powershell
ls .\artifacts
```

ควรเห็น **5 ไฟล์** version เดียวกัน (สมมติ version 1.0.2):

```
Synergy.AuthMicroservice.Core.1.0.2.nupkg
Synergy.AuthMicroservice.Migrations.SqlServer.1.0.2.nupkg
Synergy.AuthMicroservice.Migrations.Postgres.1.0.2.nupkg
Synergy.AuthMicroservice.Migrations.Sqlite.1.0.2.nupkg
Synergy.AuthMicroservice.Migrations.InMemory.1.0.2.nupkg
```

### 5.1 Inspect .nupkg content

`.nupkg` เป็น zip file — ใช้ `Expand-Archive` เปิดดูข้างในได้:

```powershell
Expand-Archive .\artifacts\Synergy.AuthMicroservice.Core.1.0.2.nupkg `
               -DestinationPath .\artifacts\inspect\Core -Force
Get-Content .\artifacts\inspect\Core\Synergy.AuthMicroservice.Core.nuspec
```

**สิ่งที่ควรเห็นใน `.nuspec`**:
- `<version>1.0.2</version>` — ตรงกับ Directory.Build.props
- `<description>Reusable authentication library ...</description>` — ตรงกับ csproj
- `<license type="expression">MIT</license>`
- `<dependencies>` — list `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `MailKit`, `FluentValidation`, ... ครบตาม `<PackageReference>` ใน [Core.csproj](../src/AuthMicroservice.Core/AuthMicroservice.Core.csproj)

**Check migration package** — ต้องเห็น dependency ชี้ไป Core:

```powershell
Expand-Archive .\artifacts\Synergy.AuthMicroservice.Migrations.Sqlite.1.0.2.nupkg `
               -DestinationPath .\artifacts\inspect\Sqlite -Force
Get-Content .\artifacts\inspect\Sqlite\Synergy.AuthMicroservice.Migrations.Sqlite.nuspec
```

ต้องมี:
```xml
<dependency id="Synergy.AuthMicroservice.Core" version="1.0.2" ... />
```

ถ้าไม่มี → `ProjectReference` ไม่ถูก convert (อาจเป็นเพราะ Core ไม่ได้ `IsPackable=true`)

---

## 6. Test package locally (local folder feed)

Test `.nupkg` กับ consumer project จริงก่อน push ขึ้น feed public — ลด risk ปล่อย package พังไปแล้วต้อง unlist

### Step 1 — Register `.\artifacts` เป็น NuGet source

```powershell
dotnet nuget add source (Resolve-Path .\artifacts).Path -n AuthMicroservice-Local
```

Verify:
```powershell
dotnet nuget list source
```

ควรเห็น `AuthMicroservice-Local [Enabled]` พร้อม path ไป `artifacts/`

### Step 2 — สร้าง test consumer project นอก repo

```powershell
cd $env:TEMP
dotnet new webapi -n PackTest
cd PackTest
dotnet add package Synergy.AuthMicroservice.Core `
    --source AuthMicroservice-Local --version 1.0.2
dotnet add package Synergy.AuthMicroservice.Migrations.Sqlite `
    --source AuthMicroservice-Local --version 1.0.2
```

### Step 3 — Wire + smoke test

ทำตาม [INSTALLATION.md — Step 2 (Program.cs) + Step 3 (appsettings.json)](INSTALLATION.md#3-path-a-library-mode) แล้ว:

```powershell
$env:AuthMicroservice__Jwt__Key = "test-jwt-key-must-be-at-least-32-chars-long"
dotnet run
```

ยิง smoke test:
```powershell
$body = @{ email="test@example.com"; password="P@ssw0rd!"; fullName="Test" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:5xxx/auth/register `
                  -ContentType "application/json" -Body $body
```

ควรได้ HTTP 201 + `accessToken` + `refreshToken` → package ใช้งานได้จริง

### Step 4 — Clear cache หลัง test

ถ้าจะ re-pack version **เดิม** (เช่น 1.0.2) อีกรอบ NuGet จะ cache version ที่ install ไปแล้วไว้ — ต้อง clear:

```powershell
dotnet nuget locals all --clear
```

> Best practice: อัพ version ทุกครั้งที่ re-pack (ใช้ pre-release suffix เช่น `1.0.3-preview.1`) เพื่อไม่ต้อง clear cache ทุกครั้ง

### Step 5 — Remove source (ตอนไม่ใช้แล้ว)

```powershell
dotnet nuget remove source AuthMicroservice-Local
```

---

## 7. Re-pack workflow

หลังแก้ code แล้วจะ pack version ใหม่:

1. Edit `<Version>` ใน [Directory.Build.props](../Directory.Build.props) — ขึ้น patch/minor/major ตาม change
2. เคลียร์ output เก่า:
   ```powershell
   Remove-Item .\artifacts\*.nupkg
   ```
3. Pack:
   ```powershell
   dotnet pack AuthMicroservice.sln -c Release -o .\artifacts
   ```
4. ถ้า test consumer เคย install version เดิม cache ค้าง:
   ```powershell
   dotnet nuget locals all --clear
   ```

---

## 8. Troubleshooting

| Symptom | สาเหตุ / วิธีแก้ |
|---|---|
| `dotnet pack` ไม่ผลิต `.nupkg` ของ project ใด project หนึ่ง | Project นั้นไม่มี `<IsPackable>true</IsPackable>` — check `.csproj` (Api / Sample / tests จงใจไม่ pack) |
| `.nupkg` version ไม่ถูก install ตอน `dotnet add package` | NuGet cache — `dotnet nuget locals all --clear` แล้วลอง install ใหม่ |
| Version ใน `.nupkg` ไม่ตรงกับที่แก้ Directory.Build.props | มี `<Version>` override ใน `.csproj` รายตัว หรือ CLI มี `/p:Version=...` ทับ |
| Migration package ไม่ pull Core ตาม dependency | เปิด `.nuspec` ใน `.nupkg` — ต้องเห็น `<dependency id="Synergy.AuthMicroservice.Core" ...>` (generate จาก ProjectReference อัตโนมัติเมื่อ Core `IsPackable=true`) |
| `dotnet nuget add source` แจ้ง `Name already exists` | source ชื่อเดิมยังลงทะเบียนอยู่ — `dotnet nuget remove source AuthMicroservice-Local` ก่อน add ใหม่ |
| Pack ผ่านแต่ `.nupkg` ไม่มี `Templates/*.html` ของ Core | Check ว่า [Core.csproj](../src/AuthMicroservice.Core/AuthMicroservice.Core.csproj) มี `<EmbeddedResource Include="Templates\*.html" />` (มีอยู่แล้วโดย default) |
| Consumer install แล้ว build error `The type or namespace 'AuthMicroservice.Core.Extensions' could not be found` | อาจ install package version เก่าที่ยังไม่มี extension — check version ที่ install จริง (`dotnet list package`) |

---

## 9. Next steps (ยังไม่ทำใน guide นี้)

- **Publish ไป feed จริง** — `dotnet nuget push` ไป nuget.org / GitHub Packages / Azure Artifacts (จะเพิ่มเป็น section แยกภายหลัง)
- **CI auto-pack on tag** — GitHub Actions workflow ให้ auto pack + publish ตอน push tag `v*.*.*` (ยังไม่มี [.github/workflows/](../.github/workflows/))
- **Source Link** — ให้ consumer step through library code ตอน debug ได้ (ผ่าน `Microsoft.SourceLink.GitHub` + `PublishRepositoryUrl`)
