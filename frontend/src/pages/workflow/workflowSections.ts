import type { UserDto } from '../../api/auth';
import { PERMISSION_KEYS } from '../../api/permissions';
import { getGrantScope, hasPermission, hasWorkflowPermission } from '../../hooks/usePermissions';
export interface WorkflowSectionConfig {
  sectionId: string;
  bundle: 'intake' | 'approval' | 'finance';
}

export const SECTION_ORDER: string[] = [
  'my_drafts',
  'my_returned_for_revision',
  'my_waiting_manager_approval',
  'my_suspended',
  'my_in_finance_execution',
  'waiting_my_approval',
  'approved',
  'manager_suspended',
  'manager_rejected',
  'manager_returned',
  'finance_awaiting_execution',
  'finance_executing',
  'finance_proof_uploaded',
  'finance_on_hold',
  'finance_paid',
  'finance_returned',
  'my_paid_completed',
  'my_rejected',
];

export function hasWorkflowViewAccess(user: UserDto): boolean {
  return hasPermission(user, PERMISSION_KEYS.committeeDecisionsView)
    || hasPermission(user, PERMISSION_KEYS.paymentsView);
}

export function canCreateRequest(user: UserDto): boolean {
  return hasWorkflowPermission(user, PERMISSION_KEYS.committeeDecisionsCreate);
}

export function isOrgScopedSection(sectionId: string): boolean {
  return sectionId.startsWith('waiting_')
    || sectionId.startsWith('manager_')
    || sectionId.startsWith('finance_')
    || sectionId === 'approved';
}

export function usesMyRecordsOnly(user: UserDto): boolean {
  if (user.fullAccess || (user.role === 'SuperAdmin' && user.actingOrganizationId)) return false;
  const viewScope = getGrantScope(user, PERMISSION_KEYS.committeeDecisionsView);
  return viewScope === 'my_records';
}
