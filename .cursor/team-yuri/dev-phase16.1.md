# Developer Phase 16.1

## Phase Identifier
PHASE=16.1

## Status
STATUS: BLOCKED — **M161-1 → M161-7 and M161-9 complete**. Repair is **merged to `main`** (PR #2 → `6398b92`). **M161-8 production Render evidence still incomplete**: no Render API key / service id / production API URL / backup confirmation available in this environment. Phase 16.1 **not** marked complete. Phase 17 bulk actions **not** opened. `PHASE=16.1`.

## Source References

- [`.cursor/team-yuri/PHASE.md`](PHASE.md) — `PHASE=16.1`
- [`.cursor/team-yuri/arch-phase16.1.md`](arch-phase16.1.md) — Production Database Schema Repair
- [`.cursor/team-yuri/manager-phase16.1.md`](manager-phase16.1.md) — READY_FOR_DEVELOPER (M161-1 → M161-9)
- [`.cursor/team-yuri/family-assistance-arch-plan.md`](family-assistance-arch-plan.md) — Phase 17 bulk actions **NOT APPROVED**

## Implementation Summary

Phase 16.1 production schema repair:

1. **Repair migration** `20260714000000_RepairMissingAssistanceItemTransferColumns` with guarded `ADD COLUMN IF NOT EXISTS` for the three transfer columns; forward-only `Down`; stub Designer discovery metadata; **no** snapshot rebuild; **no** edit of `20260629000000_AddAssistanceItemTransferBank`; **no** `__EFMigrationsHistory` surgery.
2. **Startup column-contract verification** via `information_schema.columns` (type / length / nullability) after migrate + required-table checks.
3. **Production ban** on `EnsureCreatedAsync` (missing tables fail startup in Production; Development/Test retain isolated fallback).
4. **Structured logging** for applied/pending counts, applied ids, table + column verify success, and the required success message.
5. PostgreSQL scenarios **A–E** exercised against throwaway DB `fam_p161`; local API auth `/me` + dashboard **2xx**; full automated suite **121** passed.

## Implemented Milestones

| Milestone | Completed: Yes/No | Notes |
|---|---:|---|
| M161-1 — Repair migration + EF discovery | **Yes** | Exact id; guarded SQL; forward-only Down; stub Designer; original untouched; no snapshot rebuild |
| M161-2 — Startup column-contract verification | **Yes** | `DbSeeder.VerifyRequiredColumnContractsAsync` via `information_schema.columns` |
| M161-3 — Production EnsureCreated guard | **Yes** | `AllowEnsureCreatedFallback` false in Production; missing tables throw; local Prod-env probe PASS |
| M161-4 — Structured logging | **Yes** | Counts, ids, table/column success; success message present in API logs |
| M161-5 — PostgreSQL scenarios A–E | **Yes** | All PASS on `fam_p161` (evidence below) |
| M161-6 — Build + automated tests | **Yes** | Docker API image build; tests **121** passed (incl. 6 Phase 16.1) |
| M161-7 — Authenticated smoke | **Yes** | `GET /api/v1/auth/me` 200; `GET /api/v1/org/workflow/dashboard` 200; no `42703` |
| M161-8 — Render production evidence | **Partial** | Merged to `main` (`6398b92`). Still blocked on verified prod backup + Render deploy logs + prod auth/dashboard smoke |
| M161-9 — Developer evidence packet | **Yes** | This document (§14 items 1–14) |

---

## §14 Developer Evidence Packet

### 1. Migration identifier

`20260714000000_RepairMissingAssistanceItemTransferColumns`

### 2. Migration file path

`backend/FamilyAssistance.Api/Migrations/20260714000000_RepairMissingAssistanceItemTransferColumns.cs`

### 3. Designer / discovery-metadata file path

`backend/FamilyAssistance.Api/Migrations/20260714000000_RepairMissingAssistanceItemTransferColumns.Designer.cs`

Attributes:

```csharp
[DbContext(typeof(AppDbContext))]
[Migration("20260714000000_RepairMissingAssistanceItemTransferColumns")]
```

### 4. Startup verification implementation file list

| File | Role |
|---|---|
| `backend/FamilyAssistance.Api/Data/DbSeeder.cs` | Migrate → required tables → column contracts → logging; Production EnsureCreated gate |
| `backend/FamilyAssistance.Api/Program.cs` | Passes `app.Environment` into `DbSeeder.SeedAsync` |
| `backend/FamilyAssistance.Api/FamilyAssistance.Api.csproj` | `InternalsVisibleTo` for unit tests of internal helpers |
| `backend/FamilyAssistance.Api.Tests/SchemaRepairPhase161Tests.cs` | Guard, contract match, mismatch formatting, migration SQL/Down tests |

### 5. Production `EnsureCreatedAsync` guard evidence

**Code path** (`DbSeeder`):

- `AllowEnsureCreatedFallback(environmentName)` returns `false` for Production.
- When required tables are missing and fallback is disallowed → throws  
  `Database schema incomplete in Production. Missing tables: …. EnsureCreatedAsync is not permitted in Production.`
- `EnsureCreatedAsync` only runs in non-Production after an explicit warning log.

**Unit tests:** `AllowEnsureCreatedFallback_IsFalse_InProduction` / `_IsTrue_OutsideProduction` — PASS.

**Runtime probe (throwaway DB `fam_p161`, `ASPNETCORE_ENVIRONMENT=Production`, table `assistance_items` renamed away):**

```text
Unhandled exception. System.InvalidOperationException: Database schema incomplete in Production. Missing tables: assistance_items. EnsureCreatedAsync is not permitted in Production.
```

No `EnsureCreated returned` log on that path. **Prod guard PASS: True**.

### 6. API build output

```text
Command: docker compose build api
Result: PASS — Image family-assistance-management-api Built (exit 0)
```

Also: `docker build -f backend/Dockerfile.test -t fam-api-tests backend` — PASS (exit 0).

### 7. Test output

```text
Command: docker run --rm fam-api-tests dotnet test --verbosity quiet
Result: PASS — Failed: 0, Passed: 121, Skipped: 0, Total: 121, Duration: ~1s
```

Phase 16.1 filter:

```text
Command: docker run --rm fam-api-tests dotnet test --filter "FullyQualifiedName~SchemaRepairPhase161Tests" --verbosity quiet
Result: PASS — Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

Lint: **NOT AVAILABLE** as a separate project lint command in this repo (no dedicated lint script for API). No new compile errors; existing CS1998 warning in `PermissionService.cs` unchanged / pre-existing.

### 8. PostgreSQL results for scenarios A–E

All scenarios run against throwaway database `fam_p161` (cloned from local `family_assistance` before repair application), using one-shot containers of `family-assistance-management-api`.

| Scenario | Initial state | Result | Evidence summary |
|---|---|---|---|
| **A — All columns missing** | Dropped all three `transfer_*`; repair history row absent | **PASS** | `23 applied, 1 pending` → applied repair → columns created → `required tables and required column contracts are valid.` |
| **B — Partial columns present** | Only `transfer_bank_number` present | **PASS** | Repair added missing two; all three contracts match; existing bank column preserved |
| **C — All columns present** | All three correct; repair history deleted to re-apply | **PASS** | Idempotent `IF NOT EXISTS`; history returns to 24; contracts valid; no duplicates |
| **D — Incorrect definition** | `transfer_account_number` recreated as `varchar(5)` | **PASS** | Startup failed: `Column contract mismatch for public.assistance_items.transfer_account_number. Expected: character varying(34) nullable=YES. Detected: character varying(5) nullable=YES.` No `Application started` |
| **E — History drift** | Original `20260629000000_AddAssistanceItemTransferBank` present; physical columns missing; repair absent | **PASS** | Before: history 23, only original Transfer id, 0 transfer columns. After: history 24, original + repair rows, all three columns with exact contracts |

### 9. Migration-history before-and-after evidence

**Main DB `family_assistance` (first apply of repair via API recreate):**

| Moment | History count | Repair row | Notes |
|---|---:|---|---|
| Before | 23 | absent | Original TransferBank present; columns already correct |
| After first apply | 24 | present once | Log: `23 applied, 1 pending` → `Applying migrations: 20260714000000_RepairMissingAssistanceItemTransferColumns` |
| After re-run | 24 | present once | Log: `24 applied, 0 pending`; re-run safe |

**Scenario E (`fam_p161`):**

```text
E before:
23
20260629000000_AddAssistanceItemTransferBank
0   (transfer columns)

E after:
24
20260629000000_AddAssistanceItemTransferBank
20260714000000_RepairMissingAssistanceItemTransferColumns
transfer_account_number|34|YES
transfer_bank_number|10|YES
transfer_branch_number|10|YES
```

Previous history rows retained; exactly one new repair history row added via normal EF migrate (no manual history surgery).

### 10. Render deployment commit identifier

| Field | Value |
|---|---|
| Branch | `fix/phase16.1-production-schema-repair` (not pushed to `main` directly) |
| PR | https://github.com/bracha0660-debug/family-assistance-management/pull/2 |
| Feature commit | `44c8e3ad5f90eb54a2cb0af1354ccbf48fb92285` |
| Merged to `main` | **Yes** — merge commit `6398b92a8ab8b200f638d7527676a16481ddd2c8` |
| Render deploy of exact merged commit | **NOT VERIFIED** — no Render API credentials / service id / deploy hook in this environment |
| Verified production DB backup before deploy | **NOT CONFIRMED** — requires operator confirmation in Render / Postgres backup store |

**Remaining for M161-8 close:** confirm backup → deploy `6398b92` (or current `main` tip containing it) on Render → capture logs → prod smoke.

### 11. Render migration logs

**NOT CAPTURED YET** (Render access missing). Auto-deploy may occur from `main` if configured; cannot be observed here.

Local equivalent (Compose API after repair) for readiness reference:

```text
Database migrations: 23 applied, 1 pending
Applying migrations: 20260714000000_RepairMissingAssistanceItemTransferColumns
Applying migration '20260714000000_RepairMissingAssistanceItemTransferColumns'.
ADD COLUMN IF NOT EXISTS transfer_account_number character varying(34) NULL;
ADD COLUMN IF NOT EXISTS transfer_bank_number character varying(10) NULL;
ADD COLUMN IF NOT EXISTS transfer_branch_number character varying(10) NULL;
Database schema verified: all 15 required tables exist
Database schema verified: 3 required column contracts match
Database schema verified: required tables and required column contracts are valid.
```

Re-run:

```text
Database migrations: 24 applied, 0 pending
Database schema verified: required tables and required column contracts are valid.
```

No `EnsureCreated` on healthy Production-intended path when tables exist.

### 12. Authenticated dashboard recovery result

Local API (`http://localhost:8080`) after repair:

| Step | Result |
|---|---|
| `POST /api/v1/auth/login` (superadmin) | **200** |
| `POST /api/v1/admin/organizations/{id}/enter` | **200** |
| `GET /api/v1/auth/me` | **200** (acting org set) |
| `GET /api/v1/org/workflow/dashboard` | **200** |
| PostgreSQL `42703` / `transfer_account_number does not exist` in recent API logs | **0 hits** |

### 13. Complete list of changed files

| File | Change |
|---|---|
| `backend/FamilyAssistance.Api/Migrations/20260714000000_RepairMissingAssistanceItemTransferColumns.cs` | **Added** — guarded repair Up; forward-only Down |
| `backend/FamilyAssistance.Api/Migrations/20260714000000_RepairMissingAssistanceItemTransferColumns.Designer.cs` | **Added** — stub EF discovery metadata |
| `backend/FamilyAssistance.Api/Data/DbSeeder.cs` | **Modified** — column contracts, logging, Production EnsureCreated gate, env parameter |
| `backend/FamilyAssistance.Api/Program.cs` | **Modified** — pass `app.Environment` to seeder |
| `backend/FamilyAssistance.Api/FamilyAssistance.Api.csproj` | **Modified** — `InternalsVisibleTo` for tests |
| `backend/FamilyAssistance.Api.Tests/SchemaRepairPhase161Tests.cs` | **Added** — Phase 16.1 unit tests |
| `.cursor/team-yuri/dev-phase16.1.md` | **Added** — this evidence packet |

**Not changed:** any `frontend/**` file; `AppDbContextModelSnapshot.cs`; `20260629000000_AddAssistanceItemTransferBank.cs`.

### 14. Explicit confirmation — original migration not modified

**Confirmed:** `backend/FamilyAssistance.Api/Migrations/20260629000000_AddAssistanceItemTransferBank.cs` was **not** modified.

- `git diff -- backend/FamilyAssistance.Api/Migrations/20260629000000_AddAssistanceItemTransferBank.cs` → empty
- File not listed in `git status` changes
- `AppDbContextModelSnapshot.cs` also unmodified (no full snapshot rebuild)

---

## Functional Testability Evidence

| Field | Value |
|---|---|
| Method | API + CLI (local Docker Compose PostgreSQL + API) |
| Steps | 1) Rebuild/recreate API with repair migration 2) Observe migrate + schema verify logs 3) Login as superadmin 4) Enter org 5) `GET /api/v1/auth/me` 6) `GET /api/v1/org/workflow/dashboard` |
| Expected Result | Repair applies once; contracts valid; auth me success; dashboard HTTP 2xx; no `42703` |
| Actual Result | **PASS** (local). **Merged to main (`6398b92`)**. **Render production not yet verified** |
| Notes | Infrastructure-primary with dashboard recovery functional outcome. Phase 16.1 not closed; Phase 17 not started. |

