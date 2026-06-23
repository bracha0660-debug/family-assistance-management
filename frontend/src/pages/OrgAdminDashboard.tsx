import { useState } from 'react';

import { getMe, logout } from '../api/auth';
import { exitOrganization } from '../api/admin';

import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';
import { hasPermission } from '../hooks/usePermissions';

import { hasWorkflowViewAccess } from './workflow/workflowSections';
import type { HomeNavigationTarget } from '../api/workflow';
import { HomeDashboardPage } from './home/HomeDashboardPage';
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
import { AppShell } from '../components/AppShell';

interface OrgAdminDashboardProps {
  user: UserDto;
  onLogout: () => void;
  onUserUpdated?: (user: UserDto) => void;
}

type TabId = 'workflow' | 'users' | 'families' | 'types' | 'suppliers' | 'decisions' | 'payments' | 'activity' | 'permissions';

export function OrgAdminDashboard({ user, onLogout, onUserUpdated }: OrgAdminDashboardProps) {
  const isSuperAdminInOrg = user.role === 'SuperAdmin' && !!user.actingOrganizationId;
  const hasWorkflow = hasWorkflowViewAccess(user) || isSuperAdminInOrg;
  const [tab, setTab] = useState<TabId>(hasWorkflow ? 'workflow' : 'users');
  const [listFilter, setListFilter] = useState<HomeNavigationTarget | null>(null);

  function handleHomeNavigate(target: HomeNavigationTarget) {
    setListFilter(target);
    setTab(target.targetTab);
  }

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
    { id: 'workflow', label: 'לוח בקרה', visible: hasWorkflow },
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
  const activeLabel = visibleTabs.find((t) => t.id === tab)?.label ?? 'ניהול ארגון';
  const pageTitle = `${activeLabel}${user.organizationName ? ` — ${user.organizationName}` : ''}`;
  const homeTabId: TabId | undefined = hasWorkflow ? 'workflow' : visibleTabs[0]?.id;

  return (
    <AppShell
      brandTitle="ניהול ארגון"
      brandLogoSrc="/keren-ahavat-chesed-logo.png"
      homeTabId={homeTabId}
      pageTitle={pageTitle}
      user={user}
      tabs={visibleTabs}
      activeTab={tab}
      onTabChange={setTab}
      onLogout={handleLogout}
      onExitOrg={isSuperAdminInOrg ? handleExitOrg : undefined}
    >
      {tab === 'workflow' && hasWorkflow && (
        <HomeDashboardPage onNavigate={handleHomeNavigate} />
      )}
      {tab === 'users' && <OrgUsersPage />}
      {tab === 'families' && isSuperAdminInOrg && <CoordinatorFamiliesPage user={user} />}
      {tab === 'families' && !isSuperAdminInOrg && <OrgAdminFamiliesPage user={user} />}
      {tab === 'types' && isSuperAdminInOrg && <FinanceAssistanceTypesPage user={user} />}
      {tab === 'types' && !isSuperAdminInOrg && <OrgAdminAssistanceTypesPage user={user} />}
      {tab === 'suppliers' && <SuppliersPage user={user} />}
      {tab === 'decisions' && <CommitteeDecisionsPage user={user} initialFilter={listFilter} />}
      {tab === 'payments' && <PaymentsQueuePage user={user} initialFilter={listFilter} />}
      {tab === 'activity' && <OrgActivityLogPage />}
      {tab === 'permissions' && (
        <OrgPermissionsPage onPermissionsChanged={handlePermissionsChanged} />
      )}
    </AppShell>
  );
}
