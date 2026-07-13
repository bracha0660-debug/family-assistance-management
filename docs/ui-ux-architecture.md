# Family Assistance Management — UI/UX Architecture Specification

Version 1.1

This document is the **design authority** for the Home Dashboard and related UI work. Consistency is preferred over innovation; screen-specific improvements are allowed when they improve usability.

## Design philosophy

Professional operational platform — not a public website, marketing product, or CRM.

Every screen should answer:

- What requires attention?
- What requires action?
- What is currently blocked?
- What is the current financial situation?

The interface should feel: professional, clean, calm, efficient, trustworthy.

Avoid: playful design, excessive colors, decorative elements, visual clutter.

## Application language

Use process-oriented workflow language. **Do not use "שלי"** or personal ownership wording.

Prefer: ממתין לאישור, ממתין לביצוע, הוחזר לטיפול, בהשהיה, טיוטות.

## Dashboard architecture

The Home Dashboard is the primary landing page. It provides **visibility and navigation** — not workflow actions. Actions remain on workflow screens (החלטות ועדה, תשלומים).

### Sections

1. **Operational KPIs** — טיוטות, ממתין לאישור, הוחזר לטיפול, בהשהיה, ממתין לביצוע
2. **Financial snapshot** — אושר החודש, שולם החודש, ממתין לביצוע, בהשהיה (ILS)
3. **Bottlenecks** — operational delays (7 / 30 / 14 day thresholds)
4. **Trends** — simple monthly trends
5. **Recent activity** — latest workflow events

### Visibility

Permission-driven only. Never branch on role names. Adapt when effective permissions change.

### Semantic status vs presentation

Business meaning and visual presentation MUST remain decoupled.

| Layer | Responsibility |
|-------|----------------|
| **Backend** | Communicates workflow meaning via `statusSemantic` identifiers only. SHALL NOT return color names, CSS classes, hexadecimal values, or icon names. |
| **Frontend** | Maps `statusSemantic` → centralized design tokens, centralized icons, Hebrew status text, and reusable visual variants. |

Central mapping location: `frontend/src/pages/home/workflowStatus.ts` (or a future shared status-design module only if this architecture document later relocates it).

**Do not create per-screen status color or icon mappings.** All surfaces that render `statusSemantic` reuse the same mapping module.

## Visual style

- Clean layouts, large whitespace, soft shadows, rounded corners
- Clear hierarchy, minimal visual noise
- Modern SaaS — not governmental, not legacy enterprise

## Logo behavior

Organization logo in sidebar (RTL: top area). Clicking logo navigates to Home Dashboard from every screen.

## Status design language

### Non-success semantics

| Semantic | Hex | Meaning |
|----------|-----|---------|
| draft | `#3B82F6` | Draft |
| pending_approval | `#F59E0B` | Pending approval |
| returned_for_treatment | `#0EA5A4` | Returned for treatment |
| on_hold | `#8B5CF6` | On hold |
| pending_execution | `#6366F1` | Pending execution |
| rejected | `#EF4444` | Rejected |

### Successful-process family

The statuses `approved`, `paid`, and `completed` belong to the same successful-process color family, but SHALL be visually distinguishable through stronger contrast.

| Status semantic | Hebrew label | Visual role |
|-----------------|--------------|-------------|
| `approved` | אושר | Light success |
| `paid` | שולם | Medium success |
| `completed` | תהליך הושלם | Dark success |

Exact implementation SHALL use centralized CSS tokens (background + foreground pairs). Do not use the same foreground and background colors for all three statuses. Production hex values for the stepped success family are defined in CSS tokens at implementation time — not as a single shared green for all three.

Status must always include text — do not rely on color alone.

### Successful workflow contrast

Successful workflow states SHALL use distinct contrast levels.

**approved**
- light success background
- dark-green text
- existing approval/check icon
- visually lighter than paid

**paid**
- medium success background
- stronger green foreground
- existing payment icon
- visually stronger than approved

**completed**
- dark success background or dark success badge
- white or very light foreground
- existing completion/check-circle icon
- visually represents the final successful state

Known gap: home-screen cards for approved and paid are currently too visually similar. Future token work MUST fix this via the stepped contrast family above.

### System-wide applicability

This status design language applies to the Home Dashboard and all future workflow surfaces (lists, badges, financial metrics, activity pills) that render `statusSemantic`, always via the single frontend mapping module.

## Action buttons

Primary: blue. Positive: green. Warning: orange. Danger: red. Neutral: gray.

## Reusable components

Prefer shared primitives: `.home-widget`, `.home-panel`, `.home-metric`, `.home-grid-3`, status classes `.home-status--*`.

## Accessibility

High contrast, readable typography, large click targets, consistent spacing.

## Guiding principle

Clarity over creativity. Usability over decoration. Consistency over novelty.
