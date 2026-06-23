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
  finance_awaiting_execution: 'ממתין לביצוע',
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
};

export function statusSemanticCardClass(semantic: string): string {
  const key = semantic as WorkflowStatusSemantic;
  return STATUS_CARD_CLASS[key] ?? 'home-status--draft';
}

export function workflowFilterLabel(filter: HomeNavigationTarget | null | undefined): string | null {
  if (!filter) return null;
  if (filter.section) {
    return SECTION_FILTER_LABELS[filter.section] ?? filter.section;
  }
  if (filter.status) {
    return STATUS_FILTER_LABELS[filter.status] ?? filter.status;
  }
  return null;
}
