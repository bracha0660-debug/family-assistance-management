# Step 1 — Architecture Compliance Report

## Database Schema (7 tables)

| Table | Versioned | Step 1 Active |
|-------|:---------:|:-------------:|
| `organizations` | Yes | Schema only |
| `users` | Yes | Seed SuperAdmin |
| `user_sessions` | No | Yes |
| `bank_accounts` | Yes | Schema only |
| `bank_account_history` | No | Schema only |
| `audit_logs` | No | Interface only |
| `security_audit_logs` | No | **Yes — writes** |

## Bank Duplicate Rule

```sql
UNIQUE (organization_id, bank_number, branch_number, account_number) WHERE is_active = true
```

Account number alone is **not** used for duplicate detection.

## API Endpoints (Step 1)

| Method | Path | Auth |
|--------|------|------|
| POST | `/api/v1/auth/login` | No |
| POST | `/api/v1/auth/logout` | Yes |
| GET | `/api/v1/auth/me` | Yes |
| GET | `/api/v1/health` | No |

## Security Audit Verification

| Code | Event | Written on |
|------|-------|------------|
| SEC-001 | login_success | 200 login |
| SEC-002 | login_failed_invalid_credentials | 401 |
| SEC-003 | login_failed_account_inactive | 403 |
| SEC-004 | login_failed_rate_limited | 429 |
| SEC-005 | logout | 204 |

Each record includes: `username_attempted`, `ip_address`, `user_agent`, `created_at`.

Failure to write security audit → HTTP 500 (auth not completed silently).

## Business Audit (AUD-001 – AUD-021)

Interface registered (`IAuditService`). No business events fired in Step 1.

## Compliance Checklist

- [x] ASP.NET Core backend
- [x] React + TypeScript frontend
- [x] PostgreSQL with EF Core migration
- [x] Server-side cookie session (`FAM.Session`)
- [x] SuperAdmin seed from env var
- [x] Multi-org schema foundation
- [x] Bank accounts separate table (no inline bank fields)
- [x] Security audit table (not app log only)
- [x] Material change reason policy in `AuditService`
- [x] Version column on editable entities
- [x] Docker Compose (postgres + api + web)
- [x] No Step 2 features

## Migration Scripts

- EF Core: `backend/FamilyAssistance.Api/Migrations/20240611000000_InitialCreate.cs`
- SQL reference: `backend/migrations/001_initial.sql`
