# Phase 16.2 — Shared Assistance Item Edit Modal Layout — Acceptance

## Result
| Screen | Result |
|--------|--------|
| Create New Decision (small modal) | Unchanged intent; before/after PNGs for review (hash differs due to seeded org content) |
| Create Decision first-entry wide (`.modal-committee-expanded` without `.modal-item-edit`) | **Pixel-identical** before/after (`create_regression_createFirstEntryWide_pixel_identical`) |
| Edit Item — Committee Decisions | Improved wide layout |
| Edit Item — Payments Queue | Improved identical wide layout |

## Regression confirmations
1. Create first-entry wide screenshots: same SHA-256 before/after.
2. No new CSS rule matches `.modal-committee-expanded` without requiring `.modal-item-edit` (append-only `.modal-item-edit` block).
3. `CommitteeDecisionsPage` / `PaymentsQueuePage` not modified.
4. Only `AssistanceItemEditModal` receives `modal-item-edit`.

## Layout asserts (after, 1366×768)
- Edit modal has `modal-item-edit` + `modal-committee-expanded`
- Body is scroll owner (`overflow-y: auto`); form/shell are not
- `scrollWidth <= clientWidth` for body and form
- Save/Cancel reachable
- 390×844 one-column screenshot captured

## Build / tests
- `npm run build` — pass
- `npx tsx --test src/validation/*.test.ts` — 3 pass

## Evidence files
See PNGs and `*-report.json` in this folder.

## Credentials
Capture script uses runtime-generated users; SuperAdmin password only via env `SUPERADMIN_PASSWORD` (default local `ChangeMe123!`). No credentials written into evidence PNGs/JSON beyond what the live UI shows (usernames in nav).
