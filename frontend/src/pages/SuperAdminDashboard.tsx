import { useCallback, useEffect, useState } from 'react';
import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import type { OrganizationDto, OrganizationListResponse } from '../api/admin';
import { listOrganizations } from '../api/admin';
import { BootstrapAdminModal } from '../components/BootstrapAdminModal';
import { CreateOrganizationModal } from '../components/CreateOrganizationModal';
import { SuspendOrganizationDialog } from '../components/SuspendOrganizationDialog';

interface SuperAdminDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

export function SuperAdminDashboard({ user, onLogout }: SuperAdminDashboardProps) {
  const [data, setData] = useState<OrganizationListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [suspendTarget, setSuspendTarget] = useState<OrganizationDto | null>(null);
  const [bootstrapTarget, setBootstrapTarget] = useState<OrganizationDto | null>(null);

  const loadOrganizations = useCallback(async () => {
    setError('');
    try {
      const result = await listOrganizations();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadOrganizations();
  }, [loadOrganizations]);

  async function handleLogout() {
    try {
      await logout();
    } finally {
      onLogout();
    }
  }

  function statusLabel(status: string) {
    return status === 'active' ? 'פעיל' : status === 'suspended' ? 'מושעה' : status;
  }

  return (
    <div className="dashboard super-admin">
      <header className="dashboard-header">
        <h1>ניהול ארגונים — מנהל מערכת</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <main className="dashboard-main super-admin-main">
        {data && (
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-label">סה״כ ארגונים</span>
              <span className="summary-value">{data.summary.total}</span>
            </div>
            <div className="summary-card summary-active">
              <span className="summary-label">פעילים</span>
              <span className="summary-value">{data.summary.active}</span>
            </div>
            <div className="summary-card summary-suspended">
              <span className="summary-label">מושעים</span>
              <span className="summary-value">{data.summary.suspended}</span>
            </div>
          </div>
        )}

        <div className="toolbar">
          <button type="button" onClick={() => setShowCreate(true)}>ארגון חדש</button>
          <button type="button" className="btn-secondary" onClick={loadOrganizations}>רענן</button>
        </div>

        {error && <div className="error" role="alert">{error}</div>}

        {loading ? (
          <p>טוען ארגונים...</p>
        ) : (
          <div className="table-wrap">
            <table className="org-table">
              <thead>
                <tr>
                  <th>שם</th>
                  <th>קוד</th>
                  <th>סטטוס</th>
                  <th>מנהל ארגון</th>
                  <th>פעולות</th>
                </tr>
              </thead>
              <tbody>
                {data?.organizations.length === 0 && (
                  <tr>
                    <td colSpan={5} className="empty-row">אין ארגונים במערכת</td>
                  </tr>
                )}
                {data?.organizations.map((org) => (
                  <tr key={org.id}>
                    <td>{org.name}</td>
                    <td><code>{org.code}</code></td>
                    <td>
                      <span className={`status-badge status-${org.status}`}>
                        {statusLabel(org.status)}
                      </span>
                    </td>
                    <td>{org.hasOrgAdmin ? 'כן' : 'לא'}</td>
                    <td className="actions-cell">
                      {org.status === 'active' && (
                        <>
                          <button
                            type="button"
                            className="btn-small btn-danger"
                            onClick={() => setSuspendTarget(org)}
                          >
                            השעה
                          </button>
                          {!org.hasOrgAdmin && (
                            <button
                              type="button"
                              className="btn-small"
                              onClick={() => setBootstrapTarget(org)}
                            >
                              מנהל ראשון
                            </button>
                          )}
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>

      {showCreate && (
        <CreateOrganizationModal
          onClose={() => setShowCreate(false)}
          onCreated={loadOrganizations}
        />
      )}
      {suspendTarget && (
        <SuspendOrganizationDialog
          organization={suspendTarget}
          onClose={() => setSuspendTarget(null)}
          onSuspended={loadOrganizations}
        />
      )}
      {bootstrapTarget && (
        <BootstrapAdminModal
          organization={bootstrapTarget}
          onClose={() => setBootstrapTarget(null)}
          onBootstrapped={loadOrganizations}
        />
      )}
    </div>
  );
}
