import { apiJson } from './client';

export interface ActivityLogEntryDto {
  id: string;
  createdAt: string;
  eventCode: string;
  actorUserId: string;
  actorUsername: string;
  actorFullName: string;
  entityType: string;
  entityId: string;
  action: string;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
}

export interface ActivityLogListResponse {
  entries: ActivityLogEntryDto[];
  returnedCount: number;
}

export interface ListActivityOptions {
  limit?: number;
  offset?: number;
}

export async function listOrgActivity(options: ListActivityOptions = {}): Promise<ActivityLogListResponse> {
  const params = new URLSearchParams();
  if (typeof options.limit === 'number') params.set('limit', String(options.limit));
  if (typeof options.offset === 'number') params.set('offset', String(options.offset));
  const qs = params.toString();
  const path = qs ? `/api/v1/org/activity?${qs}` : '/api/v1/org/activity';
  return apiJson<ActivityLogListResponse>(path);
}
