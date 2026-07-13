export interface PhoneFieldErrors {
  prefix?: string | null;
  number?: string | null;
}

export function validateOptionalPhoneParts(prefix: string, number: string): PhoneFieldErrors {
  const p = prefix.trim();
  const n = number.trim();

  if (p.length === 0 && n.length === 0) {
    return {};
  }

  const errors: PhoneFieldErrors = {};

  if (p.length === 0) {
    errors.prefix = 'קידומת היא שדה חובה';
  } else if (!/^\d+$/.test(p)) {
    errors.prefix = 'קידומת חייבת להכיל ספרות בלבד';
  } else if (p.length !== 2 && p.length !== 3) {
    errors.prefix = 'קידומת חייבת להכיל 2 או 3 ספרות';
  }

  if (n.length === 0) {
    errors.number = 'מספר טלפון הוא שדה חובה';
  } else if (!/^\d+$/.test(n)) {
    errors.number = 'מספר טלפון חייב להכיל ספרות בלבד';
  } else if (n.length !== 7) {
    errors.number = 'מספר טלפון חייב להכיל 7 ספרות';
  }

  return errors;
}

export function hasPhoneErrors(errors: PhoneFieldErrors): boolean {
  return Boolean(errors.prefix || errors.number);
}

/** Parse stored phone (e.g. "054-1234567") into prefix and number parts. */
export function parsePhoneValue(value: string): { prefix: string; number: string } {
  const trimmed = value.trim();
  if (trimmed.length === 0) return { prefix: '', number: '' };

  const dashed = trimmed.match(/^(\d{2,3})-(\d+)$/);
  if (dashed) {
    return { prefix: dashed[1], number: dashed[2].slice(0, 7) };
  }

  const digits = trimmed.replace(/\D/g, '');
  if (digits.length <= 3) {
    return { prefix: digits, number: '' };
  }
  return { prefix: digits.slice(0, 3), number: digits.slice(3, 10) };
}
