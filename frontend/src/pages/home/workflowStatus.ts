import type { HomeNavigationTarget } from '../../api/workflow';

/** Semantic workflow status identifiers — mirrors backend HomeWorkflowStatus. */
export type WorkflowStatusSemantic =
  | 'draft'
  | 'pending_approval'
  | 'returned_for_treatment'
  | 'on_hold'
  | 'pending_execution'
  | 'paid'
  | 'rejected';

const STATUS_CARD_CLASS: Record<WorkflowStatusSemantic, string> = {
  draft: 'home-status--draft',
  pending_approval: 'home-status--pending-approval',
  returned_for_treatment: 'home-status--returned',
  on_hold: 'home-status--on-hold',
  pending_execution: 'home-status--pending-execution',
  paid: 'home-status--paid',
  rejected: 'home-status--rejected',
};

const SECTION_FILTER_LABELS: Record<string, string> = {
  my_drafts: 'טיוטות',
  my_waiting_manager_approval: 'ממתין לאישור',
  my_returned_for_revision: 'הוחזר לטיפול',
  my_suspended: 'בהשהיה',
  waiting_my_approval: 'ממתין לאישור',
  manager_returned: 'הוחזר לטיפול',
  manager_suspended: 'בהשהיה',
  finance_awaiting_execution: 'ממתין לתשלום',
  finance_on_hold: 'בהשהיה',
  finance_executing: 'בביצוע',
  finance_proof_uploaded: 'הוכחה הועלתה',
  finance_paid: 'שולם',
  finance_returned: 'הוחזר לטיפול',
};

const STATUS_FILTER_LABELS: Record<string, string> = {
  draft: 'טיוטות',
  submitted: 'ממתין לאישור',
  returned_for_revision: 'הוחזר לטיפול',
  suspended: 'בהשהיה',
  approved: 'אושר',
  rejected: 'נדחה',
  cancelled: 'בוטל',
  partially_paid: 'שולם חלקית',
  fully_paid: 'שולם במלואו',
  awaiting_payment: 'ממתין לתשלום',
};

export function statusSemanticCardClass(semantic: string): string {
  const key = semantic as WorkflowStatusSemantic;
  return STATUS_CARD_CLASS[key] ?? 'home-status--draft';
}

const STATUS_SEMANTIC_LABELS: Record<WorkflowStatusSemantic, string> = {
  draft: 'טיוטה',
  pending_approval: 'ממתין לאישור',
  returned_for_treatment: 'הוחזר לטיפול',
  on_hold: 'בהשהיה',
  pending_execution: 'ממתין לתשלום',
  paid: 'שולם',
  rejected: 'נדחה',
};

export function statusSemanticLabel(semantic: string): string {
  const key = semantic as WorkflowStatusSemantic;
  return STATUS_SEMANTIC_LABELS[key] ?? semantic;
}

export function workflowFilterLabel(filter: HomeNavigationTarget | null | undefined): string | null {
  if (!filter) return null;
  let base: string | null = null;
  if (filter.section) {
    base = SECTION_FILTER_LABELS[filter.section] ?? filter.section;
  } else if (filter.status) {
    base = STATUS_FILTER_LABELS[filter.status] ?? filter.status;
  }
  if (!base) return null;
  if (filter.minAgeDays && filter.minAgeDays > 0) {
    return `${base} · מעל ${filter.minAgeDays} ימים`;
  }
  return base;
}

export function isPendingPaymentFilter(filter: HomeNavigationTarget | null | undefined): boolean {
  if (!filter) return false;
  return filter.section === 'finance_awaiting_execution' || filter.status === 'awaiting_payment';
}
