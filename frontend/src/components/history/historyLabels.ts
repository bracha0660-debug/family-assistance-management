/**
 * Approved Hebrew display labels for AssistanceItem history (§5.3.1 / §20).
 * Never expose technical keys in the UI.
 */

const FIELD_LABELS_HE: Record<string, string> = {
  amount: 'סכום לתשלום',
  PaymentAmount: 'סכום לתשלום',
  payment_amount: 'סכום לתשלום',
  supplier_id: 'ספק',
  SupplierId: 'ספק',
  assistance_type_id: 'סוג סיוע',
  AssistanceTypeId: 'סוג סיוע',
  account_number: 'מספר חשבון',
  AccountNumber: 'מספר חשבון',
  payment_method: 'אמצעי תשלום',
  PaymentMethod: 'אמצעי תשלום',
  payment_target: 'יעד תשלום',
  PaymentTarget: 'יעד תשלום',
  beneficiary: 'מוטב',
  Beneficiary: 'מוטב',
  description: 'תיאור',
  Description: 'תיאור',
  workflow_status: 'סטטוס',
  WorkflowStatus: 'סטטוס',
  status: 'סטטוס',
  bank_number: 'מספר בנק',
  branch_number: 'מספר סניף',
  account_holder_name: 'שם בעל החשבון',
};

const EVENT_LABELS_HE: Record<string, string> = {
  item_created: 'נוצר פריט סיוע',
  submitted: 'הוגש לאישור',
  resubmitted: 'הוגש מחדש',
  approved: 'אושר',
  rejected: 'נדחה',
  returned: 'הוחזר לתיקון',
  suspended: 'מושהה',
  item_edited: 'עריכת פריט',
  export_batch_created: 'נוצר גליון ייצוא',
  export_item_cancelled: 'בוטל ייצוא לפריט',
  export_batch_cancelled: 'בוטל גליון ייצוא',
  reference_entered: 'הוזנה אסמכתא',
  marked_paid: 'סומן כשולם',
  process_completed: 'תהליך הושלם',
  amount_changed: 'שינוי סכום',
  supplier_changed: 'שינוי ספק',
  status_changed: 'שינוי סטטוס',
};

const STATUS_VALUE_HE: Record<string, string> = {
  draft: 'טיוטה',
  submitted: 'הוגש לאישור',
  returned: 'הוחזר לתיקון',
  approved: 'אושר',
  suspended: 'מושהה',
  rejected: 'נדחה',
  waiting_for_reference: 'בביצוע',
  paid: 'שולם',
  completed: 'תהליך הושלם',
};

/** Returns Hebrew field label, or null when the key has no approved mapping. */
export function historyFieldLabelHe(fieldKey: string, backendLabelHe?: string | null): string | null {
  if (fieldKey && FIELD_LABELS_HE[fieldKey]) {
    return FIELD_LABELS_HE[fieldKey];
  }
  const trimmed = (backendLabelHe ?? '').trim();
  if (trimmed && !looksTechnical(trimmed)) {
    return trimmed;
  }
  return null;
}

export function historyEventLabelHe(eventType: string, backendDescriptionHe?: string | null): string | null {
  if (eventType && EVENT_LABELS_HE[eventType]) {
    return EVENT_LABELS_HE[eventType];
  }
  const trimmed = (backendDescriptionHe ?? '').trim();
  if (trimmed && !looksTechnical(trimmed)) {
    return trimmed;
  }
  return null;
}

/** Translate workflow status enums inside transition values; leave other text as-is. */
export function historyDisplayValue(raw: string | null | undefined): string {
  if (raw == null || raw === '') return '—';
  const mapped = STATUS_VALUE_HE[raw];
  if (mapped) return mapped;
  if (looksTechnical(raw)) return '—';
  return raw;
}

export function looksTechnical(value: string): boolean {
  if (/^[A-Z][A-Za-z0-9_]+$/.test(value)) return true;
  if (/^[a-z]+(_[a-z0-9]+)+$/.test(value) && !STATUS_VALUE_HE[value]) {
    // snake_case keys that aren't known status values
    if (
      value.includes('_id')
      || value.endsWith('_at')
      || value.startsWith('payment_')
      || value.startsWith('export_')
      || value.startsWith('item_')
      || value.includes('Amount')
    ) {
      return true;
    }
  }
  if (value.startsWith('{') || value.startsWith('[')) return true;
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)) {
    return true;
  }
  return false;
}

/** Approved mapping table for evidence / documentation. */
export function approvedHistoryFieldLabelMap(): Record<string, string> {
  return { ...FIELD_LABELS_HE };
}
