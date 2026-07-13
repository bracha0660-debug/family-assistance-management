import { apiFetch, apiJson } from './client';

export interface PaymentRowDto {
  assistanceItemId: string;
  paymentExecutionId: string | null;
  committeeDecisionId: string;
  decisionCode: string;
  familyId: string;
  familyCode: string;
  familyAccountingCode: number;
  familyLastName: string;
  assistanceTypeId: string;
  assistanceTypeName: string;
  assistanceTypeCode: string;
  amount: number;
  originalApprovedAmount: number | null;
  previousPaymentAmount: number | null;
  amountAdjustmentReason: string | null;
  amountAdjustmentExplanation: string | null;
  hasAmountAdjustment: boolean;
  paymentTarget: string;
  paymentMethod: string;
  description: string | null;
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
  status: string;
  executionReference: string | null;
  activeExportBatchId: string | null;
  activeExportBatchNumber: string | null;
  activeExportBatchItemId: string | null;
  eligibleForExport: boolean;
  availableActions: string[];
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface PaymentRowSummaryDto {
  total: number;
  approved: number;
  waitingForReference: number;
  paid: number;
  completed: number;
}

export interface PaymentRowListResponse {
  items: PaymentRowDto[];
  summary: PaymentRowSummaryDto;
}

export interface PaymentRowListOptions {
  status?: string;
  section?: string;
  minAgeDays?: number;
  limit?: number;
  offset?: number;
}

export interface ExportBatchSelection {
  assistanceItemId: string;
  version: number;
}

export interface ExportBatchItemDto {
  id: string;
  assistanceItemId: string;
  paymentExecutionId: string;
  exportedAmount: number;
  status: string;
  cancelReason: string | null;
  cancelledAt: string | null;
  decisionCode: string;
  familyCode: string;
  familyAccountingCode: number | null;
  familyName: string;
  assistanceTypeName: string;
  assistanceTypeCode: string;
  originalApprovedAmount: number;
  amountAdjustmentReason: string | null;
  amountAdjustmentExplanation: string | null;
  supplierName: string | null;
  supplierAccountingCode: string | null;
  paymentTarget: string;
  paymentMethod: string;
  payeeName: string | null;
  executionReference: string | null;
}

export interface ExportBatchDto {
  id: string;
  batchNumber: string;
  status: string;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  fileName: string | null;
  contentType: string | null;
  fileSizeBytes: number | null;
  generatedAt: string | null;
  totalItemCount: number;
  activeItemCount: number;
  cancelledItemCount: number;
  availableActions: string[];
  items?: ExportBatchItemDto[] | null;
}

export interface ExportBatchListResponse {
  batches: ExportBatchDto[];
}

export const AMOUNT_ADJUSTMENT_REASONS = [
  { value: 'typing_error', label: 'טעות הקלדה' },
  { value: 'quote_update', label: 'עדכון הצעת מחיר' },
  { value: 'quantity_change', label: 'שינוי כמות' },
  { value: 'other', label: 'אחר' },
] as const;

export function amountAdjustmentReasonLabel(reason: string | null | undefined): string {
  if (!reason) return '—';
  return AMOUNT_ADJUSTMENT_REASONS.find((r) => r.value === reason)?.label ?? reason;
}

export async function listPaymentRows(
  options?: PaymentRowListOptions,
): Promise<PaymentRowListResponse> {
  const params = new URLSearchParams();
  if (options?.status) params.set('status', options.status);
  if (options?.section) params.set('section', options.section);
  if (options?.minAgeDays) params.set('minAgeDays', String(options.minAgeDays));
  if (options?.limit) params.set('limit', String(options.limit));
  if (options?.offset) params.set('offset', String(options.offset));
  const qs = params.toString();
  const path = qs ? `/api/v1/org/payment-rows?${qs}` : '/api/v1/org/payment-rows';
  return apiJson<PaymentRowListResponse>(path);
}

export async function getPaymentRow(assistanceItemId: string): Promise<PaymentRowDto> {
  const data = await apiJson<{ item: PaymentRowDto }>(
    `/api/v1/org/payment-rows/${assistanceItemId}`,
  );
  return data.item;
}

export async function enterPaymentReference(
  assistanceItemId: string,
  reference: string,
): Promise<void> {
  await apiJson(`/api/v1/org/payment-rows/${assistanceItemId}/enter-reference`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reference }),
  });
}

export async function adjustPaymentAmount(
  assistanceItemId: string,
  version: number,
  newAmount: number,
  reason: string,
  explanation?: string | null,
): Promise<PaymentRowDto> {
  const data = await apiJson<{ item: PaymentRowDto }>(
    `/api/v1/org/payment-rows/${assistanceItemId}/adjust-amount`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({
        newAmount,
        reason,
        explanation: explanation ?? null,
      }),
    },
  );
  return data.item;
}

