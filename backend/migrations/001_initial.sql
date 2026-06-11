-- Step 1 Foundation — Initial schema (reference script)
-- Applied automatically via EF Core migration 20240611000000_InitialCreate

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Tables: organizations, users, user_sessions, bank_accounts,
--         bank_account_history, audit_logs, security_audit_logs

-- Bank duplicate rule (active accounts only):
-- UNIQUE (organization_id, bank_number, branch_number, account_number)
