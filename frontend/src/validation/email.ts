export function validateOptionalEmail(email: string): string | null {
  const trimmed = email.trim();
  if (trimmed.length === 0) return null;
  if (trimmed.length > 254) return 'כתובת אימייל חייבת להיות עד 254 תווים';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) return 'כתובת אימייל לא תקינה';
  return null;
}
