import { apiFetch } from './client';

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
  accountingCode: string | null;
  email: string | null;
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

export interface InactiveSupplierConflictDetails {
  existingSupplierId: string;
  existingSupplierCode: string;
  existingSupplierName: string;
  existingVersion: number;
}

export interface CreateSupplierPayload {
  name: string;
  registrationNumber?: string | null;
  phone?: string | null;
  accountingCode: string;
  email?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  acknowledgeInactiveDuplicate?: boolean;
}

export interface UpdateSupplierPayload {
  name?: string;
  registrationNumber?: string | null;
  phone?: string | null;
  accountingCode?: string | null;
  email?: string | null;
  address?: string | null;
  bankNumber?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  reason?: string | null;
}

interface SupplierApiErrorBody {
  error: string;
  code: string;
  details?: InactiveSupplierConflictDetails | string[];
}

export class SupplierApiError extends Error {
  readonly code: string;
  readonly inactiveConflict?: InactiveSupplierConflictDetails;

  constructor(message: string, code: string, inactiveConflict?: InactiveSupplierConflictDetails) {
    super(message);
    this.name = 'SupplierApiError';
    this.code = code;
    this.inactiveConflict = inactiveConflict;
  }
}

function isInactiveConflictDetails(value: unknown): value is InactiveSupplierConflictDetails {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const record = value as Record<string, unknown>;
  return typeof record.existingSupplierId === 'string'
    && typeof record.existingSupplierCode === 'string'
    && typeof record.existingSupplierName === 'string'
    && typeof record.existingVersion === 'number';
}

async function parseSupplierApiError(response: Response): Promise<never> {
  let body: SupplierApiErrorBody = { error: 'שגיאת מערכת', code: 'INTERNAL_ERROR' };
  try {
    body = (await response.json()) as SupplierApiErrorBody;
  } catch {
    // keep default
  }

  if (body.code === 'INACTIVE_SUPPLIER_SAME_REGISTRATION' && isInactiveConflictDetails(body.details)) {
    throw new SupplierApiError(body.error, body.code, body.details);
  }

  if (body.code === 'DUPLICATE_REGISTRATION_NUMBER') {
    throw new SupplierApiError(body.error, body.code);
  }

  throw new Error(body.error);
}

export async function listSuppliers(): Promise<SupplierListResponse> {
  return apiFetch('/api/v1/org/suppliers').then(async (response) => {
    if (!response.ok) throw new Error('שגיאת מערכת');
    return (await response.json()) as SupplierListResponse;
  });
}

export async function createSupplier(payload: CreateSupplierPayload): Promise<SupplierDto> {
  const response = await apiFetch('/api/v1/org/suppliers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    await parseSupplierApiError(response);
  }

  const data = (await response.json()) as { supplier: SupplierDto };
  return data.supplier;
}

export async function updateSupplier(
  id: string,
  version: number,
  payload: UpdateSupplierPayload,
): Promise<SupplierDto> {
  const response = await apiFetch(`/api/v1/org/suppliers/${id}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    await parseSupplierApiError(response);
  }

  const data = (await response.json()) as { supplier: SupplierDto };
  return data.supplier;
}

export async function deactivateSupplier(
  id: string,
  version: number,
  reason: string,
): Promise<SupplierDto> {
  const response = await apiFetch(`/api/v1/org/suppliers/${id}/deactivate`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    await parseSupplierApiError(response);
  }

  const data = (await response.json()) as { supplier: SupplierDto };
  return data.supplier;
}

export async function restoreSupplier(
  id: string,
  version: number,
  reason: string,
): Promise<SupplierDto> {
  const response = await apiFetch(`/api/v1/org/suppliers/${id}/restore`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    await parseSupplierApiError(response);
  }

  const data = (await response.json()) as { supplier: SupplierDto };
  return data.supplier;
}

export function maskSupplierBank(s: Pick<SupplierDto, 'bankNumber' | 'branchNumber' | 'accountNumber'>): string {
  if (!s.accountNumber) return '—';
  const last4 = s.accountNumber.slice(-4);
  return `${s.bankNumber || '**'}-${s.branchNumber || '***'}-****${last4}`;
}
