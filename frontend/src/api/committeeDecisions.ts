import { apiJson } from './client';

export type PaymentTarget = 'family' | 'supplier' | 'other';
export type PaymentMethod = 'bank_transfer' | 'check' | 'vouchers';

export const PAYMENT_TARGETS: PaymentTarget[] = ['family', 'supplier', 'other'];
export const PAYMENT_METHODS: PaymentMethod[] = ['bank_transfer', 'check', 'vouchers'];

export interface AssistanceItemDto {
  id: string;
  lineNumber: number;
  assistanceTypeId: string;
  assistanceTypeName: string;
  description: string | null;
  amount: number;
  paymentTarget: PaymentTarget | string;
  paymentMethod: PaymentMethod | string;
  supplierId: string | null;
  supplierName: string | null;
  payeeName: string | null;
  voucherType: string | null;
  isUrgent: boolean;
  executionStatus: string;
  paymentSummary?: {
    paymentId: string | null;
    status: string | null;
    returnReason: string | null;
    executedAt: string | null;
    proofUploadedAt: string | null;
    paidAt: string | null;
    proofFileName: string | null;
  } | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface CommitteeDecisionDto {
  id: string;
  decisionCode: string;
  familyId: string;
  familyCode: string;
  familyLastName: string;
  meetingDate: string;
  summary: string | null;
  status: string;
  createdByUserId: string;
  createdByUserName: string;
  totalAmount: number;
  rejectionReason: string | null;
  suspendReason: string | null;
  returnReason: string | null;
  cancelReason: string | null;
  approvalNotes: string | null;
  submittedAt: string | null;
  approvedAt: string | null;
  rejectedAt: string | null;
  suspendedAt: string | null;
  resumedAt: string | null;
  cancelledAt: string | null;
  workflowPhase: string;
  isOwnedByCurrentUser: boolean;
  availableActions: string[];
  version: number;
  createdAt: string;
  updatedAt: string;
  items: AssistanceItemDto[];
}

export interface CommitteeDecisionSummary {
  total: number;
  draft: number;
  submitted: number;
  approved: number;
}

export interface CommitteeDecisionListResponse {
  summary: CommitteeDecisionSummary;
  decisions: CommitteeDecisionDto[];
}

export interface CreateCommitteeDecisionPayload {
  familyId: string;
  meetingDate: string;
  summary?: string | null;
}

export interface UpdateCommitteeDecisionPayload {
  meetingDate?: string;
  summary?: string | null;
}

export interface CreateAssistanceItemPayload {
  assistanceTypeId: string;
  description?: string | null;
  amount: number;
  paymentTarget: PaymentTarget;
  paymentMethod: PaymentMethod;
  supplierId?: string | null;
  payeeName?: string | null;
  voucherType?: string | null;
  isUrgent?: boolean;
}

export interface UpdateAssistanceItemPayload {
  assistanceTypeId?: string;
  description?: string | null;
  amount?: number;
  paymentTarget?: PaymentTarget;
  paymentMethod?: PaymentMethod;
  supplierId?: string | null;
  clearSupplierId?: boolean;
  payeeName?: string | null;
  voucherType?: string | null;
  isUrgent?: boolean;
}

export interface CommitteeDecisionListOptions {
  section?: string;
  status?: string;
  ownership?: 'mine';
  minAgeDays?: number;
}

export async function listCommitteeDecisions(
  options?: CommitteeDecisionListOptions,
): Promise<CommitteeDecisionListResponse> {
  const params = new URLSearchParams();
  if (options?.section) params.set('section', options.section);
  if (options?.status) params.set('status', options.status);
  if (options?.ownership) params.set('ownership', options.ownership);
  if (options?.minAgeDays) params.set('minAgeDays', String(options.minAgeDays));
  const qs = params.toString();
  const path = qs ? `/api/v1/org/committee-decisions?${qs}` : '/api/v1/org/committee-decisions';
  return apiJson<CommitteeDecisionListResponse>(path);
}

export async function getCommitteeDecision(id: string): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(`/api/v1/org/committee-decisions/${id}`);
  return data.decision;
}

export async function createCommitteeDecision(
  payload: CreateCommitteeDecisionPayload,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>('/api/v1/org/committee-decisions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  return data.decision;
}

export async function updateCommitteeDecision(
  id: string,
  version: number,
  payload: UpdateCommitteeDecisionPayload,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(`/api/v1/org/committee-decisions/${id}`, {
    method: 'PATCH',
    headers: {
      'Content-Type': 'application/json',
      'If-Match': String(version),
    },
    body: JSON.stringify(payload),
  });
  return data.decision;
}

export async function submitCommitteeDecision(
  id: string,
  version: number,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/submit`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({}),
    },
  );
  return data.decision;
}

export async function approveCommitteeDecision(
  id: string,
  version: number,
  reason?: string | null,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/approve`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason: reason ?? null }),
    },
  );
  return data.decision;
}

export async function rejectCommitteeDecision(
  id: string,
  version: number,
  reason: string,
  returnForRevision: boolean,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/reject`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason, returnForRevision }),
    },
  );
  return data.decision;
}

export async function cancelCommitteeDecision(
  id: string,
  version: number,
  reason: string,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/cancel`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason }),
    },
  );
  return data.decision;
}

export async function suspendCommitteeDecision(
  id: string,
  version: number,
  reason: string,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/suspend`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason }),
    },
  );
  return data.decision;
}

export async function resumeCommitteeDecision(
  id: string,
  version: number,
  reason?: string | null,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${id}/resume`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason: reason ?? null }),
    },
  );
  return data.decision;
}

export async function addAssistanceItem(
  decisionId: string,
  version: number,
  payload: CreateAssistanceItemPayload,
): Promise<{ item: AssistanceItemDto; decisionVersion: number }> {
  const data = await apiJson<{ item: AssistanceItemDto; decisionVersion: number }>(
    `/api/v1/org/committee-decisions/${decisionId}/items`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify(payload),
    },
  );
  return { item: data.item, decisionVersion: data.decisionVersion };
}

export async function updateAssistanceItem(
  decisionId: string,
  itemId: string,
  version: number,
  payload: UpdateAssistanceItemPayload,
): Promise<AssistanceItemDto> {
  const data = await apiJson<{ item: AssistanceItemDto }>(
    `/api/v1/org/committee-decisions/${decisionId}/items/${itemId}`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify(payload),
    },
  );
  return data.item;
}

export async function removeAssistanceItem(
  decisionId: string,
  itemId: string,
  version: number,
): Promise<CommitteeDecisionDto> {
  const data = await apiJson<{ decision: CommitteeDecisionDto }>(
    `/api/v1/org/committee-decisions/${decisionId}/items/${itemId}`,
    {
      method: 'DELETE',
      headers: { 'If-Match': String(version) },
    },
  );
  return data.decision;
}
