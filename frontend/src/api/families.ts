import { apiJson } from './client';

export interface FamilySummary {
  total: number;
  active: number;
  inactive: number;
}

export interface FamilyDto {
  id: string;
  familyCode: string;
  accountingCode: number;
  accountingCoordinatorId: string;
  familyLastName: string;
  fatherName: string | null;
  fatherIsraeliId: string | null;
  motherName: string | null;
  motherIsraeliId: string | null;
  phone: string | null;
  address: string | null;
  bankNumber: string | null;
  branchNumber: string | null;
  accountNumber: string | null;
  accountHolderName: string | null;
  assignedCoordinatorId: string;
  assignedCoordinatorName: string;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface FamilyListResponse {
  summary: FamilySummary;
  families: FamilyDto[];
}

export interface SuggestedAccountingCodeResponse {
  accountingCoordinatorId: string;
  suggestedAccountingCode: number;
}

export interface CreateFamilyPayload {
  familyLastName: string;
  accountingCode?: number | null;
  assignedCoordinatorId?: string | null;
  fatherName?: string | null;
  fatherIsraeliId?: string | null;
  motherName?: string | null;
  motherIsraeliId?: string | null;
  phone?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
}

export interface UpdateFamilyPayload {
  familyLastName?: string;
  accountingCode?: number | null;
  fatherName?: string | null;
  fatherIsraeliId?: string | null;
  motherName?: string | null;
  motherIsraeliId?: string | null;
  phone?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  assignedCoordinatorId?: string | null;
  reason?: string | null;
}

export async function listFamilies(): Promise<FamilyListResponse> {
  return apiJson<FamilyListResponse>('/api/v1/org/families');
}

export async function getSuggestedAccountingCode(
  coordinatorId: string,
): Promise<SuggestedAccountingCodeResponse> {
  return apiJson<SuggestedAccountingCodeResponse>(
    `/api/v1/org/families/suggested-accounting-code?coordinatorId=${encodeURIComponent(coordinatorId)}`,
  );
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

export async function restoreFamily(
  id: string,
  version: number,
  reason: string,
): Promise<FamilyDto> {
  const data = await apiJson<{ family: FamilyDto }>(`/api/v1/org/families/${id}/restore`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.family;
}

export function maskBankAccount(family: Pick<FamilyDto, 'bankNumber' | 'branchNumber' | 'accountNumber'>): string {
  if (!family.accountNumber) return '—';
  const last4 = family.accountNumber.slice(-4);
  const bank = family.bankNumber || '**';
  const branch = family.branchNumber || '***';
  return `${bank}-${branch}-****${last4}`;
}
