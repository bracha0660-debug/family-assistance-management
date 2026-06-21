import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import { restoreFamily, type FamilyDto } from '../api/families';

interface RestoreFamilyDialogProps {
  family: FamilyDto;
  onClose: () => void;
  onRestored: () => void;
}

export function RestoreFamilyDialog({
  family,
  onClose,
  onRestored,
}: RestoreFamilyDialogProps) {
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
      await restoreFamily(family.id, family.version, reason);
      onRestored();
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
        <h2>שחזור משפחה</h2>
        <p>
          משפחה: <strong>{family.familyCode}</strong> — {family.familyLastName}
        </p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="restore-family-reason">סיבת השחזור (חובה)</label>
          <textarea
            id="restore-family-reason"
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
            <button type="submit" disabled={loading}>
              {loading ? 'משחזר...' : 'שחזר משפחה'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
