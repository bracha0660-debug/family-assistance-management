const SESSION_STORAGE_KEY = 'FAM.Session';

export function saveSessionToken(token: string): void {
  sessionStorage.setItem(SESSION_STORAGE_KEY, token);
}

export function getSessionToken(): string | null {
  return sessionStorage.getItem(SESSION_STORAGE_KEY);
}

export function clearSessionToken(): void {
  sessionStorage.removeItem(SESSION_STORAGE_KEY);
}

export const sessionHeaderName = 'X-FAM-Session';
