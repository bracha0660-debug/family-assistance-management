import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { FinanceAssistanceTypesPage } from './FinanceAssistanceTypesPage';

interface FinanceDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

export function FinanceDashboard({ user, onLogout }: FinanceDashboardProps) {
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
        <h1>ניהול סוגי סיוע {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <main className="dashboard-main super-admin-main">
        <FinanceAssistanceTypesPage />
      </main>
    </div>
  );
}
