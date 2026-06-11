import type { ApiError } from './auth';

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

const baseUrl = import.meta.env.VITE_API_URL ?? '';

async function parseError(response: Response): Promise<ApiError> {
  try {
    return (await response.json()) as ApiError;
  } catch {
    return { error: 'שגיאת מערכת', code: 'INTERNAL_ERROR' };
  }
}

export async function listOrganizations(): Promise<OrganizationListResponse> {
  const response = await fetch(`${baseUrl}/api/v1/admin/organizations`, {
    credentials: 'include',
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  return (await response.json()) as OrganizationListResponse;
}

export async function createOrganization(name: string, code: string): Promise<OrganizationDto> {
  const response = await fetch(`${baseUrl}/api/v1/admin/organizations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ name, code }),
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  const data = (await response.json()) as { organization: OrganizationDto };
  return data.organization;
}

export async function suspendOrganization(
  id: string,
  version: number,
  reason: string,
): Promise<OrganizationDto> {
  const response = await fetch(`${baseUrl}/api/v1/admin/organizations/${id}/suspend`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    credentials: 'include',
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  const data = (await response.json()) as { organization: OrganizationDto };
  return data.organization;
}

export async function bootstrapOrgAdmin(
  organizationId: string,
  username: string,
  password: string,
  fullName: string,
): Promise<BootstrapUserDto> {
  const response = await fetch(`${baseUrl}/api/v1/admin/organizations/${organizationId}/admin`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ username, password, fullName }),
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  const data = (await response.json()) as { user: BootstrapUserDto };
  return data.user;
}
