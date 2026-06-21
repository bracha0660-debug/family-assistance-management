import { useCallback, useEffect, useState } from 'react';
import {
  listOrgUsers,
  type OrgUserDto,
  type OrgUserListResponse,
} from '../api/orgUsers';
import { CreateUserModal } from '../components/CreateUserModal';
import { DisableUserDialog } from '../components/DisableUserDialog';
import { EditUserModal } from '../components/EditUserModal';
import { UserCreatedConfirmation } from '../components/UserCreatedConfirmation';
import { translateRole, translateStatus } from '../components/roleLabel';

export function OrgUsersPage() {
  const [data, setData] = useState<OrgUserListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<OrgUserDto | null>(null);
  const [disableTarget, setDisableTarget] = useState<OrgUserDto | null>(null);
  const [createdUser, setCreatedUser] = useState<OrgUserDto | null>(null);

  const loadUsers = useCallback(async () => {
    setError('');
    try {
      const result = await listOrgUsers();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadUsers();
  }, [loadUsers]);

  function handleUserCreated(created: OrgUserDto) {
    setShowCreate(false);
    setCreatedUser(created);
    loadUsers();
  }

  function handleBackToList() {
    setCreatedUser(null);
  }

  function handleCreateAnother() {
    setCreatedUser(null);
    setShowCreate(true);
  }

  function displayRole(u: OrgUserDto): string {
    if (u.role === 'OrganizationAdministrator') return translateRole(u.role);
    return u.organizationRoleName ?? translateRole(u.role);
  }

  if (createdUser) {
    return (
      <UserCreatedConfirmation
        user={createdUser}
        onBackToList={handleBackToList}
        onCreateAnother={handleCreateAnother}
      />
    );
  }

  return (
    <div>
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ משתמשים</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">פעילים</span>
            <span className="summary-value">{data.summary.active}</span>
          </div>
          <div className="summary-card summary-suspended">
            <span className="summary-label">מושבתים</span>
            <span className="summary-value">{data.summary.disabled}</span>
          </div>
        </div>
      )}

      <div className="toolbar">
        <button type="button" onClick={() => setShowCreate(true)}>משתמש חדש</button>
        <button type="button" className="btn-secondary" onClick={loadUsers}>רענן</button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען משתמשים...</p>
      ) : (
        <div className="table-wrap">
          <table className="org-table">
            <thead>
              <tr>
                <th>שם מלא</th>
                <th>שם משתמש</th>
                <th>תפקיד</th>
                <th>סטטוס</th>
                <th>פעולות</th>
              </tr>
            </thead>
            <tbody>
              {data?.users.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty-row">אין משתמשים בארגון</td>
                </tr>
              )}
              {data?.users.map((u) => (
                <tr key={u.id} className={u.status === 'disabled' ? 'row-disabled' : undefined}>
                  <td>
                    {u.fullName}
                    {u.isSelf && <span className="hint-text"> (אני)</span>}
                  </td>
                  <td><code>{u.username}</code></td>
                  <td>{displayRole(u)}</td>
                  <td>
                    <span className={`status-badge status-${u.status === 'disabled' ? 'suspended' : u.status}`}>
                      {translateStatus(u.status)}
                    </span>
                  </td>
                  <td className="actions-cell">
                    {u.status === 'active' && !u.isSelf && u.role !== 'OrganizationAdministrator' && (
                      <>
                        <button
                          type="button"
                          className="btn-small"
                          onClick={() => setEditTarget(u)}
                        >
                          ערוך
                        </button>
                        <button
                          type="button"
                          className="btn-small btn-danger"
                          onClick={() => setDisableTarget(u)}
                        >
                          השבת
                        </button>
                      </>
                    )}
                    {u.status === 'active' && u.role === 'OrganizationAdministrator' && !u.isSelf && (
                      <button
                        type="button"
                        className="btn-small"
                        onClick={() => setEditTarget(u)}
                      >
                        ערוך שם
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onCreated={handleUserCreated}
        />
      )}
      {editTarget && (
        <EditUserModal
          user={editTarget}
          onClose={() => setEditTarget(null)}
          onUpdated={loadUsers}
        />
      )}
      {disableTarget && (
        <DisableUserDialog
          user={disableTarget}
          onClose={() => setDisableTarget(null)}
          onDisabled={loadUsers}
        />
      )}
    </div>
  );
}
