import { useEffect, useState } from 'react';
import { getMe, logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { exitOrganization } from '../api/admin';
import { PERMISSION_KEYS } from '../api/permissions';
import { canWriteFamilies, hasPermission } from '../hooks/usePermissions';
import { CommitteeDecisionsPage } from './CommitteeDecisionsPage';
import { CoordinatorFamiliesPage } from './CoordinatorFamiliesPage';
import { FinanceAssistanceTypesPage } from './FinanceAssistanceTypesPage';
import { ManagerAssistanceTypesPage } from './ManagerAssistanceTypesPage';
import { ManagerFamiliesPage } from './ManagerFamiliesPage';
import { PaymentsQueuePage } from './PaymentsQueuePage';
import { SuppliersPage } from './SuppliersPage';

interface OrgUserDashboardProps {
  user: UserDto;
  onLogout: () => void;
  onUserUpdated?: (user: UserDto) => void;
}

type TabId = 'families' | 'types' | 'suppliers' | 'decisions' | 'payments';

export function OrgUserDashboard({ user, onLogout, onUserUpdated }: OrgUserDashboardProps) {
  const tabs: { id: TabId; label: string; visible: boolean }[] = [
    { id: 'families', label: 'משפחות', visible: hasPermission(user, PERMISSION_KEYS.familiesView) },
    { id: 'types', label: 'סוגי סיוע', visible: hasPermission(user, PERMISSION_KEYS.assistanceTypesView) },
    { id: 'suppliers', label: 'ספקים', visible: hasPermission(user, PERMISSION_KEYS.suppliersView) },
    { id: 'decisions', label: 'החלטות ועדה', visible: hasPermission(user, PERMISSION_KEYS.committeeDecisionsView) },
    { id: 'payments', label: 'תשלומים', visible: hasPermission(user, PERMISSION_KEYS.paymentsView) },
  ];
  const visibleTabs = tabs.filter((t) => t.visible);
  const [tab, setTab] = useState<TabId>(visibleTabs[0]?.id ?? 'families');
  const activeTab = visibleTabs.some((t) => t.id === tab) ? tab : (visibleTabs[0]?.id ?? 'families');

  useEffect(() => {
    if (!onUserUpdated) return;
    getMe().then(onUserUpdated).catch(() => {});
    const refresh = () => {
      getMe().then(onUserUpdated).catch(() => {});
    };
    window.addEventListener('focus', refresh);
    return () => window.removeEventListener('focus', refresh);
  }, [onUserUpdated]);

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

  async function handleLogout() {
    try {
      await logout();
    } finally {
      onLogout();
    }
  }

  const isSuperAdminInOrg = user.role === 'SuperAdmin' && !!user.actingOrganizationId;
  const showWritableFamilies = isSuperAdminInOrg || canWriteFamilies(user);
  const showReadOnlyFamilies = !showWritableFamilies && hasPermission(user, PERMISSION_KEYS.familiesView);

  return (
    <div className="dashboard org-admin">
      <header className="dashboard-header">
        <h1>מערכת סיוע {user.organizationName ? `— ${user.organizationName}` : ''}</h1>
        <div className="header-actions">
          <span className="user-greeting">שלום, {user.fullName}</span>
          {user.role === 'SuperAdmin' && user.actingOrganizationId && (
            <button type="button" className="btn-secondary" onClick={handleExitOrg}>
              יציאה מארגון
            </button>
          )}
          <button type="button" onClick={handleLogout}>התנתק</button>
        </div>
      </header>

      {visibleTabs.length > 1 && (
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
        {activeTab === 'families' && showWritableFamilies && (
          <CoordinatorFamiliesPage user={user} />
        )}
        {activeTab === 'families' && showReadOnlyFamilies && (
          <ManagerFamiliesPage user={user} />
        )}
        {activeTab === 'types' && hasPermission(user, PERMISSION_KEYS.assistanceTypesCreate) && (
          <FinanceAssistanceTypesPage user={user} />
        )}
        {activeTab === 'types' && hasPermission(user, PERMISSION_KEYS.assistanceTypesView)
          && !hasPermission(user, PERMISSION_KEYS.assistanceTypesCreate) && (
          <ManagerAssistanceTypesPage user={user} />
        )}
        {activeTab === 'suppliers' && hasPermission(user, PERMISSION_KEYS.suppliersView) && (
          <SuppliersPage user={user} />
        )}
        {activeTab === 'decisions' && hasPermission(user, PERMISSION_KEYS.committeeDecisionsView) && (
          <CommitteeDecisionsPage user={user} />
        )}
        {activeTab === 'payments' && hasPermission(user, PERMISSION_KEYS.paymentsView) && (
          <PaymentsQueuePage user={user} />
        )}
      </main>
    </div>
  );
}
