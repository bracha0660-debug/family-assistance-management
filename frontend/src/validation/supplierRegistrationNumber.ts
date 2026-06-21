import { isValidIsraeliId } from './israeliId';

export const SUPPLIER_REGISTRATION_REQUIRED = 'שדה חובה';
export const SUPPLIER_REGISTRATION_MIN_DIGITS = 'מספר עוסק / ח.פ. חייב להכיל לפחות 9 ספרות';
export const SUPPLIER_REGISTRATION_DIGITS_ONLY = 'מספר עוסק / ח.פ. חייב להכיל ספרות בלבד';
export const SUPPLIER_REGISTRATION_INVALID = 'מספר עוסק / ח.פ. אינו תקין';

export function validateSupplierRegistrationNumber(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return SUPPLIER_REGISTRATION_REQUIRED;
  }

  if (!/^\d+$/.test(trimmed)) {
    return SUPPLIER_REGISTRATION_DIGITS_ONLY;
  }

  if (trimmed.length < 9) {
    return SUPPLIER_REGISTRATION_MIN_DIGITS;
  }

  if (trimmed.length > 9) {
    return SUPPLIER_REGISTRATION_INVALID;
  }

  if (!isValidIsraeliId(trimmed)) {
    return SUPPLIER_REGISTRATION_INVALID;
  }

  return null;
}
