import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
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
  const [createdUsername, setCreatedUsername] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const created = await bootstrapOrgAdmin(
        organization.id,
        username,
        password,
        fullName,
      );
      setCreatedUsername(created.username);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  function handleFormClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    onClose();
  }

  function handleSuccessClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    onBootstrapped();
    setCreatedUsername(null);
    onClose();
  }

  if (createdUsername) {
    return (
      <div className="modal-overlay">
        <div className="modal-card" onClick={(e) => e.stopPropagation()}>
          <h2>מנהל ארגון נוצר</h2>
          <div className="success" role="status" aria-live="polite">
            מנהל נוצר בהצלחה. שמרי את הסיסמה שהזנת. הסיסמה לא תוצג שוב במערכת.
          </div>
          <p>ארגון: <strong>{organization.name}</strong> ({organization.code})</p>
          <p>שם משתמש: <strong>{createdUsername}</strong></p>
          <div className="modal-actions">
            <button type="button" onClick={handleSuccessClose}>סגירה</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-overlay" onClick={handleFormClose}>
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
            <button
              type="button"
              className="btn-secondary"
              onClick={handleFormClose}
              disabled={loading}
            >
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
