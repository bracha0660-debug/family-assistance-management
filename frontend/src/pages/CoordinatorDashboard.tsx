import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { CoordinatorFamiliesPage } from './CoordinatorFamiliesPage';

interface CoordinatorDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

export function CoordinatorDashboard({ user, onLogout }: CoordinatorDashboardProps) {
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
        <h1>ניהול משפחות {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <main className="dashboard-main super-admin-main">
        <CoordinatorFamiliesPage currentUserId={user.id} />
      </main>
    </div>
  );
}
