import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import { disableOrgUser, type OrgUserDto } from '../api/orgUsers';

interface DisableUserDialogProps {
  user: OrgUserDto;
  onClose: () => void;
  onDisabled: () => void;
}

export function DisableUserDialog({ user, onClose, onDisabled }: DisableUserDialogProps) {
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
      await disableOrgUser(user.id, user.version, reason);
      onDisabled();
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
        <h2>השבתת משתמש</h2>
        <p>
          משתמש: <strong>{user.fullName}</strong> ({user.username})
        </p>
        <p className="hint-text">
          השבתת המשתמש תנתק את כל ההפעלות הפעילות שלו במערכת ותמנע ממנו להתחבר מחדש.
        </p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="disable-reason">סיבת ההשבתה (חובה)</label>
          <textarea
            id="disable-reason"
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
              {loading ? 'משבית...' : 'השבת משתמש'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
