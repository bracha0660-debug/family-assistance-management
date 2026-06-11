# Family Assistance Management — Step 1 Foundation

Platform skeleton: authentication, security audit, multi-tenant schema foundation.

## Stack

- **Backend:** ASP.NET Core 8
- **Frontend:** React + TypeScript (Vite)
- **Database:** PostgreSQL 16
- **Deploy:** Docker Compose

## Quick Start

```bash
docker compose up --build
```

| Service | URL |
|---------|-----|
| Web (login) | http://localhost:3000 |
| API | http://localhost:8080 |
| Health | http://localhost:8080/api/v1/health |

## Default SuperAdmin

| Field | Value |
|-------|-------|
| Username | `superadmin` |
| Password | `ChangeMe123!` (override via `SUPERADMIN_INITIAL_PASSWORD`) |

## Local Frontend Dev

```bash
cd frontend
npm install
npm run dev
```

API proxy: `http://localhost:5173` → `http://localhost:8080`

## Step 1 Scope

- Login / logout / session (`FAM.Session` cookie)
- Security audit (SEC-001 – SEC-005)
- 7-table schema (organizations, users, sessions, bank accounts, audit)
- Hebrew RTL login page

**Not included:** Step 2+ (organizations CRUD, users, families, etc.)