## Documentation Update Evidence

| Field | Value |
|---|---|
| Documentation Updated | YES |
| Files Updated | `.cursor/team-yuri/dev-phase16.1.md` |
| Reason if Not Required | — |

## Dependencies Installed

| Dependency / Tool | Command Used | Reason |
|---|---|---|
| Docker SDK test image | `docker build -f backend/Dockerfile.test -t fam-api-tests backend` | Build/test without local `dotnet` on PATH |
| Docker Compose API image | `docker compose build api` / `up -d api --force-recreate` | Apply migration + smoke |
| Throwaway DB `fam_p161` | `CREATE DATABASE fam_p161 TEMPLATE family_assistance` | Isolated A–E scenarios |

## Unit Tests

| Field | Value |
|---|---|
| Command | `docker run --rm fam-api-tests dotnet test --verbosity quiet` |
| Result | **PASS** |
| Notes | 121 total; 6 Phase 16.1-specific |

## Lint

| Field | Value |
|---|---|
| Command | N/A (no dedicated API lint pipeline) |
| Result | **NOT AVAILABLE** |
| Notes | Compile succeeded in Docker test/build images; no new errors |

## Known Issues / Limitations

1. **M161-8 Render evidence incomplete** after merge: need verified production backup confirmation, Render deploy of `6398b92`, migration/schema logs, and production `/auth/me` + dashboard smoke with no `42703`.
2. Scenario D / Production-guard one-shot containers may still show `Running=true` briefly while crashing (race with `docker inspect`); failure is proven by exception logs and absence of ready message.
3. Local dashboard JSON titles may show mojibake in PowerShell console encoding; HTTP status **200** is the acceptance signal.

## Scope Compliance

- Phase 16.1 only — production schema repair
- No frontend changes
- No Phase 17 bulk actions
- Original transfer-bank migration untouched
- No `__EFMigrationsHistory` surgery
- No snapshot rebuild
- No secrets in logs/evidence (connection passwords only in local docker-compose config, not pasted into evidence beyond redacted host/db names)

## Developer Declaration

Sarah (Developer): M161-1–M161-7 implemented and evidenced. Repair committed on `fix/phase16.1-production-schema-repair`, PR #2 merged to `main` as `6398b92`. `PHASE` kept at **16.1**. Phase 16.1 **not** declared complete. Phase 17 **not** opened. **M161-8 remains blocked** until Render backup + deploy logs + production smoke are supplied/captured.

```text
Detected phase: 16.1
Selected state: 100 IMPLEMENT
Status: BLOCKED
```
