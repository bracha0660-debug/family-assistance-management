import type {
  AssistanceItemDto,
  CreateAssistanceItemPayload,
  PaymentMethod,
  PaymentTarget,
  UpdateAssistanceItemPayload,
} from '../../api/committeeDecisions';
import type { BankFields } from '../../validation/bankFields';
import type { CommitteeItemRowState } from '../../validation/committeeItemPayment';

export function translatePaymentTarget(t: string): string {
  switch (t) {
    case 'family': return 'משפחה';
    case 'supplier': return 'ספק';
    case 'other': return 'אחר';
    default: return t;
  }
}

export function translatePaymentMethod(m: string): string {
  switch (m) {
    case 'bank_transfer': return 'העברה בנקאית';
    case 'check': return 'המחאה';
    case 'vouchers': return 'תווי קנייה';
    default: return m;
  }
}

export function toBankFields(source: {
  bankNumber: string | null;
  branchNumber: string | null;
  accountNumber: string | null;
  accountHolderName: string | null;
}): BankFields {
  return {
    bankNumber: source.bankNumber,
    branchNumber: source.branchNumber,
    accountNumber: source.accountNumber,
    accountHolderName: source.accountHolderName,
  };
}

export function itemToRowState(item: AssistanceItemDto): CommitteeItemRowState {
  return {
    assistanceTypeId: item.assistanceTypeId,
    description: item.description ?? '',
    amount: String(item.amount),
    paymentTarget: item.paymentTarget as PaymentTarget,
    paymentMethod: item.paymentMethod as PaymentMethod,
    supplierId: item.supplierId ?? '',
    payeeName: item.payeeName ?? '',
    transferBankNumber: item.transferBankNumber ?? '',
    transferBranchNumber: item.transferBranchNumber ?? '',
    transferAccountNumber: item.transferAccountNumber ?? '',
    voucherType: item.voucherType ?? '',
    isUrgent: item.isUrgent,
  };
}

export function buildCreatePayload(state: CommitteeItemRowState): CreateAssistanceItemPayload {
  const payload: CreateAssistanceItemPayload = {
    assistanceTypeId: state.assistanceTypeId,
    description: state.description.trim() || null,
    amount: Number(state.amount),
    paymentTarget: state.paymentTarget as PaymentTarget,
    paymentMethod: state.paymentMethod as PaymentMethod,
    supplierId: state.paymentTarget === 'supplier' ? state.supplierId : null,
    payeeName: (state.paymentTarget === 'family' || state.paymentTarget === 'other')
      ? state.payeeName.trim()
      : null,
    voucherType: state.paymentMethod === 'vouchers' ? state.voucherType.trim() || null : null,
    isUrgent: state.isUrgent,
  };

  if (state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer') {
    payload.transferBankNumber = state.transferBankNumber.trim();
    payload.transferBranchNumber = state.transferBranchNumber.trim();
    payload.transferAccountNumber = state.transferAccountNumber.trim();
  }

  return payload;
}

export function buildUpdatePayload(
  state: CommitteeItemRowState,
  previousSupplierId: string | null,
): UpdateAssistanceItemPayload {
  const payload: UpdateAssistanceItemPayload = {
    assistanceTypeId: state.assistanceTypeId,
    description: state.description.trim() || null,
    amount: Number(state.amount),
    paymentTarget: state.paymentTarget as PaymentTarget,
    paymentMethod: state.paymentMethod as PaymentMethod,
    isUrgent: state.isUrgent,
    voucherType: state.paymentMethod === 'vouchers' ? state.voucherType.trim() || null : null,
  };

  if (state.paymentTarget === 'supplier') {
    payload.supplierId = state.supplierId;
  } else if (previousSupplierId) {
    payload.clearSupplierId = true;
  }

  if (state.paymentTarget === 'family' || state.paymentTarget === 'other') {
    payload.payeeName = state.payeeName.trim();
  }

  if (state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer') {
    payload.transferBankNumber = state.transferBankNumber.trim();
    payload.transferBranchNumber = state.transferBranchNumber.trim();
    payload.transferAccountNumber = state.transferAccountNumber.trim();
  } else {
    payload.clearTransferBank = true;
  }

  return payload;
}

export const ADD_ITEM_FOCUS_ORDER = [
  'item-assistance-type',
  'item-description',
  'item-payment-target',
  'item-payee-name',
  'item-payment-method',
  'item-voucher-type',
  'item-amount',
];

export const EDIT_ITEM_FOCUS_ORDER = [
  'edit-item-assistance-type',
  'edit-item-description',
  'edit-item-payment-target',
  'edit-item-payee-name',
  'edit-item-payment-method',
  'edit-item-voucher-type',
  'edit-item-amount',
];



