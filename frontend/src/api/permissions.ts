import { apiJson } from './client';

export interface PermissionCatalogItem {
  permissionKey: string;
  category: string;
  displayNameHe: string;
  descriptionHe: string | null;
  sortOrder: number;
  supportsMyRecords: boolean;
  scopeApplies: boolean;
}

export interface RoleGrant {
  permissionKey: string;
  scope: string;
}

export interface OrganizationRoleListItem {
  id: string;
  name: string;
  description: string | null;
  status: string;
  factoryPresetKey: string | null;
  version: number;
  userCount: number;
}

export interface OrganizationRoleDetail {
  id: string;
  name: string;
  description: string | null;
  status: string;
  factoryPresetKey: string | null;
  version: number;
  grants: RoleGrant[];
}

export async function getPermissionCatalog(): Promise<PermissionCatalogItem[]> {
  const data = await apiJson<{ catalog: PermissionCatalogItem[] }>('/api/v1/org/permissions/catalog');
  return data.catalog;
}

export async function listOrgRoles(): Promise<OrganizationRoleListItem[]> {
  const data = await apiJson<{ roles: OrganizationRoleListItem[] }>('/api/v1/org/roles');
  return data.roles;
}

export async function getOrgRole(roleId: string): Promise<OrganizationRoleDetail> {
  const data = await apiJson<{ role: OrganizationRoleDetail }>(`/api/v1/org/roles/${roleId}`);
  return data.role;
}

export async function updateRoleGrants(
  roleId: string,
  grants: RoleGrant[],
  reason: string,
): Promise<OrganizationRoleDetail> {
  const data = await apiJson<{ role: OrganizationRoleDetail }>(`/api/v1/org/roles/${roleId}/grants`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ grants, reason }),
  });
  return data.role;
}

export async function resetRoleGrants(roleId: string, reason: string): Promise<OrganizationRoleDetail> {
  const data = await apiJson<{ role: OrganizationRoleDetail }>(`/api/v1/org/roles/${roleId}/grants/reset`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  });
  return data.role;
}

export const PERMISSION_KEYS = {
  familiesView: 'families.view',
  familiesCreate: 'families.create',
  familiesEdit: 'families.edit',
  familiesDeactivate: 'families.deactivate',
  familiesRestore: 'families.restore',
  familiesExport: 'families.export',
  suppliersView: 'suppliers.view',
  suppliersCreate: 'suppliers.create',
  suppliersEdit: 'suppliers.edit',
  suppliersDeactivate: 'suppliers.deactivate',
  suppliersRestore: 'suppliers.restore',
  suppliersExport: 'suppliers.export',
  assistanceTypesView: 'assistance_types.view',
  assistanceTypesCreate: 'assistance_types.create',
  assistanceTypesEdit: 'assistance_types.edit',
  assistanceTypesDeactivate: 'assistance_types.deactivate',
  assistanceTypesRestore: 'assistance_types.restore',
  committeeDecisionsView: 'committee_decisions.view',
  committeeDecisionsCreate: 'committee_decisions.create',
  committeeDecisionsEditDraft: 'committee_decisions.edit_draft',
  committeeDecisionsSubmit: 'committee_decisions.submit',
  committeeDecisionsApprove: 'committee_decisions.approve',
  committeeDecisionsReject: 'committee_decisions.reject',
  committeeDecisionsCancel: 'committee_decisions.cancel',
  assistanceItemsView: 'assistance_items.view',
  assistanceItemsCreate: 'assistance_items.create',
  assistanceItemsEdit: 'assistance_items.edit',
  assistanceItemsRemoveDraft: 'assistance_items.remove_draft',
  paymentsView: 'payments.view',
  paymentsExecute: 'payments.execute',
  paymentsUploadProof: 'payments.upload_proof',
  paymentsMarkPaid: 'payments.mark_paid',
  paymentsReturnToCoordinator: 'payments.return_to_coordinator',
} as const;

export function translatePermissionCategory(category: string): string {
  switch (category) {
    case 'families':
      return 'משפחות';
    case 'suppliers':
      return 'ספקים';
    case 'assistance_types':
      return 'סוגי סיוע';
    case 'committee_decisions':
      return 'החלטות ועדה';
    case 'assistance_items':
      return 'פריטי סיוע';
    case 'payments':
      return 'תשלומים';
    default:
      return category;
  }
}
