import { apiFetch, apiJson } from './client';

export interface PaymentQueueSummary {
  total: number;
  awaitingPayment: number;
  executing: number;
  proofUploaded: number;
  onHold: number;
}

export interface PaymentQueueItemDto {
  id: string;
  committeeDecisionId: string;
  decisionCode: string;
  assistanceItemId: string;
  lineNumber: number;
  familyId: string;
  familyCode: string;
  familyLastName: string;
  assistanceTypeName: string;
  amount: number;
  paymentTarget: string;
  paymentMethod: string;
  supplierName: string | null;
  payeeName: string | null;
  status: string;
  decisionStatus: string;
  suspendReason: string | null;
  isUrgent: boolean;
  isOnHold: boolean;
  executionReference: string | null;
  proofFileName: string | null;
  availableActions: string[];
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface PaymentQueueListResponse {
  summary: PaymentQueueSummary;
  payments: PaymentQueueItemDto[];
}

export interface PaymentListOptions {
  section?: string;
  minAgeDays?: number;
}

export async function listPayments(
  options?: string | PaymentListOptions,
): Promise<PaymentQueueListResponse> {
  const params = new URLSearchParams();
  if (typeof options === 'string') {
    if (options) params.set('section', options);
  } else if (options) {
    if (options.section) params.set('section', options.section);
    if (options.minAgeDays) params.set('minAgeDays', String(options.minAgeDays));
  }
  const qs = params.toString();
  const path = qs ? `/api/v1/org/payments?${qs}` : '/api/v1/org/payments';
  return apiJson<PaymentQueueListResponse>(path);
}

export async function executePayment(
  id: string,
  version: number,
  executionReference?: string | null,
): Promise<PaymentQueueItemDto> {
  const data = await apiJson<{ payment: PaymentQueueItemDto }>(`/api/v1/org/payments/${id}/execute`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ executionReference: executionReference ?? null }),
  });
  return data.payment;
}

export async function uploadPaymentProof(
  id: string,
  version: number,
  file: File,
): Promise<PaymentQueueItemDto> {
  const form = new FormData();
  form.append('file', file);
  const response = await apiFetch(`/api/v1/org/payments/${id}/proof`, {
    method: 'POST',
    headers: { 'If-Match': String(version) },
    body: form,
  });
  if (!response.ok) {
    const err = await response.json().catch(() => ({ error: 'שגיאת מערכת' }));
    throw new Error(err.error ?? 'שגיאת מערכת');
  }
  const data = (await response.json()) as { payment: PaymentQueueItemDto };
  return data.payment;
}

export async function markPaymentPaid(
  id: string,
  version: number,
  executionReference?: string | null,
): Promise<PaymentQueueItemDto> {
  const data = await apiJson<{ payment: PaymentQueueItemDto }>(`/api/v1/org/payments/${id}/mark-paid`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify({ executionReference: executionReference ?? null }),
  });
  return data.payment;
}

export async function returnPaymentToCoordinator(
  id: string,
  version: number,
  reason: string,
): Promise<PaymentQueueItemDto> {
  const data = await apiJson<{ payment: PaymentQueueItemDto }>(
    `/api/v1/org/payments/${id}/return-to-coordinator`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason }),
    },
  );
  return data.payment;
}
