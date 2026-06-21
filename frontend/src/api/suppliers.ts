import { apiJson } from './client';

export interface SupplierSummary {
  total: number;
  active: number;
  inactive: number;
}

export interface SupplierDto {
  id: string;
  supplierCode: string;
  name: string;
  registrationNumber: string | null;
  phone: string | null;
  address: string | null;
  bankNumber: string | null;
  branchNumber: string | null;
  accountNumber: string | null;
  accountHolderName: string | null;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface SupplierListResponse {
  summary: SupplierSummary;
  suppliers: SupplierDto[];
}

export interface CreateSupplierPayload {
  name: string;
  registrationNumber?: string | null;
  phone?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
}

export interface UpdateSupplierPayload {
  name?: string;
  registrationNumber?: string | null;
  phone?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  reason?: string | null;
}

export async function listSuppliers(): Promise<SupplierListResponse> {
  return apiJson<SupplierListResponse>('/api/v1/org/suppliers');
}

export async function createSupplier(payload: CreateSupplierPayload): Promise<SupplierDto> {
  const data = await apiJson<{ supplier: SupplierDto }>('/api/v1/org/suppliers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return data.supplier;
}

export async function updateSupplier(
  id: string,
  version: number,
  payload: UpdateSupplierPayload,
): Promise<SupplierDto> {
  const data = await apiJson<{ supplier: SupplierDto }>(`/api/v1/org/suppliers/${id}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify(payload),
  });
  return data.supplier;
}

export async function deactivateSupplier(
  id: string,
  version: number,
  reason: string,
): Promise<SupplierDto> {
  const data = await apiJson<{ supplier: SupplierDto }>(`/api/v1/org/suppliers/${id}/deactivate`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.supplier;
}

export async function restoreSupplier(
  id: string,
  version: number,
  reason: string,
): Promise<SupplierDto> {
  const data = await apiJson<{ supplier: SupplierDto }>(`/api/v1/org/suppliers/${id}/restore`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });
  return data.supplier;
}

export function maskSupplierBank(s: Pick<SupplierDto, 'bankNumber' | 'branchNumber' | 'accountNumber'>): string {
  if (!s.accountNumber) return '—';
  const last4 = s.accountNumber.slice(-4);
  return `${s.bankNumber || '**'}-${s.branchNumber || '***'}-****${last4}`;
}
