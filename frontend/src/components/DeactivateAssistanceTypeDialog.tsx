import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import { deactivateAssistanceType, type AssistanceTypeDto } from '../api/assistanceTypes';

interface DeactivateAssistanceTypeDialogProps {
  assistanceType: AssistanceTypeDto;
  onClose: () => void;
  onDeactivated: () => void;
}

export function DeactivateAssistanceTypeDialog({
  assistanceType,
  onClose,
  onDeactivated,
}: DeactivateAssistanceTypeDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  function handleClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    if (loading) return;
    onClose();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await deactivateAssistanceType(assistanceType.id, assistanceType.version, reason);
      onDeactivated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>השבתת סוג סיוע</h2>
        <p>
          סוג סיוע: <strong>{assistanceType.typeCode}</strong> — {assistanceType.name}
        </p>
        <p className="hint-text">סוגי סיוע מושבתים לא יהיו זמינים לבחירה בהחלטות חדשות.</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="deactivate-type-reason">סיבת ההשבתה (חובה)</label>
          <textarea
            id="deactivate-type-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={loading}
            rows={4}
            required
            minLength={3}
            maxLength={500}
          />
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={handleClose}
              disabled={loading}
            >
              ביטול
            </button>
            <button type="submit" className="btn-danger" disabled={loading}>
              {loading ? 'משבית...' : 'השבת סוג סיוע'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
