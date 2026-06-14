import { apiJson } from './client';

export interface FamilySummary {
  total: number;
  active: number;
  inactive: number;
}

export interface FamilyDto {
  id: string;
  familyCode: string;
  headOfHouseholdName: string;
  headIdNumber: string | null;
  phone: string | null;
  address: string | null;
  householdSize: number;
  assignedCoordinatorId: string;
  assignedCoordinatorName: string;
  status: string;
  notes: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface FamilyListResponse {
  summary: FamilySummary;
  families: FamilyDto[];
}

export interface CreateFamilyPayload {
  headOfHouseholdName: string;
  headIdNumber?: string | null;
  phone?: string | null;
  address?: string | null;
  householdSize?: number | null;
  notes?: string | null;
}

export interface UpdateFamilyPayload {
  headOfHouseholdName?: string;
  headIdNumber?: string | null;
  phone?: string | null;
  address?: string | null;
  householdSize?: number | null;
  notes?: string | null;
}

export async function listFamilies(): Promise<FamilyListResponse> {
  return apiJson<FamilyListResponse>('/api/v1/org/families');
}

export async function createFamily(payload: CreateFamilyPayload): Promise<FamilyDto> {
  const data = await apiJson<{ family: FamilyDto }>('/api/v1/org/families', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return data.family;
}

export async function updateFamily(
  id: string,
  version: number,
  payload: UpdateFamilyPayload,
): Promise<FamilyDto> {
  const data = await apiJson<{ family: FamilyDto }>(`/api/v1/org/families/${id}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify(payload),
  });
  return data.family;
}

export async function deactivateFamily(
  id: string,
  version: number,
  reason: string,
): Promise<FamilyDto> {
  const data = await apiJson<{ family: FamilyDto }>(`/api/v1/org/families/${id}/deactivate`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.family;
}
