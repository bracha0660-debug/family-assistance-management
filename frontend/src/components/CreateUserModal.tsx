import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  assignableRoles,
  createOrgUser,
  type AssignableRole,
  type OrgUserDto,
} from '../api/orgUsers';
import { translateRole } from './roleLabel';

interface CreateUserModalProps {
  onClose: () => void;
  onCreated: (created: OrgUserDto) => void;
}

export function CreateUserModal({ onClose, onCreated }: CreateUserModalProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [role, setRole] = useState<AssignableRole>('Coordinator');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  function handleOverlayClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    if (loading) return;
    onClose();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const created = await createOrgUser({ username, password, fullName, role });
      onCreated(created);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={handleOverlayClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>יצירת משתמש חדש</h2>
        <form onSubmit={handleSubmit}>
          <label htmlFor="new-user-username">שם משתמש</label>
          <input
            id="new-user-username"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            disabled={loading}
            required
            minLength={3}
            maxLength={100}
          />
          <label htmlFor="new-user-fullname">שם מלא</label>
          <input
            id="new-user-fullname"
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="new-user-role">תפקיד</label>
          <select
            id="new-user-role"
            value={role}
            onChange={(e) => setRole(e.target.value as AssignableRole)}
            disabled={loading}
            required
          >
            {assignableRoles.map((r) => (
              <option key={r} value={r}>
                {translateRole(r)}
              </option>
            ))}
          </select>
          <label htmlFor="new-user-password">סיסמה ראשונית</label>
          <input
            id="new-user-password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={loading}
            required
            minLength={8}
            maxLength={128}
          />
          <p className="hint-text">
            הסיסמה לא תוצג שוב במערכת. ודאי שמסרת אותה למשתמש בערוץ מאובטח.
          </p>
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={handleOverlayClose}
              disabled={loading}
            >
              ביטול
            </button>
            <button type="submit" disabled={loading}>
              {loading ? 'יוצר...' : 'צור משתמש'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

