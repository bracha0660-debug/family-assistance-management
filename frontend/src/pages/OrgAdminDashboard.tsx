import { useState } from 'react';
import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { OrgActivityLogPage } from './OrgActivityLogPage';
import { OrgUsersPage } from './OrgUsersPage';

interface OrgAdminDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

type TabId = 'users' | 'activity';

export function OrgAdminDashboard({ user, onLogout }: OrgAdminDashboardProps) {
  const [tab, setTab] = useState<TabId>('users');

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
        <h1>ניהול ארגון {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <nav className="tab-nav" aria-label="ניווט במערכת">
        <button
          type="button"
          className={`tab-button ${tab === 'users' ? 'tab-active' : ''}`}
          onClick={() => setTab('users')}
        >
          ניהול משתמשים
        </button>
        <button
          type="button"
          className={`tab-button ${tab === 'activity' ? 'tab-active' : ''}`}
          onClick={() => setTab('activity')}
        >
          יומן פעילות
        </button>
      </nav>

      <main className="dashboard-main super-admin-main">
        {tab === 'users' ? <OrgUsersPage /> : <OrgActivityLogPage />}
      </main>
    </div>
  );
}
