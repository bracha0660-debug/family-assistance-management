import { useState } from 'react';
import { logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';
import { hasPermission } from '../hooks/usePermissions';
import { ManagerAssistanceTypesPage } from './ManagerAssistanceTypesPage';
import { ManagerFamiliesPage } from './ManagerFamiliesPage';

interface ManagerDashboardProps {
  user: UserDto;
  onLogout: () => void;
}

type TabId = 'families' | 'types';

export function ManagerDashboard({ user, onLogout }: ManagerDashboardProps) {
  const tabs: { id: TabId; label: string; visible: boolean }[] = [
    { id: 'families', label: 'משפחות', visible: hasPermission(user, PERMISSION_KEYS.familiesView) },
    { id: 'types', label: 'סוגי סיוע', visible: hasPermission(user, PERMISSION_KEYS.assistanceTypesView) },
  ];
  const visibleTabs = tabs.filter((t) => t.visible);
  const [tab, setTab] = useState<TabId>(visibleTabs[0]?.id ?? 'families');
  const activeTab = visibleTabs.some((t) => t.id === tab) ? tab : (visibleTabs[0]?.id ?? 'families');

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

      {visibleTabs.length > 0 && (
        <nav className="tab-nav" aria-label="ניווט במערכת">
          {visibleTabs.map((t) => (
            <button
              key={t.id}
              type="button"
              className={`tab-button ${activeTab === t.id ? 'tab-active' : ''}`}
              onClick={() => setTab(t.id)}
            >
              {t.label}
            </button>
          ))}
        </nav>
      )}

      <main className="dashboard-main super-admin-main">
        {visibleTabs.length === 0 && (
          <p className="empty-row">אין הרשאות מוגדרות. פנה/י למנהל/ת הארגון.</p>
        )}
        {activeTab === 'families' && <ManagerFamiliesPage user={user} />}
        {activeTab === 'types' && <ManagerAssistanceTypesPage user={user} />}
      </main>
    </div>
  );
}
