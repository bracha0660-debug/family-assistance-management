# Family Assistance Management - Project Master State

**Status:** ACTIVE
**Baseline:** Step 15 Stabilized
**Verification Status:** PASS (13/13)
**Source of Truth:** docs/architecture.md + committed code
**Last Updated:** Step 15 Stabilized

---

# 1. Project Purpose

Family Assistance Management is a multi-tenant Hebrew RTL platform used by organizations to manage assistance programs for families.

The platform supports:

* Family management
* Supplier management
* Assistance approvals
* Committee workflows
* Payment processing
* Permissions and role-based access control
* Audit and traceability

The system is designed so that multiple independent organizations can operate within the same platform while maintaining strict data isolation.

---

# 2. Current Technology Stack

| Layer          | Technology                   |
| -------------- | ---------------------------- |
| Backend        | ASP.NET Core 8 Minimal API   |
| ORM            | Entity Framework Core        |
| Database       | PostgreSQL 16                |
| Frontend       | React 18                     |
| Language       | TypeScript                   |
| Build Tool     | Vite                         |
| Authentication | Session Cookie (FAM.Session) |
| Deployment     | Docker Compose               |

---

# 3. Current Git Baseline

## Stable Branch

```text
rollback/step5-assistance-requests
```

## Stable Tag

```text
step-15-stabilized
```

## Verification

```text
verify-step15.ps1
13 / 13 PASS
```

---

# 4. Implemented Functional Areas

## Foundation

Implemented and approved.

Includes:

* Authentication
* Session management
* Audit infrastructure
* Multi-tenant foundations
* Base schema

Status:

```text
APPROVED
```

---

## Super Admin

Implemented and approved.

Capabilities:

* Create organizations
* Suspend organizations
* Bootstrap organization administrator

Status:

```text
APPROVED
```

---

## Organization Administration

Implemented and approved.

Capabilities:

* User management
* Role assignment
* Activity log

Status:

```text
APPROVED
```

---

## Families

Implemented and approved.

Capabilities:

* Family creation
* Family editing
* Family deactivation
* Family restoration
* Coordinator assignment
* Accounting code support
* Embedded bank details

Family code format:

```text
F-NNNNNN
```

Status:

```text
APPROVED
```

---

## Assistance Types

Implemented and approved.

Capabilities:

* Create assistance types
* Update assistance types
* Activate/deactivate assistance types

Currency:

```text
ILS
```

Status:

```text
APPROVED
```

---

## Permissions Framework

Implemented.

Capabilities:

* Permission grants
* Permission scopes
* Permission-based navigation
* Organization-specific permissions

Status:

```text
IMPLEMENTED
```

---

## Family Card

Implemented.

Capabilities:

* Extended family profile
* Bank information
* Coordinator information
* Accounting information

Important:

```text
BankVerifiedExternally removed
```

Bank details are optional.

Status:

```text
IMPLEMENTED
```

---

## Suppliers

Implemented.

Capabilities:

* Supplier creation
* Supplier editing
* Supplier bank information
* Supplier payment targeting

Status:

```text
IMPLEMENTED
```

---

## Committee Decisions

Implemented.

Capabilities:

* Draft decisions
* Submit for approval
* Approve decisions
* Return for revision
* Suspend decisions
* Cancel decisions

Status:

```text
IMPLEMENTED
```

---

## Assistance Items

Implemented.

Capabilities:

* Multiple items per decision
* Assistance type selection
* Payment target selection
* Payment method selection
* Item editing
* Item urgency

Important:

```text
isUrgent is stored on AssistanceItem
```

Status:

```text
IMPLEMENTED
```

---

## Payment Queue

Implemented.

Capabilities:

* Payment generation
* Payment execution
* Payment tracking
* Proof management
* Queue visibility

Status:

```text
IMPLEMENTED
```

---

# 5. Roles

Current known roles:

## SuperAdmin

System-wide administration.

## OrgAdmin

Organization administration.

## Coordinator

Family and committee operations.

## Manager

Approval authority.

## Finance

Payment authority.

---

# 6. Payment Methods

Supported methods:

```text
bank_transfer
check
voucher
```

---

# 7. Payment Targets

Supported targets:

```text
family
supplier
other
```

---

# 8. Approved Workflow (Financial Committee Decisions)

Workflow root: `CommitteeDecision`. Does not include pre-committee assistance requests or family intake.

```text
draft
    ->
submitted
    ->
approved
    ->
awaiting_payment
    ->
paid
```

Additional states may exist for revision, suspension and cancellation.

---

# 9. Critical Business Rules

## Rule 1

Bank transfer requires complete bank information.

### Family

```text
paymentTarget = family
paymentMethod = bank_transfer
```

Requires:

```text
bankNumber
branchNumber
accountNumber
accountHolderName
```

If incomplete:

```text
HTTP 400
INCOMPLETE_BANK_DETAILS
```

---

### Supplier

```text
paymentTarget = supplier
paymentMethod = bank_transfer
```

Requires:

```text
bankNumber
branchNumber
accountNumber
accountHolderName
```

If incomplete:

```text
HTTP 400
INCOMPLETE_BANK_DETAILS
```

---

## Rule 2

Backend enforcement is mandatory.

Validation exists in:

```text
CommitteeDecisionService
```

---

## Rule 3

Execution layer also validates bank completeness.

Validation exists in:

```text
PaymentService
```

This is intentional defense-in-depth.

---

# 10. Verification Suite

Available verification scripts:

```text
verify-step01
verify-step02
verify-step03
verify-step04
verify-step04_1
verify-permissions-system
verify-family-card
verify-supplier-card
verify-step15
```

Current approved verification:

```text
verify-step15
13 / 13 PASS
```

---

# 11. Architecture Status

| Area                | Status      |
| ------------------- | ----------- |
| Foundation          | Approved    |
| Super Admin         | Approved    |
| Organization Admin  | Approved    |
| Families            | Approved    |
| Assistance Types    | Approved    |
| Permissions         | Implemented |
| Family Card         | Implemented |
| Suppliers           | Implemented |
| Committee Decisions | Implemented |
| Assistance Items    | Implemented |
| Payment Queue       | Implemented |

---

# 12. Known Technical Debt

## Documentation Synchronization

Status:

```text
RESOLVED
```

docs/architecture.md has been synchronized with the Step 15 stabilized baseline.

---

# 13. Deferred Areas

## Data Import

Status:

```text
DEFERRED
```

Not part of current approved scope.

---

# 14. Source Of Truth

When conflicts exist:

Priority order:

1. Committed code
2. docs/architecture.md
3. Verification scripts
4. Historical plans

---

# 15. Recommended Next Step

Before implementing additional features:

1. Maintain architecture.md as the authoritative architecture document.
2. Keep verification scripts aligned with business rules.
3. Preserve backend validation for all critical business rules.
4. Continue future development only from the Step 15 stabilized baseline.
