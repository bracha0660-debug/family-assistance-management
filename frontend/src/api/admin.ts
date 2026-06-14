import { apiJson } from './client';

export interface OrganizationSummary {
  total: number;
  active: number;
  suspended: number;
}

export interface OrganizationDto {
  id: string;
  name: string;
  code: string;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  hasOrgAdmin: boolean;
}

export interface OrganizationListResponse {
  summary: OrganizationSummary;
  organizations: OrganizationDto[];
}

export interface BootstrapUserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  organizationId: string;
  status: string;
}

export async function listOrganizations(): Promise<OrganizationListResponse> {
  return apiJson<OrganizationListResponse>('/api/v1/admin/organizations');
}

export async function createOrganization(name: string, code: string): Promise<OrganizationDto> {
  const data = await apiJson<{ organization: OrganizationDto }>('/api/v1/admin/organizations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, code }),
  });
  return data.organization;
}

export async function suspendOrganization(
  id: string,
  version: number,
  reason: string,
): Promise<OrganizationDto> {
  const data = await apiJson<{ organization: OrganizationDto }>(
    `/api/v1/admin/organizations/${id}/suspend`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason }),
    },
  );
  return data.organization;
}

export async function bootstrapOrgAdmin(
  organizationId: string,
  username: string,
  password: string,
  fullName: string,
): Promise<BootstrapUserDto> {
  const data = await apiJson<{ user: BootstrapUserDto }>(
    `/api/v1/admin/organizations/${organizationId}/admin`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password, fullName }),
    },
  );
  return data.user;
}
