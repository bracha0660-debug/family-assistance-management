import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import { deactivateFamily, type FamilyDto } from '../api/families';

interface DeactivateFamilyDialogProps {
  family: FamilyDto;
  onClose: () => void;
  onDeactivated: () => void;
}

export function DeactivateFamilyDialog({
  family,
  onClose,
  onDeactivated,
}: DeactivateFamilyDialogProps) {
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
      await deactivateFamily(family.id, family.version, reason);
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
        <h2>השבתת משפחה</h2>
        <p>
          משפחה: <strong>{family.familyCode}</strong> — {family.familyLastName}
        </p>
        <p className="hint-text">השבתת משפחה תסיר אותה מרשימות העבודה הפעילות.</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="deactivate-family-reason">סיבת ההשבתה (חובה)</label>
          <textarea
            id="deactivate-family-reason"
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
              {loading ? 'משבית...' : 'השבת משפחה'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
