import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { listOrgRoles, type OrganizationRoleListItem } from '../api/permissions';
import {
  createOrgUser,
  type OrgUserDto,
} from '../api/orgUsers';
import { ModalShell } from './ModalShell';

interface CreateUserModalProps {
  onClose: () => void;
  onCreated: (created: OrgUserDto) => void;
}

export function CreateUserModal({ onClose, onCreated }: CreateUserModalProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [organizationRoleId, setOrganizationRoleId] = useState('');
  const [roles, setRoles] = useState<OrganizationRoleListItem[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    listOrgRoles()
      .then((data) => {
        const active = data.filter((r) => r.status === 'active');
        setRoles(active);
        if (active.length > 0) setOrganizationRoleId(active[0].id);
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'שגיאת מערכת'));
  }, []);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const created = await createOrgUser({ username, password, fullName, organizationRoleId });
      onCreated(created);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <ModalShell
      title="יצירת משתמש חדש"
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={() => onClose()} disabled={loading}>
            ביטול
          </button>
          <button type="submit" disabled={loading || !organizationRoleId}>
            {loading ? 'יוצר...' : 'צור משתמש'}
          </button>
        </>
      )}
    >
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
        value={organizationRoleId}
        onChange={(e) => setOrganizationRoleId(e.target.value)}
        disabled={loading || roles.length === 0}
        required
      >
        {roles.map((r) => (
          <option key={r.id} value={r.id}>
            {r.name}
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
    </ModalShell>
  );
}
