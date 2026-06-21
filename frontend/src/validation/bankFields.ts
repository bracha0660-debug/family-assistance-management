import { findBankByName, isKnownBankNumber } from '../data/israeliBanks';

export interface BankFieldErrors {
  bankNumber?: string | null;
  bankName?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
}

export function validateBankDigits(value: string, label: string): string | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return `${label} הוא שדה חובה`;
  if (!/^\d+$/.test(trimmed)) return 'שדות בנק חייבים להכיל ספרות בלבד';
  return null;
}

export function validateBankNumber(value: string): string | null {
  const digitsError = validateBankDigits(value, 'מספר בנק');
  if (digitsError) return digitsError;
  if (!isKnownBankNumber(value)) return 'מספר בנק אינו מזוהה';
  return null;
}

export function validateBankFieldErrors(
  bankNumber: string,
  branchNumber: string,
  accountNumber: string,
  accountHolderName: string,
  bankName = '',
): BankFieldErrors {
  const errors: BankFieldErrors = {};
  const bankNumErr = validateBankNumber(bankNumber);
  if (bankNumErr) errors.bankNumber = bankNumErr;

  const trimmedName = bankName.trim();
  if (trimmedName.length > 0 && !findBankByName(trimmedName)) {
    errors.bankName = 'שם בנק אינו מזוהה';
  } else if (bankNumber.trim().length === 0 && trimmedName.length === 0) {
    errors.bankNumber = 'מספר בנק הוא שדה חובה';
  }

  const branchErr = validateBankDigits(branchNumber, 'מספר סניף');
  if (branchErr) errors.branchNumber = branchErr;

  const accountErr = validateBankDigits(accountNumber, 'מספר חשבון');
  if (accountErr) errors.accountNumber = accountErr;

  if (accountHolderName.trim().length === 0) {
    errors.accountHolderName = 'שם בעל החשבון הוא שדה חובה';
  }

  return errors;
}

export function firstBankFieldError(errors: BankFieldErrors): string | null {
  return errors.bankNumber
    ?? errors.bankName
    ?? errors.branchNumber
    ?? errors.accountNumber
    ?? errors.accountHolderName
    ?? null;
}

export function validateBankDetails(
  bankNumber: string,
  branchNumber: string,
  accountNumber: string,
  accountHolderName: string,
): string | null {
  return firstBankFieldError(validateBankFieldErrors(
    bankNumber,
    branchNumber,
    accountNumber,
    accountHolderName,
  ));
}
