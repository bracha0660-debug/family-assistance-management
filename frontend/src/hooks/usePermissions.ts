import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';

export function hasPermission(user: UserDto | null | undefined, key: string): boolean {
  if (!user) return false;
  if (user.fullAccess) return true;
  if (user.role === 'SuperAdmin' && user.actingOrganizationId) return true;
  return user.permissions?.includes(key) ?? user.grants?.some((g) => g.permissionKey === key) ?? false;
}

export function canWriteFamilies(user: UserDto | null | undefined): boolean {
  return hasPermission(user, PERMISSION_KEYS.familiesCreate)
    || hasPermission(user, PERMISSION_KEYS.familiesEdit)
    || hasPermission(user, PERMISSION_KEYS.familiesDeactivate);
}

export function usesMyRecordsFamilyScope(user: UserDto | null | undefined): boolean {
  if (!user || user.fullAccess) return false;
  const viewScope = getGrantScope(user, PERMISSION_KEYS.familiesView);
  const editScope = getGrantScope(user, PERMISSION_KEYS.familiesEdit);
  return viewScope === 'my_records' || (viewScope === null && editScope === 'my_records');
}

export function getGrantScope(user: UserDto | null | undefined, key: string): string | null {
  if (!user?.grants) return null;
  return user.grants.find((g) => g.permissionKey === key)?.scope ?? null;
}

export function usePermissions(user: UserDto | null | undefined) {
  return {
    has: (key: string) => hasPermission(user, key),
    scope: (key: string) => getGrantScope(user, key),
    fullAccess: user?.fullAccess ?? user?.role === 'OrganizationAdministrator',
  };
}
