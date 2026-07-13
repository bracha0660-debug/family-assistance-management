import { apiJson } from './client';
import type { RoleGrant } from './permissions';

export type PermissionOverrideEffect = 'grant' | 'deny';

export interface UserPermissionOverride {
  permissionKey: string;
  effect: PermissionOverrideEffect;
  scope?: string | null;
}

export interface EffectiveGrant {
  permissionKey: string;
  scope: string;
  sourceTag: 'role' | 'grant' | 'deny' | 'grant_override' | 'none';
}

export interface UserPermissionOverridesResponse {
  roleGrants: RoleGrant[];
  overrides: UserPermissionOverride[];
  effectiveGrants: EffectiveGrant[];
}

export interface UserPermissionOverrideInput {
  permissionKey: string;
  effect: PermissionOverrideEffect;
  scope?: string;
}

export function translateSourceTag(tag: EffectiveGrant['sourceTag']): string {
  switch (tag) {
    case 'role':
      return 'מתפקיד';
    case 'grant':
      return 'הענקה';
    case 'deny':
      return 'שלילה';
    case 'grant_override':
      return 'הענקה (מחליף)';
    default:
      return '';
  }
}

export function translateScope(scope: string | null | undefined): string {
  if (!scope) return '—';
  if (scope === 'my_records') return 'הרשומות שלי';
  if (scope === 'organization') return 'ארגון';
  return scope;
}

export async function getUserPermissionOverrides(userId: string): Promise<UserPermissionOverridesResponse> {
  return apiJson<UserPermissionOverridesResponse>(`/api/v1/org/users/${userId}/permission-overrides`);
}

export async function updateUserPermissionOverrides(
  userId: string,
  overrides: UserPermissionOverrideInput[],
): Promise<UserPermissionOverridesResponse> {
  return apiJson<UserPermissionOverridesResponse>(`/api/v1/org/users/${userId}/permission-overrides`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ overrides }),
  });
}

export async function deleteUserPermissionOverride(
  userId: string,
  permissionKey: string,
): Promise<UserPermissionOverridesResponse> {
  return apiJson<UserPermissionOverridesResponse>(
    `/api/v1/org/users/${userId}/permission-overrides/${encodeURIComponent(permissionKey)}`,
    { method: 'DELETE' },
  );
}
