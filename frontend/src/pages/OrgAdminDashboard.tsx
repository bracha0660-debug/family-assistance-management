import { useState } from 'react';

import { getMe, logout } from '../api/auth';
import { exitOrganization } from '../api/admin';

import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';
import { hasPermission } from '../hooks/usePermissions';

import { CommitteeDecisionsPage } from './CommitteeDecisionsPage';
import { CoordinatorFamiliesPage } from './CoordinatorFamiliesPage';
import { FinanceAssistanceTypesPage } from './FinanceAssistanceTypesPage';
import { OrgActivityLogPage } from './OrgActivityLogPage';
import { OrgAdminAssistanceTypesPage } from './OrgAdminAssistanceTypesPage';
import { OrgAdminFamiliesPage } from './OrgAdminFamiliesPage';
import { OrgPermissionsPage } from './OrgPermissionsPage';
import { OrgUsersPage } from './OrgUsersPage';
import { PaymentsQueuePage } from './PaymentsQueuePage';
import { SuppliersPage } from './SuppliersPage';

interface OrgAdminDashboardProps {
  user: UserDto;
  onLogout: () => void;
  onUserUpdated?: (user: UserDto) => void;
}

type TabId = 'users' | 'families' | 'types' | 'suppliers' | 'decisions' | 'payments' | 'activity' | 'permissions';

export function OrgAdminDashboard({ user, onLogout, onUserUpdated }: OrgAdminDashboardProps) {
  const [tab, setTab] = useState<TabId>('users');
  const isSuperAdminInOrg = user.role === 'SuperAdmin' && !!user.actingOrganizationId;

  async function handleLogout() {
    try {
      await logout();
    } finally {
      onLogout();
    }
  }

  async function handleExitOrg() {
    if (!user.actingOrganizationId) return;
    try {
      const updated = await exitOrganization(user.actingOrganizationId);
      if (updated && !updated.actingOrganizationId) {
        onUserUpdated?.(updated);
      }
    } catch {
      // ignore
    }
  }

  async function handlePermissionsChanged() {
    try {
      const refreshed = await getMe();
      onUserUpdated?.(refreshed);
    } catch {
      // ignore refresh errors
    }
  }

  const tabs: { id: TabId; label: string; visible: boolean }[] = [
    { id: 'users', label: 'ניהול משתמשים', visible: true },
    { id: 'families', label: 'משפחות', visible: hasPermission(user, PERMISSION_KEYS.familiesView) || isSuperAdminInOrg },
    { id: 'types', label: 'סוגי סיוע', visible: hasPermission(user, PERMISSION_KEYS.assistanceTypesView) || isSuperAdminInOrg },
    { id: 'suppliers', label: 'ספקים', visible: hasPermission(user, PERMISSION_KEYS.suppliersView) || isSuperAdminInOrg },
    { id: 'decisions', label: 'החלטות ועדה', visible: hasPermission(user, PERMISSION_KEYS.committeeDecisionsView) || isSuperAdminInOrg },
    { id: 'payments', label: 'תשלומים', visible: hasPermission(user, PERMISSION_KEYS.paymentsView) || isSuperAdminInOrg },
    { id: 'activity', label: 'יומן פעילות', visible: true },
    { id: 'permissions', label: 'הרשאות', visible: true },
  ];
  const visibleTabs = tabs.filter((t) => t.visible);

  return (
    <div className="dashboard org-admin">
      <header className="dashboard-header">
        <h1>ניהול ארגון {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          {isSuperAdminInOrg && (
            <button type="button" className="btn-secondary" onClick={handleExitOrg}>
              יציאה מארגון
            </button>
          )}
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      <nav className="tab-nav" aria-label="ניווט במערכת">
        {visibleTabs.map((t) => (
          <button
            key={t.id}
            type="button"
            className={`tab-button ${tab === t.id ? 'tab-active' : ''}`}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </nav>

      <main className="dashboard-main super-admin-main">
        {tab === 'users' && <OrgUsersPage />}
        {tab === 'families' && isSuperAdminInOrg && <CoordinatorFamiliesPage user={user} />}
        {tab === 'families' && !isSuperAdminInOrg && <OrgAdminFamiliesPage user={user} />}
        {tab === 'types' && isSuperAdminInOrg && <FinanceAssistanceTypesPage user={user} />}
        {tab === 'types' && !isSuperAdminInOrg && <OrgAdminAssistanceTypesPage user={user} />}
        {tab === 'suppliers' && <SuppliersPage user={user} />}
        {tab === 'decisions' && <CommitteeDecisionsPage user={user} />}
        {tab === 'payments' && <PaymentsQueuePage user={user} />}
        {tab === 'activity' && <OrgActivityLogPage />}
        {tab === 'permissions' && (
          <OrgPermissionsPage onPermissionsChanged={handlePermissionsChanged} />
        )}
      </main>
    </div>
  );
}
