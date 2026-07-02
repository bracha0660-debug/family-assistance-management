import type { PaymentMethod, PaymentTarget } from '../api/committeeDecisions';
import { findBankByNumber } from '../data/israeliBanks';
import { formatBankAccountSummary, firstBankFieldError, isBankCompleteForPayment, type BankFields, validateBankFieldErrors } from './bankFields';

export const FAMILY_BANK_INCOMPLETE_MESSAGE = 'יש לעדכן פרטי חשבון בנק בכרטיס המשפחה';
export const SUPPLIER_BANK_INCOMPLETE_MESSAGE = 'יש לעדכן פרטי חשבון בנק במסך הספקים';
export const SUPPLIER_VOUCHERS_MESSAGE = 'ספק אינו יכול לקבל תשלום בתווים';
export const PAYEE_NAME_REQUIRED_MESSAGE = 'יש לציין שם מוטב';
export const TRANSFER_BANK_REQUIRED_MESSAGE = 'יש להזין פרטי העברה בנקאית';
export const D8_CONFIRM_MESSAGE = 'שינוי יעד התשלום יאפס את המוטב ואופן התשלום שנבחרו. האם להמשיך?';

const ALLOWED_METHODS: Record<PaymentTarget, PaymentMethod[]> = {
  family: ['bank_transfer', 'check', 'vouchers'],
  supplier: ['bank_transfer', 'check'],
  other: ['bank_transfer', 'check', 'vouchers'],
};

export interface CommitteeItemRowState {
  assistanceTypeId: string;
  description: string;
  amount: string;
  paymentTarget: PaymentTarget | '';
  paymentMethod: PaymentMethod | '';
  supplierId: string;
  payeeName: string;
  transferBankNumber: string;
  transferBranchNumber: string;
  transferAccountNumber: string;
  voucherType: string;
  isUrgent: boolean;
}

export function createEmptyItemRowState(): CommitteeItemRowState {
  return {
    assistanceTypeId: '',
    description: '',
    amount: '',
    paymentTarget: '',
    paymentMethod: '',
    supplierId: '',
    payeeName: '',
    transferBankNumber: '',
    transferBranchNumber: '',
    transferAccountNumber: '',
    voucherType: '',
    isUrgent: false,
  };
}

export function clearTransferBankFields(state: CommitteeItemRowState): CommitteeItemRowState {
  return {
    ...state,
    transferBankNumber: '',
    transferBranchNumber: '',
    transferAccountNumber: '',
  };
}

export function isTransferBankComplete(state: CommitteeItemRowState): boolean {
  const bankFromNumber = findBankByNumber(state.transferBankNumber);
  return firstBankFieldError(validateBankFieldErrors(
    state.transferBankNumber,
    state.transferBranchNumber,
    state.transferAccountNumber,
    state.payeeName,
    bankFromNumber?.name ?? '',
  )) === null;
}

export function formatTransferDetailsSummary(
  paymentTarget: PaymentTarget | string,
  paymentMethod: PaymentMethod | string,
  transferBankNumber: string | null | undefined,
  transferBranchNumber: string | null | undefined,
  transferAccountNumber: string | null | undefined,
): string {
  if (paymentTarget !== 'other' || paymentMethod !== 'bank_transfer') return '—';
  if (transferBankNumber?.trim() && transferBranchNumber?.trim() && transferAccountNumber?.trim()) {
    return `${transferBankNumber.trim()}-${transferBranchNumber.trim()}-${transferAccountNumber.trim()}`;
  }
  return 'לא הוזן';
}

export function resolveCommitteeBankDetailsDisplay(
  paymentTarget: PaymentTarget | '' | string,
  paymentMethod: PaymentMethod | '' | string,
  options: {
    familyBank?: BankFields | null;
    supplierBank?: BankFields | null;
    transferBankNumber?: string | null;
    transferBranchNumber?: string | null;
    transferAccountNumber?: string | null;
  },
): string {
  if (paymentMethod !== 'bank_transfer') return '—';
  if (paymentTarget === 'family') {
    return formatBankAccountSummary(options.familyBank ?? null);
  }
  if (paymentTarget === 'supplier') {
    return formatBankAccountSummary(options.supplierBank ?? null);
  }
  if (paymentTarget === 'other') {
    return formatTransferDetailsSummary(
      paymentTarget,
      paymentMethod,
      options.transferBankNumber,
      options.transferBranchNumber,
      options.transferAccountNumber,
    );
  }
  return '—';
}

export function getAllowedPaymentMethods(target: PaymentTarget | ''): PaymentMethod[] {
  if (!target) return [];
  return ALLOWED_METHODS[target];
}

export function isPaymentMethodAllowed(target: PaymentTarget | '', method: PaymentMethod | ''): boolean {
  if (!target || !method) return false;
  return ALLOWED_METHODS[target].includes(method as PaymentMethod);
}

