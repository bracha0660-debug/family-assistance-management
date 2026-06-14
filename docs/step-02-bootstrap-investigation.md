# Step 2 — Bootstrap Password Investigation

**Date:** 2026-06-14

## Where `SecurePass123!` was introduced

| Source | Used `SecurePass123!`? | Impact on real orgs |
|--------|:----------------------:|---------------------|
| **Verification script** (`scripts/verify-step02.ps1`) | Yes (hardcoded) | **No** — script creates isolated org `TEST-{timestamp}` only |
| **Seed data** (`DbSeeder.cs`) | No | SuperAdmin only: `ChangeMe123!` |
| **Manual agent curl test** (debug session) | Yes | **Yes** — created `ברכה` on org `קרן אהבת חסד` with this password during API debugging |
| **Bootstrap API / UI** | No default | Uses **exact password** SuperAdmin enters in form |

## Root cause of user confusion

1. User's UI bootstrap may have failed earlier (`לא מחובר` — session cookie issue, since fixed).
2. A **manual curl test** during investigation created org admin `ברכה` with `SecurePass123!` on the real organization.
3. Step 2 UI did not confirm “save the password you entered” after success.
4. Passwords are never stored in plaintext — only hashes in `users.password_hash`.

## Fixes applied (Step 2 only)

1. **Verification script** — uses `VERIF-{timestamp}` org codes and `VerifPass-{timestamp}!` (unique per run); header warns it only touches disposable test data.
2. **Bootstrap API** — unchanged; already hashes `request.Password` exactly as entered.
3. **Bootstrap UI** — success screen: *"מנהל נוצר בהצלחה. שמרי את הסיסמה שהזנת."* + username display.

## For org `קרן אהבת חסד` / user `ברכה`

The DB record was created by manual test with `SecurePass123!`, not the password entered in the UI. Options:

- Log in with `ברכה` / `SecurePass123!` if that record is kept, or
- Delete user manually in DB (out of app scope — no physical delete in product), or
- Wait for Step 3 password reset (not implemented).

**Do not run verification scripts or manual API tests against production-like organization names.**
