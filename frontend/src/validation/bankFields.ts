import { findBankByName, isKnownBankNumber } from '../data/israeliBanks';

export interface BankFieldErrors {
  bankNumber?: string | null;
  bankName?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  partialBank?: string | null;
}

export const PARTIAL_BANK_MESSAGE = 'פרטי בנק חייבים להיות מלאים או ריקים';

export function isBankAllEmpty(
  bankNumber: string,
  branchNumber: string,
  accountNumber: string,
  accountHolderName: string,
  bankName = '',
): boolean {
  return bankNumber.trim().length === 0
    && branchNumber.trim().length === 0
    && accountNumber.trim().length === 0
    && accountHolderName.trim().length === 0
    && bankName.trim().length === 0;
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
  if (isBankAllEmpty(bankNumber, branchNumber, accountNumber, accountHolderName, bankName)) {
    return {};
  }

  const hasAny = bankNumber.trim().length > 0
    || branchNumber.trim().length > 0
    || accountNumber.trim().length > 0
    || accountHolderName.trim().length > 0
    || bankName.trim().length > 0;

  if (hasAny) {
    const allCoreFilled = bankNumber.trim().length > 0
      && branchNumber.trim().length > 0
      && accountNumber.trim().length > 0
      && accountHolderName.trim().length > 0;

    if (!allCoreFilled) {
      return { partialBank: PARTIAL_BANK_MESSAGE };
    }
  }

  const errors: BankFieldErrors = {};
  const bankNumErr = validateBankNumber(bankNumber);
  if (bankNumErr) errors.bankNumber = bankNumErr;

  const trimmedName = bankName.trim();
  if (trimmedName.length > 0 && !findBankByName(trimmedName)) {
    errors.bankName = 'שם בנק אינו מזוהה';
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
  return errors.partialBank
    ?? errors.bankNumber
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
