import { findBankByNumber, isKnownBankName, isKnownBankNumber } from '../data/israeliBanks';

export interface BankFieldErrors {
  bankNumber?: string | null;
  bankName?: string | null;
  branchNumber?: string | null;
  accountNumber?: string | null;
  accountHolderName?: string | null;
  partialBank?: string | null;
}

export const PARTIAL_BANK_MESSAGE = 'פרטי בנק חייבים להיות מלאים או ריקים';
export const BANK_MISMATCH_MESSAGE = 'מספר בנק אינו תואם לשם הבנק';

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
  const trimmedName = bankName.trim();
  const bankFromNumber = findBankByNumber(bankNumber);

  const bankNumErr = validateBankNumber(bankNumber);
  if (bankNumErr) errors.bankNumber = bankNumErr;

  if (trimmedName.length > 0 && !isKnownBankName(trimmedName)) {
    errors.bankName = 'שם בנק אינו מזוהה';
  }

  if (bankFromNumber && trimmedName.length > 0 && bankFromNumber.name !== trimmedName) {
    errors.bankNumber = BANK_MISMATCH_MESSAGE;
    errors.bankName = BANK_MISMATCH_MESSAGE;
  }

  const allCoreFilled = bankNumber.trim().length > 0
    && branchNumber.trim().length > 0
    && accountNumber.trim().length > 0
    && accountHolderName.trim().length > 0;

  if (allCoreFilled && trimmedName.length === 0) {
    errors.bankName = 'יש לבחור בנק מהרשימה';
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
  bankName = '',
): string | null {
  return firstBankFieldError(validateBankFieldErrors(
    bankNumber,
    branchNumber,
    accountNumber,
    accountHolderName,
    bankName,
  ));
}

/** Committee payment / transfer modal — 4 stored fields only; no UI-only bankName. */
export function validateBankFieldsForPayment(
  bankNumber: string,
  branchNumber: string,
  accountNumber: string,
  accountHolderName: string,
): string | null {
  if (isBankAllEmpty(bankNumber, branchNumber, accountNumber, accountHolderName)) {
    return PARTIAL_BANK_MESSAGE;
  }
  const allCoreFilled = bankNumber.trim().length > 0
    && branchNumber.trim().length > 0
    && accountNumber.trim().length > 0
    && accountHolderName.trim().length > 0;
  if (!allCoreFilled) return PARTIAL_BANK_MESSAGE;

  const bankNumErr = validateBankNumber(bankNumber);
  if (bankNumErr) return bankNumErr;
  const branchErr = validateBankDigits(branchNumber, 'מספר סניף');
  if (branchErr) return branchErr;
  const accountErr = validateBankDigits(accountNumber, 'מספר חשבון');
  if (accountErr) return accountErr;
  if (accountHolderName.trim().length === 0) {
    return 'שם בעל החשבון הוא שדה חובה';
  }
  return null;
}

export interface BankFields {
  bankNumber: string | null;
  branchNumber: string | null;
  accountNumber: string | null;
  accountHolderName: string | null;
}

/** Committee display — full bank-branch-account or לא הוזן when bank_transfer target needs card bank. */
export function formatBankAccountSummary(fields: BankFields | null): string {
  const bankNumber = fields?.bankNumber?.trim() ?? '';
  const branchNumber = fields?.branchNumber?.trim() ?? '';
  const accountNumber = fields?.accountNumber?.trim() ?? '';
  if (bankNumber && branchNumber && accountNumber) {
    return `${bankNumber}-${branchNumber}-${accountNumber}`;
  }
  return 'לא הוזן';
}

export function isBankCompleteForPayment(fields: BankFields): boolean {
  return validateBankFieldsForPayment(
    fields.bankNumber ?? '',
    fields.branchNumber ?? '',
    fields.accountNumber ?? '',
    fields.accountHolderName ?? '',
  ) === null;
}