export async function editPaymentRow(
  assistanceItemId: string,
  version: number,
  fields: Record<string, string | null>,
  amountAdjustmentReason?: string | null,
  amountAdjustmentExplanation?: string | null,
): Promise<PaymentRowDto> {
  const data = await apiJson<{ item: PaymentRowDto }>(
    `/api/v1/org/payment-rows/${assistanceItemId}/edit`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({
        fields,
        amountAdjustmentReason: amountAdjustmentReason ?? null,
        amountAdjustmentExplanation: amountAdjustmentExplanation ?? null,
      }),
    },
  );
  return data.item;
}

export async function listExportBatches(): Promise<ExportBatchListResponse> {
  return apiJson<ExportBatchListResponse>('/api/v1/org/export-batches');
}

export interface ExportBatchRowValidationError {
  assistanceItemId: string;
  decisionCode: string;
  message: string;
}

export class ExportBatchCreateError extends Error {
  readonly code: string;
  readonly rowErrors: ExportBatchRowValidationError[];

  constructor(message: string, code: string, rowErrors: ExportBatchRowValidationError[]) {
    super(message);
    this.name = 'ExportBatchCreateError';
    this.code = code;
    this.rowErrors = rowErrors;
  }
}

function isRowValidationError(value: unknown): value is ExportBatchRowValidationError {
  if (!value || typeof value !== 'object') return false;
  const row = value as Record<string, unknown>;
  return typeof row.message === 'string'
    && (typeof row.decisionCode === 'string' || typeof row.DecisionCode === 'string');
}

function normalizeRowError(value: ExportBatchRowValidationError | Record<string, unknown>): ExportBatchRowValidationError {
  const row = value as Record<string, unknown>;
  return {
    assistanceItemId: String(row.assistanceItemId ?? row.AssistanceItemId ?? ''),
    decisionCode: String(row.decisionCode ?? row.DecisionCode ?? ''),
    message: String(row.message ?? row.Message ?? ''),
  };
}

export function formatExportRowValidationMessage(row: ExportBatchRowValidationError): string {
  const code = row.decisionCode.trim();
  const message = row.message.trim();
  if (code && message.startsWith(`החלטה ${code}`)) return message;
  if (code) return `החלטה ${code} — ${message}`;
  return message;
}

export async function createExportBatch(
  items: ExportBatchSelection[],
): Promise<ExportBatchDto> {
  const response = await apiFetch('/api/v1/org/export-batches', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ items }),
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({
      error: 'שגיאת מערכת',
      code: 'INTERNAL_ERROR',
    })) as {
      error?: string;
      code?: string;
      details?: unknown;
    };
    const details = body.details;
    const rowErrors = Array.isArray(details)
      ? details.filter(isRowValidationError).map((row) => normalizeRowError(row))
      : [];
    throw new ExportBatchCreateError(
      body.error ?? 'שגיאת מערכת',
      body.code ?? 'INTERNAL_ERROR',
      rowErrors,
    );
  }
  const data = (await response.json()) as { batch: ExportBatchDto };
  return data.batch;
}

export async function getExportBatch(id: string): Promise<ExportBatchDto> {
  const data = await apiJson<{ batch: ExportBatchDto }>(`/api/v1/org/export-batches/${id}`);
  return data.batch;
}

export async function cancelExportBatch(id: string, reason: string): Promise<ExportBatchDto> {
  const data = await apiJson<{ batch: ExportBatchDto }>(
    `/api/v1/org/export-batches/${id}/cancel`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason }),
    },
  );
  return data.batch;
}

export async function cancelExportBatchItem(
  batchId: string,
  itemId: string,
  reason: string,
): Promise<ExportBatchDto> {
  const data = await apiJson<{ batch: ExportBatchDto }>(
    `/api/v1/org/export-batches/${batchId}/items/${itemId}/cancel`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason }),
    },
  );
  return data.batch;
}

function parseDownloadFileName(disposition: string | null, fallback: string): string {
  if (!disposition) return fallback;
  const utfMatch = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utfMatch?.[1]) {
    try {
      return decodeURIComponent(utfMatch[1].trim());
    } catch {
      /* fall through */
    }
  }
  const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
  return plainMatch?.[1]?.trim() || fallback;
}

export async function downloadExportBatch(id: string, fallbackName?: string): Promise<void> {
  const response = await apiFetch(`/api/v1/org/export-batches/${id}/download`);
  if (!response.ok) {
    const err = await response.json().catch(() => ({ error: 'שגיאת מערכת' }));
    throw new Error((err as { error?: string }).error ?? 'שגיאת מערכת');
  }
  const blob = await response.blob();
  const fileName = parseDownloadFileName(
    response.headers.get('Content-Disposition'),
    fallbackName ?? 'export-batch.csv',
  );
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
