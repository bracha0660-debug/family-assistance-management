import { useEffect, useState } from 'react';
import { getMe, logout } from '../api/auth';
import type { UserDto } from '../api/auth';
import { exitOrganization } from '../api/admin';
import { PERMISSION_KEYS } from '../api/permissions';
import { canWriteFamilies, hasPermission } from '../hooks/usePermissions';
import { WorkflowDashboardPage } from './workflow/WorkflowDashboardPage';
import { hasWorkflowViewAccess } from './workflow/workflowSections';
import { CommitteeDecisionsPage } from './CommitteeDecisionsPage';
import { CoordinatorFamiliesPage } from './CoordinatorFamiliesPage';
import { FinanceAssistanceTypesPage } from './FinanceAssistanceTypesPage';
import { ManagerAssistanceTypesPage } from './ManagerAssistanceTypesPage';
import { ManagerFamiliesPage } from './ManagerFamiliesPage';
import { PaymentsQueuePage } from './PaymentsQueuePage';
import { SuppliersPage } from './SuppliersPage';
import { AppShell } from '../components/AppShell';

interface OrgUserDashboardProps {
  user: UserDto;
  onLogout: () => void;
  onUserUpdated?: (user: UserDto) => void;
}

type TabId = 'workflow' | 'families' | 'types' | 'suppliers' | 'decisions' | 'payments';

export function OrgUserDashboard({ user, onLogout, onUserUpdated }: OrgUserDashboardProps) {
  const hasWorkflow = hasWorkflowViewAccess(user);
  const tabs: { id: TabId; label: string; visible: boolean }[] = [
    { id: 'workflow', label: 'לוח בקרה', visible: hasWorkflow },
    { id: 'families', label: 'משפחות', visible: hasPermission(user, PERMISSION_KEYS.familiesView) },
    { id: 'types', label: 'סוגי סיוע', visible: hasPermission(user, PERMISSION_KEYS.assistanceTypesView) },
    { id: 'suppliers', label: 'ספקים', visible: hasPermission(user, PERMISSION_KEYS.suppliersView) },
    { id: 'decisions', label: 'החלטות ועדה', visible: hasPermission(user, PERMISSION_KEYS.committeeDecisionsView) },
    { id: 'payments', label: 'תשלומים', visible: hasPermission(user, PERMISSION_KEYS.paymentsView) },
  ];
  const visibleTabs = tabs.filter((t) => t.visible);
  const defaultTab: TabId = hasWorkflow ? 'workflow' : (visibleTabs[0]?.id ?? 'families');
  const [tab, setTab] = useState<TabId>(defaultTab);
  const activeTab = visibleTabs.some((t) => t.id === tab) ? tab : defaultTab;

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

  const activeLabel = visibleTabs.find((t) => t.id === activeTab)?.label ?? 'מערכת סיוע';
  const pageTitle = `${activeLabel}${user.organizationName ? ` — ${user.organizationName}` : ''}`;
  const homeTabId: TabId | undefined = hasWorkflow ? 'workflow' : visibleTabs[0]?.id;

  return (
    <AppShell
      brandTitle="מערכת סיוע"
      brandLogoSrc="/keren-ahavat-chesed-logo.png"
      homeTabId={homeTabId}
      pageTitle={pageTitle}
      user={user}
      tabs={visibleTabs}
      activeTab={activeTab}
      onTabChange={setTab}
      onLogout={handleLogout}
      onExitOrg={user.role === 'SuperAdmin' && user.actingOrganizationId ? handleExitOrg : undefined}
    >
      {visibleTabs.length === 0 && (
        <p className="empty-row">אין הרשאות מוגדרות. פנה/י למנהל/ת הארגון.</p>
      )}
      {activeTab === 'workflow' && hasWorkflow && (
        <WorkflowDashboardPage user={user} />
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
    </AppShell>
  );
}
