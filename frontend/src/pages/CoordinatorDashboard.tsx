import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';
import { hasPermission } from '../hooks/usePermissions';
import { CoordinatorFamiliesPage } from './CoordinatorFamiliesPage';

interface CoordinatorDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

export function CoordinatorDashboard({ user, onLogout }: CoordinatorDashboardProps) {
  const showFamilies = hasPermission(user, PERMISSION_KEYS.familiesView);

  async function handleLogout() {
    try {
      await logout();
    } finally {
      onLogout();
    }
  }

  return (
    <div className="dashboard org-admin">
      <header className="dashboard-header">
        <h1>ניהול מתאם/ת {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <main className="dashboard-main super-admin-main">
        {!showFamilies && (
          <p className="empty-row">אין הרשאות מוגדרות. פנה/י למנהל/ת הארגון.</p>
        )}
        {showFamilies && <CoordinatorFamiliesPage user={user} />}
      </main>
    </div>
  );
}
