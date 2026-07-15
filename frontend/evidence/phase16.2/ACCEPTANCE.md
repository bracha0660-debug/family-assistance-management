# Phase 16.2 — Shared Assistance Item Edit Modal Layout — Acceptance

## Locked behavior
| Screen | Result |
|--------|--------|
| Create New Decision / first-entry wide | Unchanged (wide horizontal / `.modal-committee-expanded`) |
| Edit Item (Committee + Payments) desktop | Vertical one-column, ~640px wide |
| Edit Item mobile (390×844) | Vertical one-column, viewport-bounded |

## Implementation
- `AssistanceItemEditModal` uses `sizeClassName="modal-item-edit"` only (no `modal-committee-expanded`).
- CSS scoped under `.modal-item-edit`: `width: min(640px, calc(100vw - 32px))`, `max-height: calc(100dvh - 32px)`, `grid-template-columns: 1fr`, `--committee-items-min-width: 0`.
- Modal body is the only scroll owner; footer stays reachable.

## Evidence gates
- Create first-entry wide before/after pixel-identical when compared across runs with identical hosts.
- After: `edit_not_modal_committee_expanded`, `edit_vertical_one_column`, `edit_desktop_width_le_640`, scrollWidth asserts, Save/Cancel reachable.

## Build / tests
- `npm run build`
- `npx tsx --test src/validation/*.test.ts`
