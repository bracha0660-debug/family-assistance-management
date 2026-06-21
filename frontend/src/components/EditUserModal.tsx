import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { listOrgRoles, type OrganizationRoleListItem } from '../api/permissions';
import {
  updateOrgUser,
  type OrgUserDto,
  type UpdateOrgUserPayload,
} from '../api/orgUsers';
import { ModalShell } from './ModalShell';
import { translateRole } from './roleLabel';

interface EditUserModalProps {
  user: OrgUserDto;
  onClose: () => void;
  onUpdated: () => void;
}

export function EditUserModal({ user, onClose, onUpdated }: EditUserModalProps) {
  const isOrgAdmin = user.role === 'OrganizationAdministrator';

  const [fullName, setFullName] = useState(user.fullName);
  const [organizationRoleId, setOrganizationRoleId] = useState(user.organizationRoleId ?? '');
  const [roles, setRoles] = useState<OrganizationRoleListItem[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!isOrgAdmin) {
      listOrgRoles()
        .then((data) => setRoles(data.filter((r) => r.status === 'active')))
        .catch((err) => setError(err instanceof Error ? err.message : 'שגיאת מערכת'));
    }
  }, [isOrgAdmin]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const payload: UpdateOrgUserPayload = {};
      const trimmedName = fullName.trim();
      if (trimmedName !== user.fullName) payload.fullName = trimmedName;
      if (!isOrgAdmin && organizationRoleId && organizationRoleId !== user.organizationRoleId) {
        payload.organizationRoleId = organizationRoleId;
      }

      if (!payload.fullName && !payload.organizationRoleId) {
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
    <ModalShell
      title="עריכת משתמש"
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={() => onClose()} disabled={loading}>
            ביטול
          </button>
          <button type="submit" disabled={loading}>
            {loading ? 'שומר...' : 'שמור שינויים'}
          </button>
        </>
      )}
    >
      <p>שם משתמש: <strong>{user.username}</strong></p>
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
          value={organizationRoleId}
          onChange={(e) => setOrganizationRoleId(e.target.value)}
          disabled={loading || user.isSelf}
        >
          {roles.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
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
    </ModalShell>
  );
}
