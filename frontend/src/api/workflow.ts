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

export type HomeWidgetType =
  | 'kpi_cards'
  | 'financial_summary'
  | 'bottlenecks'
  | 'monthly_trend'
  | 'recent_activity';

export interface HomeNavigationTarget {
  targetTab: 'decisions' | 'payments';
  section?: string;
  status?: string;
  ownership?: 'mine';
  minAgeDays?: number;
}

export interface HomeKpiCard {
  kpiKey: string;
  title: string;
  subtitle: string;
  count: number;
  statusSemantic: string;
  navigationTarget: HomeNavigationTarget;
}

export interface HomeKpiCardsData {
  cards: HomeKpiCard[];
}

export interface HomeFinancialMetric {
  metricKey: string;
  title: string;
  amount: number;
  statusSemantic: string;
  navigationTarget?: HomeNavigationTarget;
}

export interface HomeFinancialSummaryData {
  metrics: HomeFinancialMetric[];
}

export interface HomeMonthlyTrendPoint {
  monthKey: string;
  labelHe: string;
  amount: number;
}

export interface HomeMonthlyTrendData {
  subtitle: string;
  points: HomeMonthlyTrendPoint[];
}

export interface HomeWidget {
  id: string;
  type: HomeWidgetType;
  title: string;
  data?: unknown;
  navigationTarget?: HomeNavigationTarget;
}

export function parseKpiCardsWidget(widget: HomeWidget): HomeKpiCard[] {
  if (widget.type !== 'kpi_cards' || !widget.data || typeof widget.data !== 'object') {
    return [];
  }
  const data = widget.data as HomeKpiCardsData;
  return Array.isArray(data.cards) ? data.cards : [];
}

export function parseFinancialSummaryWidget(widget: HomeWidget): HomeFinancialMetric[] {
  if (widget.type !== 'financial_summary' || !widget.data || typeof widget.data !== 'object') {
    return [];
  }
  const data = widget.data as HomeFinancialSummaryData;
  return Array.isArray(data.metrics) ? data.metrics : [];
}

export function parseMonthlyTrendWidget(widget: HomeWidget): HomeMonthlyTrendData | null {
  if (widget.type !== 'monthly_trend' || !widget.data || typeof widget.data !== 'object') {
    return null;
  }
  const data = widget.data as HomeMonthlyTrendData;
  if (!Array.isArray(data.points)) return null;
  return {
    subtitle: typeof data.subtitle === 'string' ? data.subtitle : '',
    points: data.points,
  };
}

export interface HomeDashboard {
  generatedAt: string;
  widgets: HomeWidget[];
}

export interface WorkflowDashboardResponse {
  awaitingMyAction: AwaitingMyActionSummary;
  sections: WorkflowSectionSummary[];
  home: HomeDashboard;
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
