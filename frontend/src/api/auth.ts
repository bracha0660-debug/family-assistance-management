export interface UserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  organizationId: string | null;
  organizationName: string | null;
  organizationStatus: string | null;
}

export interface ApiError {
  error: string;
  code: string;
  details?: string[];
}

const baseUrl = import.meta.env.VITE_API_URL ?? '';

async function parseError(response: Response): Promise<ApiError> {
  try {
    return (await response.json()) as ApiError;
  } catch {
    return { error: 'שגיאת מערכת', code: 'INTERNAL_ERROR' };
  }
}

export async function login(username: string, password: string): Promise<UserDto> {
  const response = await fetch(`${baseUrl}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  const data = (await response.json()) as { user: UserDto };
  return data.user;
}

export async function logout(): Promise<void> {
  const response = await fetch(`${baseUrl}/api/v1/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  });

  if (!response.ok && response.status !== 204) {
    const err = await parseError(response);
    throw new Error(err.error);
  }
}

export async function getMe(): Promise<UserDto> {
  const response = await fetch(`${baseUrl}/api/v1/auth/me`, {
    credentials: 'include',
  });

  if (!response.ok) {
    const err = await parseError(response);
    throw new Error(err.error);
  }

  const data = (await response.json()) as { user: UserDto };
  return data.user;
}
