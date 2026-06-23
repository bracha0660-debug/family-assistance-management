import { useState, type FormEvent } from 'react';
import type { CommitteeDecisionDto } from '../../api/committeeDecisions';
import {
  approveCommitteeDecision,
  rejectCommitteeDecision,
  resumeCommitteeDecision,
  suspendCommitteeDecision,
} from '../../api/committeeDecisions';
import { ModalShell } from '../../components/ModalShell';

export type ApprovalAction = 'approve' | 'reject' | 'return' | 'suspend' | 'resume';

interface ApprovalActionModalProps {
  decision: CommitteeDecisionDto;
  action: ApprovalAction;
  onClose: () => void;
  onCompleted: (decision: CommitteeDecisionDto) => void;
}

const TITLES: Record<ApprovalAction, string> = {
  approve: 'אישור בקשה',
  reject: 'דחיית בקשה',
  return: 'החזרה לתיקון',
  suspend: 'השעיה (בהמתנה)',
  resume: 'אישור לתשלום / חידוש אישור',
};

export function ApprovalActionModal({
  decision,
  action,
  onClose,
  onCompleted,
}: ApprovalActionModalProps) {
  const [reason, setReason] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const requiresReason = action === 'reject' || action === 'return' || action === 'suspend';
  const requiresConfirm = action === 'suspend' || action === 'resume';

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    if (requiresReason && reason.trim().length < 3) {
      setError('יש לציין סיבה (3–500 תווים)');
      return;
    }
    if (requiresConfirm && !confirmed) {
      setError(action === 'suspend'
        ? 'יש לאשר שההעברה להמתנה אינה סגירה'
        : 'יש לאשר חידוש אישור');
      return;
    }

    setLoading(true);
    try {
      let updated: CommitteeDecisionDto;
      const notes = reason.trim() || null;
      switch (action) {
        case 'approve':
          updated = await approveCommitteeDecision(decision.id, decision.version, notes);
          break;
        case 'reject':
          updated = await rejectCommitteeDecision(decision.id, decision.version, reason.trim(), false);
          break;
        case 'return':
          updated = await rejectCommitteeDecision(decision.id, decision.version, reason.trim(), true);
          break;
        case 'suspend':
          updated = await suspendCommitteeDecision(decision.id, decision.version, reason.trim());
          break;
        case 'resume':
          updated = await resumeCommitteeDecision(decision.id, decision.version, notes);
          break;
      }
      onCompleted(updated);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <ModalShell
      title={TITLES[action]}
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
            ביטול
          </button>
          <button type="submit" className="btn-primary" disabled={loading}>
            {TITLES[action]}
          </button>
        </>
      )}
    >
      <p>
        {decision.decisionCode} — {decision.familyCode} {decision.familyLastName}
      </p>

      {(requiresReason || action === 'approve' || action === 'resume') && (
        <label className="form-field">
          <span>{requiresReason ? 'סיבה (חובה)' : 'הערות (אופציונלי)'}</span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            maxLength={500}
            required={requiresReason}
          />
        </label>
      )}

      {action === 'suspend' && (
        <label className="form-field checkbox-field">
          <input
            type="checkbox"
            checked={confirmed}
            onChange={(e) => setConfirmed(e.target.checked)}
          />
          <span>מאשר/ת שההעברה להמתנה אינה סגירה או דחייה</span>
        </label>
      )}

      {action === 'resume' && (
        <label className="form-field checkbox-field">
          <input
            type="checkbox"
            checked={confirmed}
            onChange={(e) => setConfirmed(e.target.checked)}
          />
          <span>מאשר/ת חידוש אישור והחזרה לתהליך תשלום</span>
        </label>
      )}
    </ModalShell>
  );
}
