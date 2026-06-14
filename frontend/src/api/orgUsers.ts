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
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  isSelf: boolean;
}

export interface OrgUserListResponse {
  summary: OrgUserSummary;
  users: OrgUserDto[];
}

export type AssignableRole = 'Coordinator' | 'Manager' | 'Finance';

export const assignableRoles: AssignableRole[] = ['Coordinator', 'Manager', 'Finance'];

export interface CreateOrgUserPayload {
  username: string;
  password: string;
  fullName: string;
  role: AssignableRole;
}

export interface UpdateOrgUserPayload {
  fullName?: string;
  role?: AssignableRole;
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
