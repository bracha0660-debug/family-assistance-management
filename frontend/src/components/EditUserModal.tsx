import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  assignableRoles,
  updateOrgUser,
  type AssignableRole,
  type OrgUserDto,
  type UpdateOrgUserPayload,
} from '../api/orgUsers';
import { translateRole } from './roleLabel';

interface EditUserModalProps {
  user: OrgUserDto;
  onClose: () => void;
  onUpdated: () => void;
}

function isAssignable(role: string): role is AssignableRole {
  return (assignableRoles as readonly string[]).includes(role);
}

export function EditUserModal({ user, onClose, onUpdated }: EditUserModalProps) {
  const isOrgAdmin = user.role === 'OrganizationAdministrator';
  const initialRole: AssignableRole = isAssignable(user.role) ? user.role : 'Coordinator';

  const [fullName, setFullName] = useState(user.fullName);
  const [role, setRole] = useState<AssignableRole>(initialRole);
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
      const payload: UpdateOrgUserPayload = {};
      const trimmedName = fullName.trim();
      if (trimmedName !== user.fullName) payload.fullName = trimmedName;
      if (!isOrgAdmin && role !== user.role) payload.role = role;

      if (!payload.fullName && !payload.role) {
        setError('אין שינויים לעדכון');
        setLoading(false);
        return;
      }

      await updateOrgUser(user.id, user.version, payload);
      onUpdated();
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
        <h2>עריכת משתמש</h2>
        <p>שם משתמש: <strong>{user.username}</strong></p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="edit-fullname">שם מלא</label>
          <input
            id="edit-fullname"
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="edit-role">תפקיד</label>
          {isOrgAdmin ? (
            <input
              id="edit-role"
              type="text"
              value={translateRole(user.role)}
              disabled
              readOnly
              aria-describedby="edit-role-locked"
            />
          ) : (
            <select
              id="edit-role"
              value={role}
              onChange={(e) => setRole(e.target.value as AssignableRole)}
              disabled={loading || user.isSelf}
            >
              {assignableRoles.map((r) => (
                <option key={r} value={r}>
                  {translateRole(r)}
                </option>
              ))}
            </select>
          )}
          {isOrgAdmin && (
            <p id="edit-role-locked" className="hint-text">
              לא ניתן לשנות תפקיד של מנהל ארגון בשלב זה.
            </p>
          )}
          {user.isSelf && !isOrgAdmin && (
            <p className="hint-text">לא ניתן לשנות את התפקיד של עצמך.</p>
          )}
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
              {loading ? 'שומר...' : 'שמור שינויים'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
