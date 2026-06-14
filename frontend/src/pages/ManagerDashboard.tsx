import { useState } from 'react';
import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { ManagerAssistanceTypesPage } from './ManagerAssistanceTypesPage';
import { ManagerFamiliesPage } from './ManagerFamiliesPage';

interface ManagerDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

type TabId = 'families' | 'types';

export function ManagerDashboard({ user, onLogout }: ManagerDashboardProps) {
  const [tab, setTab] = useState<TabId>('families');

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
        <h1>תצוגת ועדה {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <nav className="tab-nav" aria-label="ניווט במערכת">
        <button
          type="button"
          className={`tab-button ${tab === 'families' ? 'tab-active' : ''}`}
          onClick={() => setTab('families')}
        >
          משפחות
        </button>
        <button
          type="button"
          className={`tab-button ${tab === 'types' ? 'tab-active' : ''}`}
          onClick={() => setTab('types')}
        >
          סוגי סיוע
        </button>
      </nav>

      <main className="dashboard-main super-admin-main">
        {tab === 'families' ? <ManagerFamiliesPage /> : <ManagerAssistanceTypesPage />}
      </main>
    </div>
  );
}
