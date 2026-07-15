# Phase 16.3 Stage 2 — Documented debt

**Status:** Open. Phase 16.3 must not be marked CLOSED until this is implemented or explicitly accepted as permanent debt.

## Gap

Suspending an organization must revoke or invalidate SuperAdmin sessions that are currently acting inside that organization (`ActingOrganizationId` set to the suspended org).

Without Stage 2, a SuperAdmin who entered an org before suspend may retain an in-session acting context until idle timeout / logout / exit.

## Out of scope for Stage 1

Stage 1 (ownership bypass for SuperAdmin-in-org) does not implement session invalidation on suspend.
