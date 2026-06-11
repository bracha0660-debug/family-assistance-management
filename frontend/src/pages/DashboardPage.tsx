import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';

interface DashboardPageProps {
  user: UserDto;
  onLogout: () => void;
}

export function DashboardPage({ user, onLogout }: DashboardPageProps) {
  async function handleLogout() {
    try {
      await logout();
    } finally {
      onLogout();
    }
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>לוח בקרה</h1>
        <button type="button" onClick={handleLogout}>התנתק</button>
      </header>
      <main className="dashboard-main">
        <p>שלום, <strong>{user.fullName}</strong></p>
        <p>תפקיד: {user.role}</p>
        {user.organizationName && <p>ארגון: {user.organizationName}</p>}
        <p className="placeholder">שלב 1 — שלד המערכת. מודולים עסקיים יתווספו בשלבים הבאים.</p>
      </main>
    </div>
  );
}
