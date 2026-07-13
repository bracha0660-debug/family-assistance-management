import { apiJson } from './client';

export interface OrgUserSummary {
  total: number;
  active: number;
  disabled: number;
}

export interface OrgUserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  organizationRoleId: string | null;
  organizationRoleName: string | null;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  isSelf: boolean;
  overrideCount?: number;
}

export interface OrgUserListResponse {
  summary: OrgUserSummary;
  users: OrgUserDto[];
}

export interface CreateOrgUserPayload {
  username: string;
  password: string;
  fullName: string;
  organizationRoleId: string;
}

export interface UpdateOrgUserPayload {
  fullName?: string;
  organizationRoleId?: string;
}

export async function listOrgUsers(): Promise<OrgUserListResponse> {
  return apiJson<OrgUserListResponse>('/api/v1/org/users');
}

export async function createOrgUser(payload: CreateOrgUserPayload): Promise<OrgUserDto> {
  const data = await apiJson<{ user: OrgUserDto }>('/api/v1/org/users', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return data.user;
}

export async function updateOrgUser(
  id: string,
  version: number,
  payload: UpdateOrgUserPayload,
): Promise<OrgUserDto> {
  const data = await apiJson<{ user: OrgUserDto }>(`/api/v1/org/users/${id}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify(payload),
  });
  return data.user;
}

export async function disableOrgUser(
  id: string,
  version: number,
  reason: string,
): Promise<OrgUserDto> {
  const data = await apiJson<{ user: OrgUserDto }>(`/api/v1/org/users/${id}/disable`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.user;
}

export async function restoreOrgUser(
  id: string,
  version: number,
  reason: string,
): Promise<OrgUserDto> {
  const data = await apiJson<{ user: OrgUserDto }>(`/api/v1/org/users/${id}/restore`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.user;
}

export async function resetOrgUserPassword(
  id: string,
  newPassword: string,
  reason: string,
): Promise<OrgUserDto> {
  const data = await apiJson<{ user: OrgUserDto }>(`/api/v1/org/users/${id}/reset-password`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ newPassword, reason }),
  });
  return data.user;
}
