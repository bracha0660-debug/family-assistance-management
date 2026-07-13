import { apiJson } from './client';

export interface AssistanceItemListDto {
  id: string;
  status: string;
  availableActions: string[];
  decisionId: string;
  decisionCode: string;
  familyId: string;
  familyCode: string;
  familyAccountingCode: number;
  familyName: string;
  assistanceTypeId: string;
  assistanceTypeName: string;
  assistanceTypeCode: string;
  amount: number;
  originalApprovedAmount: number | null;
  previousPaymentAmount: number | null;
  amountAdjustmentReason: string | null;
  amountAdjustmentExplanation: string | null;
  hasAmountAdjustment: boolean;
  description: string | null;
  paymentTarget: string;
  paymentMethod: string;
  supplierId: string | null;
  supplierName: string | null;
  supplierAccountingCode: string | null;
  payeeName: string | null;
  transferBankNumber: string | null;
  transferBranchNumber: string | null;
  transferAccountNumber: string | null;
  accountHolderName: string | null;
  voucherType: string | null;
  isUrgent: boolean;
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  approvedAt: string | null;
  executionReference: string | null;
  paymentExecutionId: string | null;
  version: number;
}

export interface AssistanceItemListResponse {
  items: AssistanceItemListDto[];
}

export interface AssistanceItemWorkflowResponse {
  item: AssistanceItemListDto;
}

export interface AssistanceItemListOptions {
  status?: string;
  section?: string;
  ownership?: 'mine';
  minAgeDays?: number;
  limit?: number;
  offset?: number;
}

export async function listAssistanceItems(
  options?: AssistanceItemListOptions,
): Promise<AssistanceItemListResponse> {
  const params = new URLSearchParams();
  if (options?.status) params.set('status', options.status);
  if (options?.section) params.set('section', options.section);
  if (options?.ownership) params.set('ownership', options.ownership);
  if (options?.minAgeDays) params.set('minAgeDays', String(options.minAgeDays));
  if (options?.limit) params.set('limit', String(options.limit));
  if (options?.offset) params.set('offset', String(options.offset));
  const qs = params.toString();
  const path = qs ? `/api/v1/org/assistance-items?${qs}` : '/api/v1/org/assistance-items';
  return apiJson<AssistanceItemListResponse>(path);
}

function transitionHeaders(version: number): HeadersInit {
  return {
    'Content-Type': 'application/json',
    'If-Match': String(version),
  };
}

export async function approveAssistanceItem(
  id: string,
  version: number,
  reason?: string | null,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/approve`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({ reason: reason ?? null }),
    },
  );
  return data.item;
}

export async function rejectAssistanceItem(
  id: string,
  version: number,
  reason: string,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/reject`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({ reason }),
    },
  );
  return data.item;
}

export async function returnAssistanceItem(
  id: string,
  version: number,
  reason: string,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/return`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({ reason }),
    },
  );
  return data.item;
}

export async function suspendAssistanceItem(
  id: string,
  version: number,
  reason: string,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/suspend`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({ reason }),
    },
  );
  return data.item;
}

export async function resubmitAssistanceItem(
  id: string,
  version: number,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/resubmit`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({}),
    },
  );
  return data.item;
}

export async function completeAssistanceItem(
  id: string,
  version: number,
): Promise<AssistanceItemListDto> {
  const data = await apiJson<AssistanceItemWorkflowResponse>(
    `/api/v1/org/assistance-items/${id}/complete`,
    {
      method: 'POST',
      headers: transitionHeaders(version),
      body: JSON.stringify({}),
    },
  );
  return data.item;
}

export interface AssistanceItemHistoryFieldChangeDto {
  id: string;
  fieldKey: string;
  fieldLabelHe: string;
  previousValue: string | null;
  newValue: string | null;
  valueType: string;
  isSensitive: boolean;
}

export interface AssistanceItemHistoryEventDto {
  id: string;
  assistanceItemId: string;
  eventType: string;
  eventDescriptionHe: string;
  actorUserId: string | null;
  actorDisplayName: string;
  relatedEntityType: string | null;
  relatedEntityId: string | null;
  reason: string | null;
  occurredAt: string;
  fieldChanges: AssistanceItemHistoryFieldChangeDto[];
}

export interface AssistanceItemHistoryListResponse {
  events: AssistanceItemHistoryEventDto[];
  total: number;
  limit: number;
  offset: number;
}

export async function listAssistanceItemHistory(
  assistanceItemId: string,
  options?: { limit?: number; offset?: number },
): Promise<AssistanceItemHistoryListResponse> {
  const params = new URLSearchParams();
  params.set('limit', String(options?.limit ?? 25));
  params.set('offset', String(options?.offset ?? 0));
  return apiJson<AssistanceItemHistoryListResponse>(
    `/api/v1/org/assistance-items/${assistanceItemId}/history?${params.toString()}`,
  );
}
