import { apiJson } from './client';
import type { CommitteeDecisionDto } from './committeeDecisions';
import type { PaymentQueueItemDto } from './payments';

export interface WorkflowSectionCount {
  sectionId: string;
  title: string;
  count: number;
}

export interface AwaitingMyActionSummary {
  totalAwaitingMyAction: number;
  bySection: WorkflowSectionCount[];
}

export interface WorkflowSectionSummary {
  sectionId: string;
  title: string;
  visibility: 'mine' | 'org';
  count: number;
  awaitingActionCount: number;
  decisionPreview?: CommitteeDecisionDto[];
  paymentPreview?: PaymentQueueItemDto[];
}

export interface WorkflowDashboardResponse {
  awaitingMyAction: AwaitingMyActionSummary;
  sections: WorkflowSectionSummary[];
}

export async function getWorkflowDashboard(): Promise<WorkflowDashboardResponse> {
  return apiJson<WorkflowDashboardResponse>('/api/v1/org/workflow/dashboard');
}

export async function listSectionDecisions(
  sectionId: string,
  ownership?: 'mine' | 'all',
): Promise<CommitteeDecisionDto[]> {
  const params = new URLSearchParams({ section: sectionId });
  if (ownership) params.set('ownership', ownership);
  const data = await apiJson<{ decisions: CommitteeDecisionDto[] }>(
    `/api/v1/org/committee-decisions?${params.toString()}`,
  );
  return data.decisions;
}

export async function listSectionPayments(sectionId: string): Promise<PaymentQueueItemDto[]> {
  const data = await apiJson<{ payments: PaymentQueueItemDto[] }>(
    `/api/v1/org/payments?section=${encodeURIComponent(sectionId)}`,
  );
  return data.payments;
}
