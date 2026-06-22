import { apiFetch, apiJson } from './client';
import { clearSessionToken, saveSessionToken } from './session';

export interface UserGrantDto {
  permissionKey: string;
  scope: string;
}

export interface UserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  organizationId: string | null;
  organizationRoleId: string | null;
  actingOrganizationId?: string | null;
  organizationName: string | null;
  organizationStatus: string | null;
  fullAccess?: boolean;
  grants?: UserGrantDto[];
  roleGrants?: UserGrantDto[];
  overrides?: Array<{ permissionKey: string; effect: string; scope?: string | null }>;
  permissions?: string[];
}

export interface ApiError {
  error: string;
  code: string;
  details?: string[];
}

export async function login(username: string, password: string): Promise<UserDto> {
  const data = await apiJson<{ user: UserDto; sessionToken?: string }>('/api/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });

  if (data.sessionToken) {
    saveSessionToken(data.sessionToken);
  }

  try {
    return await getMe();
  } catch {
    clearSessionToken();
    throw new Error('ההתחברות נכשלה. נסה שוב.');
  }
}

export async function logout(): Promise<void> {
  const response = await apiFetch('/api/v1/auth/logout', { method: 'POST' });
  clearSessionToken();

  if (!response.ok && response.status !== 204) {
    const err = (await response.json()) as ApiError;
    throw new Error(err.error);
  }
}

export async function getMe(): Promise<UserDto> {
  const data = await apiJson<{ user: UserDto }>('/api/v1/auth/me');
  return data.user;
}
