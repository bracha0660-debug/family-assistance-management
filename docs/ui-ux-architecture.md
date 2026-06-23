# Family Assistance Management — UI/UX Architecture Specification

Version 1.0

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

- **Backend** communicates workflow meaning via `statusSemantic` identifiers.
- **Frontend** maps semantics to colors, icons, and layout via CSS tokens in `workflowStatus.ts`.

## Visual style

- Clean layouts, large whitespace, soft shadows, rounded corners
- Clear hierarchy, minimal visual noise
- Modern SaaS — not governmental, not legacy enterprise

## Logo behavior

Organization logo in sidebar (RTL: top area). Clicking logo navigates to Home Dashboard from every screen.

## Status design language

| Semantic | Hex | Meaning |
|----------|-----|---------|
| draft | `#3B82F6` | Draft |
| pending_approval | `#F59E0B` | Pending approval |
| returned_for_treatment | `#0EA5A4` | Returned for treatment |
| on_hold | `#8B5CF6` | On hold |
| pending_execution | `#6366F1` | Pending execution |
| paid | `#22C55E` | Paid |
| rejected | `#EF4444` | Rejected |

Status must always include text — do not rely on color alone.

## Action buttons

Primary: blue. Positive: green. Warning: orange. Danger: red. Neutral: gray.

## Reusable components

Prefer shared primitives: `.home-widget`, `.home-panel`, `.home-metric`, `.home-grid-3`, status classes `.home-status--*`.

## Accessibility

High contrast, readable typography, large click targets, consistent spacing.

## Guiding principle

Clarity over creativity. Usability over decoration. Consistency over novelty.
