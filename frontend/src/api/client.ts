import type { ApiError } from './auth';
import { clearSessionToken, getSessionToken, sessionHeaderName } from './session';

const baseUrl = import.meta.env.VITE_API_URL ?? '';

type SessionExpiredHandler = () => void;

let onSessionExpired: SessionExpiredHandler | null = null;

export function setSessionExpiredHandler(handler: SessionExpiredHandler | null): void {
  onSessionExpired = handler;
}

export function getApiBaseUrl(): string {
  return baseUrl;
}

async function parseError(response: Response): Promise<ApiError> {
  try {
    return (await response.json()) as ApiError;
  } catch {
    return { error: 'שגיאת מערכת', code: 'INTERNAL_ERROR' };
  }
}

export async function apiFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers);
  const token = getSessionToken();
  if (token) {
    headers.set(sessionHeaderName, token);
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers,
    credentials: 'include',
  });

  if (response.status === 401) {
    clearSessionToken();
    onSessionExpired?.();
  }

  return response;
}

export async function apiJson<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await apiFetch(path, init);
  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