export function validateCommitteeItemRow(
  row: CommitteeItemRowState,
  familyBank: BankFields | null,
  supplierBank: BankFields | null,
): string | null {
  if (!row.assistanceTypeId) return 'יש לבחור סוג סיוע';

  const parsedAmount = Number(row.amount);
  if (!Number.isFinite(parsedAmount) || parsedAmount <= 0 || parsedAmount > 1_000_000) {
    return 'סכום חייב להיות בין 0 ל-1,000,000';
  }

  if (!row.paymentTarget) return 'יש לבחור יעד תשלום';

  if (row.paymentTarget === 'family' && !row.payeeName.trim()) {
    return PAYEE_NAME_REQUIRED_MESSAGE;
  }
  if (row.paymentTarget === 'supplier' && !row.supplierId) {
    return 'יש לבחור ספק';
  }
  if (row.paymentTarget === 'other' && !row.payeeName.trim()) {
    return PAYEE_NAME_REQUIRED_MESSAGE;
  }

  if (!row.paymentMethod) return 'יש לבחור אופן תשלום';

  if (!isPaymentMethodAllowed(row.paymentTarget, row.paymentMethod)) {
    if (row.paymentTarget === 'supplier' && row.paymentMethod === 'vouchers') {
      return SUPPLIER_VOUCHERS_MESSAGE;
    }
    return 'אופן תשלום לא חוקי ליעד שנבחר';
  }

  if (row.paymentMethod === 'vouchers' && !row.voucherType.trim()) {
    return 'יש לציין סוג שובר';
  }

  if (row.paymentMethod === 'bank_transfer') {
    if (row.paymentTarget === 'family' && familyBank && !isBankCompleteForPayment(familyBank)) {
      return FAMILY_BANK_INCOMPLETE_MESSAGE;
    }
    if (row.paymentTarget === 'supplier' && (!supplierBank || !isBankCompleteForPayment(supplierBank))) {
      return SUPPLIER_BANK_INCOMPLETE_MESSAGE;
    }
    if (row.paymentTarget === 'other' && !isTransferBankComplete(row)) {
      return TRANSFER_BANK_REQUIRED_MESSAGE;
    }
  }

  return null;
}

export function hasMeaningfulPaymentData(
  state: Pick<
    CommitteeItemRowState,
    'supplierId' | 'payeeName' | 'paymentMethod' | 'voucherType' | 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'
  >,
): boolean {
  return Boolean(state.supplierId)
    || state.payeeName.trim().length > 0
    || Boolean(state.paymentMethod)
    || state.voucherType.trim().length > 0
    || state.transferBankNumber.trim().length > 0
    || state.transferBranchNumber.trim().length > 0
    || state.transferAccountNumber.trim().length > 0;
}

export function applyPaymentTargetChange(
  newTarget: PaymentTarget,
  state: CommitteeItemRowState,
  familyLastName: string,
): CommitteeItemRowState {
  let next: CommitteeItemRowState = { ...state, paymentTarget: newTarget };

  if (newTarget === 'family') {
    next.supplierId = '';
    next.paymentMethod = '';
    next.voucherType = '';
    next.payeeName = familyLastName;
  } else if (newTarget === 'supplier') {
    next.payeeName = '';
    next.paymentMethod = '';
    next.voucherType = '';
    next.supplierId = '';
  } else {
    next.supplierId = '';
    next.payeeName = '';
    next.paymentMethod = '';
    next.voucherType = '';
  }

  next = clearTransferBankFields(next);
  return next;
}

export function applyPaymentMethodChange(
  method: PaymentMethod | '',
  state: CommitteeItemRowState,
): CommitteeItemRowState {
  let next: CommitteeItemRowState = {
    ...state,
    paymentMethod: method,
    voucherType: method === 'vouchers' ? state.voucherType : '',
  };

  if (method !== 'bank_transfer') {
    next = clearTransferBankFields(next);
  }

  return next;
}

export function onAssistanceTypeChange(state: CommitteeItemRowState): CommitteeItemRowState {
  return state;
}

export function revalidateAfterBeneficiaryChange(
  state: CommitteeItemRowState,
  familyBank: BankFields | null,
  supplierBank: BankFields | null,
): { state: CommitteeItemRowState; bankMessage: string | null } {
  if (state.paymentMethod !== 'bank_transfer') {
    return { state, bankMessage: null };
  }

  let bankMessage: string | null = null;
  if (state.paymentTarget === 'family' && familyBank && !isBankCompleteForPayment(familyBank)) {
    bankMessage = FAMILY_BANK_INCOMPLETE_MESSAGE;
  } else if (state.paymentTarget === 'supplier' && (!supplierBank || !isBankCompleteForPayment(supplierBank))) {
    bankMessage = SUPPLIER_BANK_INCOMPLETE_MESSAGE;
  }

  if (bankMessage) {
    return {
      state: clearTransferBankFields({ ...state, paymentMethod: '', voucherType: '' }),
      bankMessage,
    };
  }

  return { state, bankMessage: null };
}

export function needsTransferBankModal(state: CommitteeItemRowState): boolean {
  return state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer';
}
