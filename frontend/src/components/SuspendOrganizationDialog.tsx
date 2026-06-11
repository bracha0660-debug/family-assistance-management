import { useState } from 'react';
import type { FormEvent } from 'react';
import type { OrganizationDto } from '../api/admin';
import { suspendOrganization } from '../api/admin';

interface SuspendOrganizationDialogProps {
  organization: OrganizationDto;
  onClose: () => void;
  onSuspended: () => void;
}

export function SuspendOrganizationDialog({
  organization,
  onClose,
  onSuspended,
}: SuspendOrganizationDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await suspendOrganization(organization.id, organization.version, reason);
      onSuspended();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>השעיית ארגון</h2>
        <p>ארגון: <strong>{organization.name}</strong> ({organization.code})</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="suspend-reason">סיבת השעיה (חובה)</label>
          <textarea
            id="suspend-reason"
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
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
              ביטול
            </button>
            <button type="submit" className="btn-danger" disabled={loading}>
              {loading ? 'משעה...' : 'השעה ארגון'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
