/** Hebrew labels for workflow action keys (Phase 15 arch table). */
export function workflowActionLabel(action: string): string {
  const labels: Record<string, string> = {
    edit: 'עריכה',
    submit: 'הגש לאישור',
    approve: 'אישור',
    reject: 'דחייה',
    return: 'החזרה לתיקון',
    suspend: 'השהיה',
    resubmit: 'הגש מחדש',
    send_to_execution: 'העבר לביצוע',
    enter_reference: 'הזן אסמכתא',
    complete: 'סיים תהליך',
    cancel: 'ביטול',
    adjust_amount: 'עריכה',
    view_history: 'היסטוריה',
    cancel_export_item: 'בטל ייצוא לפריט',
    cancel_batch: 'בטל גליון ייצוא',
    download: 'הורדה',
    execute: 'ביצוע',
    upload_proof: 'העלאת הוכחה',
    mark_paid: 'סימון שולם',
    return_to_coordinator: 'החזרה לרכז',
  };
  return labels[action] ?? action;
}

/** Hebrew labels for assistance item statuses (Phase 15/16). */
export function assistanceItemStatusLabel(status: string): string {
  const labels: Record<string, string> = {
    submitted: 'הוגש לאישור',
    returned: 'הוחזר לתיקון',
    approved: 'אושר',
    suspended: 'מושהה',
    rejected: 'נדחה',
    waiting_for_reference: 'בביצוע',
    paid: 'שולם',
    completed: 'תהליך הושלם',
  };
  return labels[status] ?? status;
}

export function workflowActionButtonClass(action: string): string {
  switch (action) {
    case 'suspend':
      return 'btn-small btn-action-on-hold';
    case 'return':
    case 'return_to_coordinator':
      return 'btn-small btn-action-returned';
    case 'reject':
      return 'btn-small btn-action-rejected';
    case 'approve':
      return 'btn-small btn-action-approved';
    case 'enter_reference':
    case 'mark_paid':
      return 'btn-small btn-action-paid';
    case 'complete':
      return 'btn-small btn-action-completed';
    case 'send_to_execution':
    case 'execute':
      return 'btn-small btn-action-pending-execution';
    case 'submit':
    case 'resubmit':
      return 'btn-small btn-action-pending-approval';
    case 'edit':
    case 'adjust_amount':
      return 'btn-small btn-action-draft';
    case 'cancel':
    case 'cancel_export_item':
    case 'cancel_batch':
      return 'btn-small btn-action-cancel';
    case 'download':
    case 'upload_proof':
      return 'btn-small btn-action-neutral';
    default:
      return 'btn-small btn-action-neutral';
  }
}

/** Decisions Table 2 — payment execution actions live on PaymentsQueuePage (Phase 16). */
export const DECISIONS_ITEM_ACTIONS = new Set([
  'edit',
  'approve',
  'reject',
  'return',
  'suspend',
  'resubmit',
  'complete',
]);

/** Filter to backend availableActions only — never infer from status; never invent restore. */
export function decisionsItemActions(actions: string[] | undefined | null): string[] {
  return (actions ?? []).filter((action) => DECISIONS_ITEM_ACTIONS.has(action));
}
