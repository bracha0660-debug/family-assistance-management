import { useState } from 'react';
import type { FormEvent } from 'react';
import type { OrganizationDto } from '../api/admin';
import { bootstrapOrgAdmin } from '../api/admin';

interface BootstrapAdminModalProps {
  organization: OrganizationDto;
  onClose: () => void;
  onBootstrapped: () => void;
}

export function BootstrapAdminModal({
  organization,
  onClose,
  onBootstrapped,
}: BootstrapAdminModalProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await bootstrapOrgAdmin(organization.id, username, password, fullName);
      onBootstrapped();
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
        <h2>יצירת מנהל ארגון ראשון</h2>
        <p>ארגון: <strong>{organization.name}</strong> ({organization.code})</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="admin-username">שם משתמש</label>
          <input
            id="admin-username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            disabled={loading}
            required
          />
          <label htmlFor="admin-fullname">שם מלא</label>
          <input
            id="admin-fullname"
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            disabled={loading}
            required
          />
          <label htmlFor="admin-password">סיסמה</label>
          <input
            id="admin-password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={loading}
            required
            minLength={8}
          />
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
              ביטול
            </button>
            <button type="submit" disabled={loading}>
              {loading ? 'יוצר...' : 'צור מנהל'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
