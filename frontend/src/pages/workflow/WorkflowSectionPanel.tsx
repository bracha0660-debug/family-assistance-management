import { useState } from 'react';
import type { CommitteeDecisionDto } from '../../api/committeeDecisions';
import type { PaymentQueueItemDto } from '../../api/payments';
import type { WorkflowSectionSummary } from '../../api/workflow';

interface WorkflowSectionPanelProps {
  section: WorkflowSectionSummary;
  defaultExpanded?: boolean;
  onDecisionAction?: (decision: CommitteeDecisionDto, action: string) => void;
  onPaymentAction?: (payment: PaymentQueueItemDto, action: string) => void;
}

function translateStatus(status: string): string {
  const map: Record<string, string> = {
    draft: 'טיוטה',
    submitted: 'הוגש',
    returned_for_revision: 'הוחזר לתיקון',
    approved: 'אושר',
    rejected: 'נדחה',
    suspended: 'מושעה',
    cancelled: 'בוטל',
    partially_paid: 'שולם חלקית',
    fully_paid: 'שולם במלואו',
    awaiting_payment: 'ממתין לתשלום',
    executing: 'בביצוע',
    proof_uploaded: 'הוכחה הועלתה',
    paid: 'שולם',
    on_hold: 'בהמתנה',
    returned_to_coordinator: 'הוחזר לרכז',
  };
  return map[status] ?? status;
}

export function WorkflowSectionPanel({
  section,
  defaultExpanded = false,
  onDecisionAction,
  onPaymentAction,
}: WorkflowSectionPanelProps) {
  const [expanded, setExpanded] = useState(defaultExpanded || section.awaitingActionCount > 0);

  if (section.count === 0) return null;

  const visibilityLabel = section.visibility === 'mine' ? 'שלי' : 'ארגון';

  return (
    <section className="workflow-section-panel" id={`section-${section.sectionId}`}>
      <button
        type="button"
        className="workflow-section-header"
        onClick={() => setExpanded((v) => !v)}
        aria-expanded={expanded}
      >
        <span>{expanded ? '▼' : '▶'} {section.title} ({section.count})</span>
        <span className="workflow-visibility-badge">{visibilityLabel}</span>
        {section.awaitingActionCount > 0 && (
          <span className="workflow-action-badge">{section.awaitingActionCount} לטיפול</span>
        )}
      </button>

      {expanded && (
        <div className="workflow-section-body">
          {section.decisionPreview && section.decisionPreview.length > 0 && (
            <table className="data-table">
              <thead>
                <tr>
                  <th>קוד</th>
                  <th>משפחה</th>
                  <th>סטטוס</th>
                  <th>סכום</th>
                  <th>פעולות</th>
                </tr>
              </thead>
              <tbody>
                {section.decisionPreview.map((d) => (
                  <tr key={d.id}>
                    <td>{d.decisionCode}</td>
                    <td>{d.familyCode} — {d.familyLastName}</td>
                    <td>{translateStatus(d.status)}</td>
                    <td>{d.totalAmount.toLocaleString('he-IL')} ₪</td>
                    <td className="action-cell">
                      {d.suspendReason && d.status === 'suspended' && (
                        <span className="reason-banner">סיבת השעיה: {d.suspendReason}</span>
                      )}
                      {d.availableActions.map((action) => (
                        <button
                          key={action}
                          type="button"
                          className={actionButtonClass(action)}
                          onClick={() => onDecisionAction?.(d, action)}
                        >
                          {actionLabel(action)}
                        </button>
                      ))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {section.paymentPreview && section.paymentPreview.length > 0 && (
            <table className="data-table">
              <thead>
                <tr>
                  <th>החלטה</th>
                  <th>משפחה</th>
                  <th>סוג סיוע</th>
                  <th>סכום</th>
                  <th>סטטוס</th>
                  <th>פעולות</th>
                </tr>
              </thead>
              <tbody>
                {section.paymentPreview.map((p) => (
                  <tr key={p.id} className={p.isOnHold ? 'row-on-hold' : undefined}>
                    <td>{p.decisionCode}</td>
                    <td>{p.familyCode} — {p.familyLastName}</td>
                    <td>{p.assistanceTypeName}</td>
                    <td>{p.amount.toLocaleString('he-IL')} ₪</td>
                    <td>
                      {p.isOnHold ? 'מושעה — לא ניתן לביצוע' : translateStatus(p.status)}
                    </td>
                    <td className="action-cell">
                      {p.availableActions.map((action) => (
                        <button
                          key={action}
                          type="button"
                          className={actionButtonClass(action)}
                          onClick={() => onPaymentAction?.(p, action)}
                        >
                          {actionLabel(action)}
                        </button>
                      ))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </section>
  );
}

function actionButtonClass(action: string): string {
  if (action === 'suspend' || action === 'cancel') {
    return 'workflow-btn-action workflow-btn-action-purple';
  }
  return 'workflow-btn-action';
}

function actionLabel(action: string): string {
  const labels: Record<string, string> = {
    edit: 'עריכה',
    submit: 'הגשה',
    approve: 'אישור',
    reject: 'דחייה',
    return: 'החזרה לתיקון',
    suspend: 'השעיה',
    resume: 'אישור לתשלום',
    cancel: 'ביטול',
    execute: 'ביצוע',
    upload_proof: 'העלאת הוכחה',
    mark_paid: 'סימון שולם',
    return_to_coordinator: 'החזרה לרכז',
  };
  return labels[action] ?? action;
}
