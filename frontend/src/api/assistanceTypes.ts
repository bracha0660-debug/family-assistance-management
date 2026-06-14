import { apiJson } from './client';

export type AssistanceFrequency = 'one_time' | 'monthly' | 'quarterly' | 'annual';

export const assistanceFrequencies: AssistanceFrequency[] = [
  'one_time',
  'monthly',
  'quarterly',
  'annual',
];

export interface AssistanceTypeSummary {
  total: number;
  active: number;
  inactive: number;
}

export interface AssistanceTypeDto {
  id: string;
  typeCode: string;
  name: string;
  description: string | null;
  defaultAmount: number | null;
  currency: string;
  frequency: AssistanceFrequency | string;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface AssistanceTypeListResponse {
  summary: AssistanceTypeSummary;
  assistanceTypes: AssistanceTypeDto[];
}

export interface CreateAssistanceTypePayload {
  typeCode: string;
  name: string;
  description?: string | null;
  defaultAmount?: number | null;
  frequency: AssistanceFrequency;
}

export interface UpdateAssistanceTypePayload {
  name?: string;
  description?: string | null;
  defaultAmount?: number | null;
  clearDefaultAmount?: boolean;
  frequency?: AssistanceFrequency;
}

export async function listAssistanceTypes(): Promise<AssistanceTypeListResponse> {
  return apiJson<AssistanceTypeListResponse>('/api/v1/org/assistance-types');
}

export async function createAssistanceType(
  payload: CreateAssistanceTypePayload,
): Promise<AssistanceTypeDto> {
  const data = await apiJson<{ assistanceType: AssistanceTypeDto }>(
    '/api/v1/org/assistance-types',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  );
  return data.assistanceType;
}

export async function updateAssistanceType(
  id: string,
  version: number,
  payload: UpdateAssistanceTypePayload,
): Promise<AssistanceTypeDto> {
  const data = await apiJson<{ assistanceType: AssistanceTypeDto }>(
    `/api/v1/org/assistance-types/${id}`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify(payload),
    },
  );
  return data.assistanceType;
}

export async function deactivateAssistanceType(
  id: string,
  version: number,
  reason: string,
): Promise<AssistanceTypeDto> {
  const data = await apiJson<{ assistanceType: AssistanceTypeDto }>(
    `/api/v1/org/assistance-types/${id}/deactivate`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        'If-Match': String(version),
      },
      body: JSON.stringify({ reason }),
    },
  );
  return data.assistanceType;
}
