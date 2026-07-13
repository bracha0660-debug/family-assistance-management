import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import type { UserDto } from '../api/auth';
import { listAssistanceTypes, type AssistanceTypeDto } from '../api/assistanceTypes';
import {
  addAssistanceItem,
  cancelCommitteeDecision,
  createCommitteeDecision,
  deleteCommitteeDecision,
  getCommitteeDecision,
  listCommitteeDecisions,
  removeAssistanceItem,
  submitCommitteeDecision,
  updateAssistanceItem,
  updateCommitteeDecision,
  PAYMENT_TARGETS,
  type AssistanceItemDto,
  type CommitteeDecisionDto,
  type CommitteeDecisionListResponse,
  type CreateAssistanceItemPayload,
  type PaymentMethod,
  type PaymentTarget,
  type UpdateAssistanceItemPayload,
} from '../api/committeeDecisions';
import {
  approveAssistanceItem,
  completeAssistanceItem,
  listAssistanceItems,
  rejectAssistanceItem,
  resubmitAssistanceItem,
  returnAssistanceItem,
  suspendAssistanceItem,
  type AssistanceItemListDto,
} from '../api/assistanceItems';
import { amountAdjustmentReasonLabel } from '../api/exportBatches';
import { listFamilies, type FamilyDto } from '../api/families';
import { PERMISSION_KEYS } from '../api/permissions';
import { listSuppliers, type SupplierDto } from '../api/suppliers';
import type { HomeNavigationTarget } from '../api/workflow';
import { AssistanceItemDetailsModal } from '../components/AssistanceItemDetailsModal';
import { AssistanceItemHistoryModal } from '../components/AssistanceItemHistoryModal';
import { HistoryIconButton } from '../components/history/HistoryIconButton';
import { HistoryValueTransition } from '../components/history/HistoryValueTransition';
import { workflowFilterLabel } from './home/workflowStatus';
import {
  assistanceItemStatusLabel,
  DECISIONS_ITEM_ACTIONS,
  decisionsItemActions,
  workflowActionButtonClass,
  workflowActionLabel,
} from './home/workflowLabels';
import { hasPermission } from '../hooks/usePermissions';
import { FieldValidationTooltip } from '../components/FieldValidation';
import { BankSelect } from '../components/BankDetailsFields';
import { ModalShell } from '../components/ModalShell';
import { focusFirstInvalidField } from '../utils/formValidation';
import { partitionSuppliersForAssistanceType } from '../utils/relatedSuppliers';
import { validateBankFieldsForPayment, type BankFields } from '../validation/bankFields';
import {
  applyPaymentMethodChange,
  applyPaymentTargetChange,
  createEmptyItemRowState,
  PAYEE_NAME_REQUIRED_MESSAGE,
  D8_CONFIRM_MESSAGE,
  formatTransferDetailsSummary,
  getAllowedPaymentMethods,
  hasMeaningfulPaymentData,
  isTransferBankComplete,
  needsTransferBankModal,
  onAssistanceTypeChange,
  revalidateAfterBeneficiaryChange,
  resolveCommitteeBankDetailsDisplay,
  type CommitteeItemRowState,
  validateCommitteeItemRow,
} from '../validation/committeeItemPayment';

interface CommitteeDecisionsPageProps {
  user: UserDto;
  initialFilter?: HomeNavigationTarget | null;
}

/** Decisions Table 2 — payment execution actions live on PaymentsQueuePage (Phase 16 M97). */
// Action allow-list + filter: decisionsItemActions / DECISIONS_ITEM_ACTIONS in workflowLabels.ts

function formatMoney(amount: number): string {
  return `${amount.toLocaleString('he-IL')} ₪`;
}

function formatAmountTracking(
  amount: number,
  hasAdjustment?: boolean | null,
  originalApprovedAmount?: number | null,
  reason?: string | null,
  explanation?: string | null,
): { amount: number; original: number | null; hint: string | null } {
  if (hasAdjustment && originalApprovedAmount != null) {
    const hintParts = [`סיבה: ${amountAdjustmentReasonLabel(reason)}`];
    if (reason === 'other' && explanation) hintParts.push(explanation);
    return {
      amount,
      original: originalApprovedAmount,
      hint: hintParts.join(' — '),
    };
  }
  return { amount, original: null, hint: null };
}

function renderAmountTrackingPrimary(amt: { amount: number; original: number | null }) {
  if (amt.original != null) {
    return (
      <HistoryValueTransition
        previousValue={formatMoney(amt.original)}
        newValue={formatMoney(amt.amount)}
      />
    );
  }
  return formatMoney(amt.amount);
}

function resolveDraftListOptions(filter: HomeNavigationTarget | null | undefined) {
  if (filter?.targetTab === 'decisions' && filter.listView === 'assistance_items') {
    return { status: 'draft', ownership: 'mine' as const };
  }
  if (filter?.targetTab === 'decisions' && filter.listView === 'draft_decisions') {
    return {
      section: filter.section,
      status: filter.status ?? 'draft',
      ownership: filter.ownership ?? 'mine',
      minAgeDays: filter.minAgeDays,
    };
  }
  if (filter?.targetTab === 'decisions' && (filter.section || filter.status || filter.minAgeDays)) {
    const itemStatuses = new Set([
      'submitted', 'returned', 'approved', 'suspended', 'rejected',
      'waiting_for_reference', 'paid', 'completed',
    ]);
    if (filter.status && itemStatuses.has(filter.status)) {
      return { status: 'draft', ownership: 'mine' as const };
    }
    return {
      section: filter.section,
      status: filter.status ?? 'draft',
      ownership: filter.ownership ?? 'mine',
      minAgeDays: filter.minAgeDays,
    };
  }
  return { status: 'draft', ownership: 'mine' as const };
}

function resolveItemListOptions(filter: HomeNavigationTarget | null | undefined) {
  if (!filter || filter.targetTab !== 'decisions') return undefined;
  if (filter.listView === 'draft_decisions') return undefined;
  if (filter.listView === 'assistance_items') {
    return {
      section: filter.section,
      status: filter.status,
      ownership: filter.ownership,
      minAgeDays: filter.minAgeDays,
    };
  }
  const itemStatuses = new Set([
    'submitted', 'returned', 'approved', 'suspended', 'rejected',
    'waiting_for_reference', 'paid', 'completed',
  ]);
  if (filter.status && itemStatuses.has(filter.status)) {
    return {
      section: filter.section,
      status: filter.status,
      ownership: filter.ownership,
      minAgeDays: filter.minAgeDays,
    };
  }
  return undefined;
}

function listViewFocus(
  filter: HomeNavigationTarget | null | undefined,
): 'draft_decisions' | 'assistance_items' | null {
  if (!filter || filter.targetTab !== 'decisions') return null;
  return filter.listView ?? null;
}

function translateDecisionStatus(status: string): string {
  switch (status) {
    case 'draft': return 'טיוטה';
    case 'submitted': return 'הוגש';
    case 'returned_for_revision': return 'הוחזר לתיקון';
    case 'approved': return 'אושר';
    case 'rejected': return 'נדחה';
    case 'suspended': return 'מושעה';
    case 'cancelled': return 'בוטל';
    case 'partially_paid': return 'שולם חלקית';
    case 'fully_paid': return 'שולם במלואו';
    default: return status;
  }
}

function translatePaymentTarget(t: string): string {
  switch (t) {
    case 'family': return 'משפחה';
    case 'supplier': return 'ספק';
    case 'other': return 'אחר';
    default: return t;
  }
}

function translatePaymentMethod(m: string): string {
  switch (m) {
    case 'bank_transfer': return 'העברה בנקאית';
    case 'check': return 'המחאה';
    case 'vouchers': return 'תווים';
    default: return m;
  }
}

function formatBeneficiaryName(item: AssistanceItemDto): string {
  if (item.paymentTarget === 'supplier') return item.supplierName ?? '—';
  return item.payeeName ?? '—';
}

function formatPaymentMethodCell(item: AssistanceItemDto): string {
  const base = translatePaymentMethod(item.paymentMethod);
  if (item.paymentMethod === 'vouchers' && item.voucherType) {
    return `${base} (${item.voucherType})`;
  }
  return base;
}

function toBankFields(source: {
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

function itemToRowState(item: AssistanceItemDto): CommitteeItemRowState {
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

function buildCreatePayload(state: CommitteeItemRowState): CreateAssistanceItemPayload {
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

function buildUpdatePayload(
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

const ADD_ITEM_FOCUS_ORDER = [
  'item-assistance-type',
  'item-description',
  'item-payment-target',
  'item-payee-name',
  'item-payment-method',
  'item-voucher-type',
  'item-amount',
];

const EDIT_ITEM_FOCUS_ORDER = [
  'edit-item-assistance-type',
  'edit-item-description',
  'edit-item-payment-target',
  'edit-item-payee-name',
  'edit-item-payment-method',
  'edit-item-voucher-type',
  'edit-item-amount',
];

function SupplierSelectOptions({
  recommended,
  other,
}: {
  recommended: SupplierDto[];
  other: SupplierDto[];
}) {
  if (recommended.length === 0) {
    return (
      <>
        {other.map((s) => (
          <option key={s.id} value={s.id}>{s.name}</option>
        ))}
      </>
    );
  }

  return (
    <>
      <optgroup label="ספקים מומלצים">
        {recommended.map((s) => (
          <option key={s.id} value={s.id}>{s.name}</option>
        ))}
      </optgroup>
      <optgroup label="כל הספקים">
        {other.map((s) => (
          <option key={s.id} value={s.id}>{s.name}</option>
        ))}
      </optgroup>
    </>
  );
}

function TransferBankPopover({
  initial,
  payeeName,
  onSave,
  onCancel,
}: {
  initial: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>;
  payeeName: string;
  onSave: (values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) => void;
  onCancel: () => void;
}) {
  const [bankNumber, setBankNumber] = useState(initial.transferBankNumber);
  const [branchNumber, setBranchNumber] = useState(initial.transferBranchNumber);
  const [accountNumber, setAccountNumber] = useState(initial.transferAccountNumber);
  const [error, setError] = useState('');
  const bankListId = useId();

  function handleSave(e: FormEvent) {
    e.preventDefault();
    const validationError = validateBankFieldsForPayment(
      bankNumber,
      branchNumber,
      accountNumber,
      payeeName,
    );
    if (validationError) {
      setError(validationError);
      return;
    }
    onSave({
      transferBankNumber: bankNumber.trim(),
      transferBranchNumber: branchNumber.trim(),
      transferAccountNumber: accountNumber.trim(),
    });
  }

  return (
    <div className="committee-transfer-popover" role="dialog" aria-label="פרטי העברה בנקאית">
      {error && (
        <div className="error" role="alert">
          {error}
        </div>
      )}
      <p className="hint-text committee-transfer-popover__holder">
        שם בעל החשבון: <strong>{payeeName.trim() || '—'}</strong>
      </p>
      <label htmlFor="transfer-bank-number">בנק <span className="field-required">*</span></label>
      <BankSelect
        id="transfer-bank-number"
        listId={bankListId}
        value={bankNumber}
        displayMode="number-name"
        disabled={false}
        errorId="transfer-bank-number-error"
        placeholder="חיפוש לפי מספר או שם"
        onSelect={(bank) => setBankNumber(bank.number)}
        onClear={() => setBankNumber('')}
      />
      <label htmlFor="transfer-branch-number">סניף <span className="field-required">*</span></label>
      <input
        id="transfer-branch-number"
        type="text"
        inputMode="numeric"
        value={branchNumber}
        onChange={(e) => setBranchNumber(e.target.value.replace(/\D/g, '').slice(0, 5))}
      />
      <label htmlFor="transfer-account-number">מספר חשבון <span className="field-required">*</span></label>
      <input
        id="transfer-account-number"
        type="text"
        inputMode="numeric"
        value={accountNumber}
        onChange={(e) => setAccountNumber(e.target.value.replace(/\D/g, '').slice(0, 20))}
      />
      <div className="committee-transfer-popover__actions">
        <button type="button" className="btn-secondary btn-small" onClick={onCancel}>ביטול</button>
        <button type="button" className="btn-small" onClick={handleSave}>שמור פרטי העברה</button>
      </div>
    </div>
  );
}

function CommitteeItemFormFields({
  idPrefix,
  state,
  onStateChange,
  types,
  suppliers,
  familyLastName,
  familyBank,
  disabled,
  payeeNameManuallyEdited,
  setPayeeNameManuallyEdited,
  onValidationMessage,
  showActions,
  onAdd,
  addLoading,
  addError,
  transferPopoverOpen,
  transferPopoverInitial,
  transferPopoverSession,
  onOpenTransferPopover,
  onTransferPopoverSave,
  onTransferPopoverCancel,
}: {
  idPrefix: 'item' | 'edit-item';
  state: CommitteeItemRowState;
  onStateChange: (next: CommitteeItemRowState) => void;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  familyLastName: string;
  familyBank: BankFields | null;
  disabled: boolean;
  payeeNameManuallyEdited: boolean;
  setPayeeNameManuallyEdited: (value: boolean) => void;
  onValidationMessage?: (message: string | null) => void;
  showActions?: boolean;
  onAdd?: () => void;
  addLoading?: boolean;
  addError?: string;
  transferPopoverOpen: boolean;
  transferPopoverInitial: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>;
  transferPopoverSession: number;
  onOpenTransferPopover: (contextState?: CommitteeItemRowState) => void;
  onTransferPopoverSave: (values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) => void;
  onTransferPopoverCancel: () => void;
}) {
  const fieldId = (name: string) => `${idPrefix}-${name}`;
  const { recommended: recommendedSuppliers, other: otherSuppliers } = partitionSuppliersForAssistanceType(
    types,
    suppliers,
    state.assistanceTypeId,
  );
  const allowedMethods = getAllowedPaymentMethods(state.paymentTarget);
  const transferSummary = formatTransferDetailsSummary(
    state.paymentTarget,
    state.paymentMethod,
    state.transferBankNumber,
    state.transferBranchNumber,
    state.transferAccountNumber,
  );
  const supplierBankForDisplay = state.supplierId
    ? toBankFields(suppliers.find((s) => s.id === state.supplierId)!)
    : null;
  const showOtherTransferModal = state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer';
  const showReadonlyBank = state.paymentMethod === 'bank_transfer'
    && (state.paymentTarget === 'family' || state.paymentTarget === 'supplier');
  const bankDetailsSummary = resolveCommitteeBankDetailsDisplay(
    state.paymentTarget,
    state.paymentMethod,
    {
      familyBank,
      supplierBank: supplierBankForDisplay,
      transferBankNumber: state.transferBankNumber,
      transferBranchNumber: state.transferBranchNumber,
      transferAccountNumber: state.transferAccountNumber,
    },
  );

  function handleTargetChange(e: ChangeEvent<HTMLSelectElement>) {
    const newTarget = e.target.value as PaymentTarget | '';
    if (!newTarget || newTarget === state.paymentTarget) return;

    if (hasMeaningfulPaymentData(state) && !window.confirm(D8_CONFIRM_MESSAGE)) {
      e.target.value = state.paymentTarget;
      return;
    }

    setPayeeNameManuallyEdited(false);
    onStateChange(applyPaymentTargetChange(newTarget, state, familyLastName));
    onValidationMessage?.(null);
  }

  function handleAssistanceTypeChange(e: ChangeEvent<HTMLSelectElement>) {
    onStateChange(onAssistanceTypeChange({ ...state, assistanceTypeId: e.target.value }));
  }

  function handlePayeeNameChange(value: string) {
    setPayeeNameManuallyEdited(true);
    onStateChange({ ...state, payeeName: value });
  }

  function handleSupplierChange(supplierId: string) {
    const next = { ...state, supplierId };
    const bank = supplierId
      ? toBankFields(suppliers.find((s) => s.id === supplierId)!)
      : null;
    const { state: validated, bankMessage } = revalidateAfterBeneficiaryChange(next, familyBank, bank);
    onStateChange(validated);
    onValidationMessage?.(bankMessage);
  }

  function handlePaymentMethodChange(method: PaymentMethod | '') {
    const next = applyPaymentMethodChange(method, state);
    if (needsTransferBankModal(next) && !next.payeeName.trim()) {
      onValidationMessage?.(PAYEE_NAME_REQUIRED_MESSAGE);
      onStateChange(applyPaymentMethodChange('', state));
      return;
    }
    onStateChange(next);
    onValidationMessage?.(null);
    if (needsTransferBankModal(next)) {
      onOpenTransferPopover(next);
    }
  }

  return (
    <div className="committee-item-form__grid">
      <div className="committee-item-form__field">
        <label htmlFor={fieldId('assistance-type')}>סוג סיוע</label>
        <select
          id={fieldId('assistance-type')}
          value={state.assistanceTypeId}
          onChange={handleAssistanceTypeChange}
          disabled={disabled}
        >
          {idPrefix === 'item' && <option value="">— בחר —</option>}
          {types.filter((t) => t.status === 'active').map((t) => (
            <option key={t.id} value={t.id}>{t.name}</option>
          ))}
        </select>
      </div>

      <div className="committee-item-form__field">
        <label htmlFor={fieldId('description')}>תיאור</label>
        <input
          id={fieldId('description')}
          type="text"
          placeholder="תיאור"
          value={state.description}
          onChange={(e) => onStateChange({ ...state, description: e.target.value })}
          disabled={disabled}
        />
      </div>

      <div className="committee-item-form__field">
        <label htmlFor={fieldId('payment-target')}>יעד תשלום</label>
        <select
          id={fieldId('payment-target')}
          value={state.paymentTarget}
          onChange={handleTargetChange}
          disabled={disabled}
        >
          {idPrefix === 'item' && <option value="">— בחר —</option>}
          {PAYMENT_TARGETS.map((t) => (
            <option key={t} value={t}>{translatePaymentTarget(t)}</option>
          ))}
        </select>
      </div>

      <div className="committee-item-form__field committee-item-form__field--payee">
        <label htmlFor={fieldId('payee-name')}>שם מוטב</label>
        {state.paymentTarget === 'supplier' ? (
          <select
            id={fieldId('payee-name')}
            value={state.supplierId}
            onChange={(e) => handleSupplierChange(e.target.value)}
            disabled={disabled || !state.paymentTarget}
          >
            <option value="">— בחר ספק —</option>
            <SupplierSelectOptions recommended={recommendedSuppliers} other={otherSuppliers} />
          </select>
        ) : state.paymentTarget === 'family' ? (
          <>
            <input
              id={fieldId('payee-name')}
              type="text"
              placeholder="שם מוטב"
              value={state.payeeName}
              onChange={(e) => handlePayeeNameChange(e.target.value)}
              disabled={disabled}
            />
            {!payeeNameManuallyEdited && state.payeeName === familyLastName && (
              <p className="bank-field-hint">ניתן לעריכה במקרה הצורך</p>
            )}
          </>
        ) : state.paymentTarget === 'other' ? (
          <input
            id={fieldId('payee-name')}
            type="text"
            placeholder="שם מוטב"
            value={state.payeeName}
            onChange={(e) => handlePayeeNameChange(e.target.value)}
            disabled={disabled}
          />
        ) : (
          <input id={fieldId('payee-name')} type="text" disabled placeholder="—" />
        )}
      </div>

      <div className="committee-item-form__field committee-item-form__field--method-stack">
        <label htmlFor={fieldId('payment-method')}>אופן תשלום</label>
        <select
          id={fieldId('payment-method')}
          value={state.paymentMethod}
          onChange={(e) => handlePaymentMethodChange(e.target.value as PaymentMethod | '')}
          disabled={disabled || !state.paymentTarget || (state.paymentTarget === 'supplier' && !state.supplierId)}
        >
          <option value="">— בחר —</option>
          {allowedMethods.map((m) => (
            <option key={m} value={m}>{translatePaymentMethod(m)}</option>
          ))}
        </select>
        {state.paymentMethod === 'vouchers' && (
          <input
            id={fieldId('voucher-type')}
            type="text"
            placeholder="סוג שובר"
            value={state.voucherType}
            onChange={(e) => onStateChange({ ...state, voucherType: e.target.value })}
            disabled={disabled}
          />
        )}
      </div>

      <div className="committee-item-form__field committee-item-form__field--bank-details">
        <label htmlFor={fieldId('bank-details')}>פרטי בנק</label>
        {showOtherTransferModal ? (
          <>
            <button
              id={fieldId('bank-details')}
              type="button"
              className="btn-secondary btn-small committee-transfer-summary-btn"
              onClick={() => onOpenTransferPopover()}
              disabled={disabled}
              title={transferSummary}
              aria-expanded={transferPopoverOpen}
            >
              {isTransferBankComplete(state) ? transferSummary : 'הזן פרטים'}
            </button>
            {transferPopoverOpen && (
              <TransferBankPopover
                key={transferPopoverSession}
                initial={transferPopoverInitial}
                payeeName={state.payeeName}
                onSave={onTransferPopoverSave}
                onCancel={onTransferPopoverCancel}
              />
            )}
          </>
        ) : showReadonlyBank ? (
          <input
            id={fieldId('bank-details')}
            type="text"
            className="committee-bank-readonly"
            disabled
            readOnly
            value={bankDetailsSummary}
            title={bankDetailsSummary}
          />
        ) : (
          <input
            id={fieldId('bank-details')}
            type="text"
            className="committee-bank-readonly"
            disabled
            readOnly
            value="—"
          />
        )}
      </div>

      <div className="committee-item-form__field">
        <label htmlFor={fieldId('amount')}>סכום</label>
        <input
          id={fieldId('amount')}
          type="number"
          placeholder="סכום"
          value={state.amount}
          onChange={(e) => onStateChange({ ...state, amount: e.target.value })}
          disabled={disabled}
          min={0}
          step={0.01}
        />
      </div>

      <div className="committee-item-form__field committee-item-form__field--urgent">
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={state.isUrgent}
            onChange={(e) => onStateChange({ ...state, isUrgent: e.target.checked })}
            disabled={disabled}
          />
          דחוף
        </label>
      </div>

      {showActions ? (
        <div className="committee-item-form__field committee-item-form__field--actions">
          <div className="validated-field-control">
            <button type="button" className="btn-small" onClick={onAdd} disabled={disabled || addLoading || transferPopoverOpen}>
              הוסף שורה
            </button>
            {addError && <FieldValidationTooltip id="item-form-error" message={addError} />}
          </div>
        </div>
      ) : (
        <div className="committee-item-form__field" aria-hidden="true" />
      )}
    </div>
  );
}

function CreateDecisionModal({
  families,
  onClose,
  onCreated,
}: {
  families: FamilyDto[];
  onClose: () => void;
  onCreated: (d: CommitteeDecisionDto) => void;
}) {
  const [familyId, setFamilyId] = useState('');
  const [meetingDate, setMeetingDate] = useState(new Date().toISOString().slice(0, 10));
  const [summary, setSummary] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!familyId) {
      setError('יש לבחור משפחה');
      return;
    }
    setLoading(true);
    try {
      const created = await createCommitteeDecision({
        familyId,
        meetingDate,
        summary: summary.trim() || null,
      });
      onCreated(created);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <ModalShell
      title="החלטת ועדה חדשה"
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>ביטול</button>
          <button type="submit" disabled={loading}>{loading ? 'יוצר...' : 'צור'}</button>
        </>
      )}
    >
      <label htmlFor="decision-family">משפחה <span className="field-required">*</span></label>
      <select id="decision-family" value={familyId} onChange={(e) => setFamilyId(e.target.value)} disabled={loading}>
        <option value="">— בחר משפחה —</option>
        {families.filter((f) => f.status === 'active').map((f) => (
          <option key={f.id} value={f.id}>{f.familyCode} — {f.familyLastName}</option>
        ))}
      </select>
      <label htmlFor="decision-date">תאריך ישיבה <span className="field-required">*</span></label>
      <input id="decision-date" type="date" value={meetingDate} onChange={(e) => setMeetingDate(e.target.value)} disabled={loading} />
      <label htmlFor="decision-summary">סיכום</label>
      <textarea id="decision-summary" value={summary} onChange={(e) => setSummary(e.target.value)} disabled={loading} rows={3} maxLength={2000} />
    </ModalShell>
  );
}

function ItemFormRow({
  types,
  suppliers,
  familyLastName,
  familyBank,
  onAdd,
  disabled,
}: {
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  familyLastName: string;
  familyBank: BankFields | null;
  onAdd: (payload: CreateAssistanceItemPayload) => Promise<void>;
  disabled: boolean;
}) {
  const [state, setState] = useState<CommitteeItemRowState>(createEmptyItemRowState);
  const [payeeNameManuallyEdited, setPayeeNameManuallyEdited] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [transferPopoverOpen, setTransferPopoverOpen] = useState(false);
  const [transferPopoverSession, setTransferPopoverSession] = useState(0);
  const [transferPopoverInitial, setTransferPopoverInitial] = useState<
    Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>
  >({
    transferBankNumber: '',
    transferBranchNumber: '',
    transferAccountNumber: '',
  });

  const selectedSupplier = state.supplierId
    ? suppliers.find((s) => s.id === state.supplierId) ?? null
    : null;
  const supplierBank = selectedSupplier ? toBankFields(selectedSupplier) : null;

  function openTransferPopover(contextState?: CommitteeItemRowState) {
    const source = contextState ?? state;
    setTransferPopoverInitial({
      transferBankNumber: source.transferBankNumber,
      transferBranchNumber: source.transferBranchNumber,
      transferAccountNumber: source.transferAccountNumber,
    });
    setTransferPopoverSession((prev) => prev + 1);
    setTransferPopoverOpen(true);
  }

  function handleTransferPopoverCancel() {
    setState((prev) => ({ ...prev, ...transferPopoverInitial }));
    setTransferPopoverOpen(false);
  }

  function handleTransferPopoverSave(values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) {
    setState((prev) => ({ ...prev, ...values }));
    setTransferPopoverOpen(false);
  }

  async function handleAdd() {
    setError('');
    const validationError = validateCommitteeItemRow(state, familyBank, supplierBank);
    if (validationError) {
      setError(validationError);
      focusFirstInvalidField(ADD_ITEM_FOCUS_ORDER);
      return;
    }

    setLoading(true);
    try {
      await onAdd(buildCreatePayload(state));
      setState(createEmptyItemRowState());
      setPayeeNameManuallyEdited(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <div className="committee-item-form">
        <CommitteeItemFormFields
          idPrefix="item"
          state={state}
          onStateChange={setState}
          types={types}
          suppliers={suppliers}
          familyLastName={familyLastName}
          familyBank={familyBank}
          disabled={disabled || loading}
          payeeNameManuallyEdited={payeeNameManuallyEdited}
          setPayeeNameManuallyEdited={setPayeeNameManuallyEdited}
          onValidationMessage={(msg) => setError(msg ?? '')}
          showActions
          onAdd={handleAdd}
          addLoading={loading}
          addError={error}
          transferPopoverOpen={transferPopoverOpen}
          transferPopoverInitial={transferPopoverInitial}
          transferPopoverSession={transferPopoverSession}
          onOpenTransferPopover={openTransferPopover}
          onTransferPopoverSave={handleTransferPopoverSave}
          onTransferPopoverCancel={handleTransferPopoverCancel}
        />
      </div>
    </>
  );
}

function ItemEditModal({
  item,
  types,
  suppliers,
  familyLastName,
  familyBank,
  decisionId,
  onClose,
  onSaved,
}: {
  item: AssistanceItemDto;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  familyLastName: string;
  familyBank: BankFields | null;
  decisionId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [state, setState] = useState<CommitteeItemRowState>(() => {
    const row = itemToRowState(item);
    if (row.paymentTarget === 'family' && !row.payeeName.trim()) {
      row.payeeName = familyLastName;
    }
    return row;
  });
  const [payeeNameManuallyEdited, setPayeeNameManuallyEdited] = useState(
    item.paymentTarget === 'family' && Boolean(item.payeeName?.trim()) && item.payeeName !== familyLastName,
  );
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [transferPopoverOpen, setTransferPopoverOpen] = useState(false);
  const [transferPopoverSession, setTransferPopoverSession] = useState(0);
  const [transferPopoverInitial, setTransferPopoverInitial] = useState<
    Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>
  >({
    transferBankNumber: '',
    transferBranchNumber: '',
    transferAccountNumber: '',
  });

  const selectedSupplier = state.supplierId
    ? suppliers.find((s) => s.id === state.supplierId) ?? null
    : null;
  const supplierBank = selectedSupplier ? toBankFields(selectedSupplier) : null;

  function openTransferPopover(contextState?: CommitteeItemRowState) {
    const source = contextState ?? state;
    setTransferPopoverInitial({
      transferBankNumber: source.transferBankNumber,
      transferBranchNumber: source.transferBranchNumber,
      transferAccountNumber: source.transferAccountNumber,
    });
    setTransferPopoverSession((prev) => prev + 1);
    setTransferPopoverOpen(true);
  }

  function handleTransferPopoverCancel() {
    setState((prev) => ({ ...prev, ...transferPopoverInitial }));
    setTransferPopoverOpen(false);
  }

  function handleTransferPopoverSave(values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) {
    setState((prev) => ({ ...prev, ...values }));
    setTransferPopoverOpen(false);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    const validationError = validateCommitteeItemRow(state, familyBank, supplierBank);
    if (validationError) {
      setError(validationError);
      focusFirstInvalidField(EDIT_ITEM_FOCUS_ORDER);
      return;
    }

    setLoading(true);
    try {
      await updateAssistanceItem(
        decisionId,
        item.id,
        item.version,
        buildUpdatePayload(state, item.supplierId),
      );
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <ModalShell
        title={`עריכת פריט #${item.lineNumber}`}
        loading={loading}
        onClose={onClose}
        onSubmit={handleSubmit}
        formError={error}
        footer={(
          <>
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>ביטול</button>
            <button type="submit" disabled={loading}>{loading ? 'שומר...' : 'שמור'}</button>
          </>
        )}
      >
        <div className="committee-items-shell">
          <div className="committee-item-form">
            <CommitteeItemFormFields
              idPrefix="edit-item"
              state={state}
              onStateChange={setState}
              types={types}
              suppliers={suppliers}
              familyLastName={familyLastName}
              familyBank={familyBank}
              disabled={loading}
              payeeNameManuallyEdited={payeeNameManuallyEdited}
              setPayeeNameManuallyEdited={setPayeeNameManuallyEdited}
              onValidationMessage={(msg) => setError(msg ?? '')}
              transferPopoverOpen={transferPopoverOpen}
              transferPopoverInitial={transferPopoverInitial}
              transferPopoverSession={transferPopoverSession}
              onOpenTransferPopover={openTransferPopover}
              onTransferPopoverSave={handleTransferPopoverSave}
              onTransferPopoverCancel={handleTransferPopoverCancel}
            />
          </div>
        </div>
      </ModalShell>
    </>
  );
}

function DecisionDetailPanel({
  decision: initial,
  user,
  types,
  suppliers,
  family,
  onClose,
  onUpdated,
}: {
  decision: CommitteeDecisionDto;
  user: UserDto;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  family: FamilyDto | null;
  onClose: () => void;
  onUpdated: () => void;
}) {
  const [decision, setDecision] = useState(initial);
  const [meetingDate, setMeetingDate] = useState(initial.meetingDate);
  const [summary, setSummary] = useState(initial.summary ?? '');
  const [editItem, setEditItem] = useState<AssistanceItemDto | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const familyBank = family ? toBankFields(family) : null;

  const editable = ['draft', 'returned_for_revision'].includes(decision.status);
  const canEditDraft = editable && hasPermission(user, PERMISSION_KEYS.committeeDecisionsEditDraft);
  const canAddItems = editable && hasPermission(user, PERMISSION_KEYS.assistanceItemsCreate);
  const canEditItems = editable && hasPermission(user, PERMISSION_KEYS.assistanceItemsEdit);
  const canRemoveItems = editable && hasPermission(user, PERMISSION_KEYS.assistanceItemsRemoveDraft);
  const canSubmit = editable && hasPermission(user, PERMISSION_KEYS.committeeDecisionsSubmit);
  const showActions = canEditItems || canRemoveItems;
  const canDeleteDraft = canEditDraft && decision.status === 'draft';

  async function refresh() {
    const fresh = await getCommitteeDecision(decision.id);
    setDecision(fresh);
    setMeetingDate(fresh.meetingDate);
    setSummary(fresh.summary ?? '');
    onUpdated();
  }

  async function handleSaveDraft() {
    if (!canEditDraft) return;
    setLoading(true);
    setError('');
    try {
      const updated = await updateCommitteeDecision(decision.id, decision.version, {
        meetingDate,
        summary: summary.trim() || null,
      });
      setDecision(updated);
      onUpdated();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  async function handleAddItem(payload: CreateAssistanceItemPayload) {
    const result = await addAssistanceItem(decision.id, decision.version, payload);
    setDecision((prev) => ({ ...prev, version: result.decisionVersion }));
    await refresh();
  }

  async function handleRemoveItem(item: AssistanceItemDto) {
    const updated = await removeAssistanceItem(decision.id, item.id, decision.version);
    setDecision(updated);
    onUpdated();
  }

  async function handleSubmitDecision() {
    setLoading(true);
    setError('');
    try {
      const updated = await submitCommitteeDecision(decision.id, decision.version);
      setDecision(updated);
      onUpdated();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  async function handleDelete() {
    if (!canDeleteDraft) return;
    const confirmed = window.confirm('למחוק לצמיתות את הטיוטה?\nפעולה זו אינה ניתנת לשחזור.');
    if (!confirmed) return;
    setLoading(true);
    setError('');
    try {
      await deleteCommitteeDecision(decision.id, decision.version);
      onUpdated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <ModalShell
        title={`החלטה ${decision.decisionCode}`}
        sizeClassName="modal-committee-expanded"
        loading={loading}
        onClose={onClose}
        formError={error}
        footer={(
          <>
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>סגור</button>
            {canEditDraft && (
              <button type="button" className="btn-secondary" onClick={handleSaveDraft} disabled={loading}>
                {loading ? 'שומר...' : 'שמור טיוטה'}
              </button>
            )}
            {canSubmit && decision.items.length > 0 && (
              <button type="button" onClick={handleSubmitDecision} disabled={loading}>
                {loading ? 'מגיש...' : 'הגש לאישור מנהל'}
              </button>
            )}
            {canDeleteDraft && (
              <button type="button" className="btn-secondary btn-danger" onClick={handleDelete} disabled={loading}>
                מחק החלטה
              </button>
            )}
          </>
        )}
      >
        <p>
          משפחה: <strong>{decision.familyCode}</strong> — {decision.familyLastName}
          {' · '}
          <span className={`status-badge status-${decision.status}`}>{translateDecisionStatus(decision.status)}</span>
        </p>

        {canEditDraft ? (
          <div className="decision-header-form">
            <label htmlFor="edit-meeting-date">תאריך ישיבה</label>
            <input id="edit-meeting-date" type="date" value={meetingDate} onChange={(e) => setMeetingDate(e.target.value)} disabled={loading} />
            <label htmlFor="edit-summary">סיכום</label>
            <textarea id="edit-summary" value={summary} onChange={(e) => setSummary(e.target.value)} disabled={loading} rows={2} />
          </div>
        ) : (
          <p>תאריך ישיבה: {decision.meetingDate}{decision.summary ? ` · ${decision.summary}` : ''}</p>
        )}

        <h3>פריטי סיוע ({decision.items.length})</h3>
        <div className="committee-items-shell">
          {canAddItems && (
            <ItemFormRow
              types={types}
              suppliers={suppliers}
              familyLastName={decision.familyLastName}
              familyBank={familyBank}
              onAdd={handleAddItem}
              disabled={loading}
            />
          )}

          <table className="org-table committee-items-table">
            <thead>
              <tr>
                <th>סוג סיוע</th>
                <th>תיאור</th>
                <th>יעד תשלום</th>
                <th>שם מוטב</th>
                <th>אופן תשלום</th>
                <th>פרטי בנק</th>
                <th>סכום</th>
                <th>דחוף</th>
                {showActions ? <th>פעולות</th> : <th aria-hidden="true" />}
              </tr>
            </thead>
            <tbody>
              {decision.items.length === 0 && (
                <tr><td colSpan={9} className="empty-row">אין פריטים</td></tr>
              )}
              {decision.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.assistanceTypeName}</td>
                  <td>{item.description ?? '—'}</td>
                  <td>{translatePaymentTarget(item.paymentTarget)}</td>
                  <td>{formatBeneficiaryName(item)}</td>
                  <td>{formatPaymentMethodCell(item)}</td>
                  <td>{resolveCommitteeBankDetailsDisplay(
                    item.paymentTarget,
                    item.paymentMethod,
                    {
                      familyBank,
                      supplierBank: item.supplierId
                        ? toBankFields(suppliers.find((s) => s.id === item.supplierId)!)
                        : null,
                      transferBankNumber: item.transferBankNumber,
                      transferBranchNumber: item.transferBranchNumber,
                      transferAccountNumber: item.transferAccountNumber,
                    },
                  )}</td>
                  <td className="col-amount">
                    {(() => {
                      const amt = formatAmountTracking(
                        item.amount,
                        item.hasAmountAdjustment,
                        item.originalApprovedAmount,
                        item.amountAdjustmentReason,
                        item.amountAdjustmentExplanation,
                      );
                      return (
                        <span className="amount-tracking">
                          <span className="amount-tracking__primary">{renderAmountTrackingPrimary(amt)}</span>
                          {amt.hint && <span className="amount-tracking__hint">{amt.hint}</span>}
                        </span>
                      );
                    })()}
                  </td>
                  <td>{item.isUrgent ? 'כן' : '—'}</td>
                  {showActions ? (
                    <td className="item-actions-cell">
                      {canEditItems && (
                        <button type="button" className="btn-small" onClick={() => setEditItem(item)} disabled={loading}>ערוך</button>
                      )}
                      {canRemoveItems && (
                        <button type="button" className="btn-small btn-danger" onClick={() => handleRemoveItem(item)} disabled={loading}>הסר</button>
                      )}
                    </td>
                  ) : (
                    <td aria-hidden="true">—</td>
                  )}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={6}><strong>סה״כ</strong></td>
                <td><strong>{decision.totalAmount.toLocaleString('he-IL')} ₪</strong></td>
                <td colSpan={2} />
              </tr>
            </tfoot>
          </table>
        </div>
      </ModalShell>

      {editItem && (
        <ItemEditModal
          item={editItem}
          types={types}
          suppliers={suppliers}
          familyLastName={decision.familyLastName}
          familyBank={familyBank}
          decisionId={decision.id}
          onClose={() => setEditItem(null)}
          onSaved={refresh}
        />
      )}
    </>
  );
}

function ReasonPromptModal({
  title,
  onClose,
  onConfirm,
}: {
  title: string;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void>;
}) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = reason.trim();
    if (trimmed.length < 3) {
      setError('יש לציין סיבה (לפחות 3 תווים)');
      return;
    }
    setLoading(true);
    setError('');
    try {
      await onConfirm(trimmed);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <ModalShell
      title={title}
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>ביטול</button>
          <button type="submit" disabled={loading}>{loading ? 'שולח...' : 'אישור'}</button>
        </>
      )}
    >
      <label htmlFor="action-reason">סיבה <span className="field-required">*</span></label>
      <textarea
        id="action-reason"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        disabled={loading}
        rows={3}
        maxLength={500}
      />
    </ModalShell>
  );
}

function toAssistanceItemDetails(item: AssistanceItemListDto) {
  return {
    decisionCode: item.decisionCode,
    familyCode: item.familyCode,
    familyAccountingCode: item.familyAccountingCode,
    familyLastName: item.familyName,
    assistanceTypeName: item.assistanceTypeName,
    assistanceTypeCode: item.assistanceTypeCode,
    description: item.description,
    amount: item.amount,
    originalApprovedAmount: item.originalApprovedAmount,
    previousPaymentAmount: item.previousPaymentAmount,
    amountAdjustmentReason: item.amountAdjustmentReason,
    amountAdjustmentExplanation: item.amountAdjustmentExplanation,
    hasAmountAdjustment: item.hasAmountAdjustment,
    paymentTarget: item.paymentTarget,
    paymentMethod: item.paymentMethod,
    payeeName: item.payeeName,
    supplierName: item.supplierName,
    supplierAccountingCode: item.supplierAccountingCode,
    transferBankNumber: item.transferBankNumber,
    transferBranchNumber: item.transferBranchNumber,
    transferAccountNumber: item.transferAccountNumber,
    accountHolderName: item.accountHolderName,
    voucherType: item.voucherType,
    isUrgent: item.isUrgent,
    executionReference: item.executionReference,
    status: item.status,
    createdAt: item.createdAt,
    updatedAt: item.updatedAt,
    version: item.version,
  };
}

export function CommitteeDecisionsPage({ user, initialFilter }: CommitteeDecisionsPageProps) {
  const [draftData, setDraftData] = useState<CommitteeDecisionListResponse | null>(null);
  const [items, setItems] = useState<AssistanceItemListDto[]>([]);
  const [families, setFamilies] = useState<FamilyDto[]>([]);
  const [types, setTypes] = useState<AssistanceTypeDto[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [detailTarget, setDetailTarget] = useState<CommitteeDecisionDto | null>(null);
  const [itemTarget, setItemTarget] = useState<AssistanceItemListDto | null>(null);
  const [activeFilter, setActiveFilter] = useState<HomeNavigationTarget | null | undefined>(initialFilter);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [pendingReason, setPendingReason] = useState<{
    title: string;
    onConfirm: (reason: string) => Promise<void>;
  } | null>(null);
  const [historyItemId, setHistoryItemId] = useState<string | null>(null);
  const [historyAnchorEl, setHistoryAnchorEl] = useState<HTMLElement | null>(null);

  const draftsTableRef = useRef<HTMLDivElement>(null);
  const itemsTableRef = useRef<HTMLDivElement>(null);
  const focusedTable = listViewFocus(activeFilter);

  const loadDrafts = useCallback(async (filter?: HomeNavigationTarget | null) => {
    const listOptions = resolveDraftListOptions(filter);
    const decisions = await listCommitteeDecisions(listOptions);
    setDraftData(decisions);
    return decisions;
  }, []);

  const loadItems = useCallback(async (filter?: HomeNavigationTarget | null) => {
    const listOptions = resolveItemListOptions(filter);
    const response = await listAssistanceItems(listOptions);
    setItems(response.items);
    return response.items;
  }, []);

  const load = useCallback(async (filter?: HomeNavigationTarget | null) => {
    setError('');
    try {
      const [familiesRes, typesRes, suppliersRes] = await Promise.all([
        listFamilies(),
        listAssistanceTypes(),
        listSuppliers(),
      ]);
      await Promise.all([loadDrafts(filter), loadItems(filter)]);
      setFamilies(familiesRes.families);
      setTypes(typesRes.assistanceTypes);
      setSuppliers(suppliersRes.suppliers);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, [loadDrafts, loadItems]);

  useEffect(() => {
    setActiveFilter(initialFilter);
    setLoading(true);
    load(initialFilter);
  }, [initialFilter, load]);

  useEffect(() => {
    if (focusedTable === 'draft_decisions') {
      draftsTableRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else if (focusedTable === 'assistance_items') {
      itemsTableRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, [focusedTable, loading]);

  const canCreate = hasPermission(user, PERMISSION_KEYS.committeeDecisionsCreate);
  const filterLabel = workflowFilterLabel(activeFilter);
  const detailFamily = detailTarget
    ? families.find((f) => f.id === detailTarget.familyId) ?? null
    : null;

  /** Meters from items currently loaded for this user (scoped list). */
  const itemStatusMeters = useMemo(() => {
    const order = [
      'submitted',
      'returned',
      'suspended',
      'approved',
      'rejected',
      'waiting_for_reference',
      'paid',
      'completed',
    ] as const;
    const counts = new Map<string, number>();
    for (const item of items) {
      counts.set(item.status, (counts.get(item.status) ?? 0) + 1);
    }
    return order
      .filter((status) => (counts.get(status) ?? 0) > 0)
      .map((status) => ({
        status,
        count: counts.get(status) ?? 0,
        label: assistanceItemStatusLabel(status),
        className: `summary-card summary-status-${status}`,
      }));
  }, [items]);

  async function openDecisionById(decisionId: string) {
    const decision = await getCommitteeDecision(decisionId);
    setDetailTarget(decision);
  }

  async function runItemTransition(
    item: AssistanceItemListDto,
    fn: () => Promise<AssistanceItemListDto>,
  ) {
    setActionLoading(item.id);
    setError('');
    try {
      const updated = await fn();
      await loadItems(activeFilter);
      if (itemTarget?.id === item.id) {
        setItemTarget(updated);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
      throw err;
    } finally {
      setActionLoading(null);
    }
  }

  function handleItemAction(item: AssistanceItemListDto, action: string) {
    // Payment execution (send / reference / adjust / export) is PaymentsQueuePage only (M97).
    if (!DECISIONS_ITEM_ACTIONS.has(action)) return;

    if (action === 'edit') {
      void openDecisionById(item.decisionId);
      return;
    }
    if (action === 'approve') {
      void runItemTransition(item, () => approveAssistanceItem(item.id, item.version));
      return;
    }
    if (action === 'resubmit') {
      void runItemTransition(item, () => resubmitAssistanceItem(item.id, item.version));
      return;
    }
    if (action === 'complete') {
      // Backend includes `complete` only after paid + permission.
      if (!item.availableActions.includes('complete') || item.status !== 'paid') return;
      void runItemTransition(item, () => completeAssistanceItem(item.id, item.version));
      return;
    }
    if (action === 'reject') {
      setPendingReason({
        title: workflowActionLabel('reject'),
        onConfirm: (reason) => runItemTransition(item, () => rejectAssistanceItem(item.id, item.version, reason)),
      });
      return;
    }
    if (action === 'return') {
      setPendingReason({
        title: workflowActionLabel('return'),
        onConfirm: (reason) => runItemTransition(item, () => returnAssistanceItem(item.id, item.version, reason)),
      });
      return;
    }
    if (action === 'suspend') {
      setPendingReason({
        title: workflowActionLabel('suspend'),
        onConfirm: (reason) => runItemTransition(item, () => suspendAssistanceItem(item.id, item.version, reason)),
      });
    }
  }

  async function runDecisionAction(decision: CommitteeDecisionDto, action: string) {
    if (action === 'cancel') {
      setPendingReason({
        title: workflowActionLabel('cancel'),
        onConfirm: async (reason) => {
          setActionLoading(decision.id);
          setError('');
          try {
            await cancelCommitteeDecision(decision.id, decision.version, reason);
            await load(activeFilter);
          } catch (err) {
            setError(err instanceof Error ? err.message : 'שגיאת מערכת');
            throw err;
          } finally {
            setActionLoading(null);
          }
        },
      });
      return;
    }

    setActionLoading(decision.id);
    setError('');
    try {
      if (action === 'edit') {
        setDetailTarget(decision);
        return;
      }
      if (action === 'submit') {
        if (!window.confirm('להגיש את ההחלטה לאישור?')) return;
        await submitCommitteeDecision(decision.id, decision.version);
        await load(activeFilter);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setActionLoading(null);
    }
  }

  function renderDecisionActions(decision: CommitteeDecisionDto) {
    const busy = actionLoading === decision.id;
    return (
      <td className="actions-cell">
        {decision.availableActions.map((action) => (
          <button
            key={action}
            type="button"
            className={workflowActionButtonClass(action)}
            disabled={busy}
            onClick={() => { void runDecisionAction(decision, action); }}
          >
            {workflowActionLabel(action)}
          </button>
        ))}
      </td>
    );
  }

  function renderItemActions(item: AssistanceItemListDto) {
    const busy = actionLoading === item.id;
    const actions = decisionsItemActions(item.availableActions);
    const showHistory = (item.availableActions ?? []).includes('view_history');
    return (
      <td className="actions-cell actions-cell--with-history">
        <div className="actions-cell__business">
          <button
            type="button"
            className="btn-small btn-action-neutral"
            disabled={busy}
            onClick={() => setItemTarget(item)}
          >
            פרטים
          </button>
          {actions.map((action) => (
            <button
              key={action}
              type="button"
              className={workflowActionButtonClass(action)}
              disabled={busy}
              onClick={() => handleItemAction(item, action)}
            >
              {workflowActionLabel(action)}
            </button>
          ))}
        </div>
        {showHistory && (
          <HistoryIconButton
            disabled={busy}
            onClick={(anchor) => {
              setHistoryAnchorEl(anchor);
              setHistoryItemId(item.id);
            }}
          />
        )}
      </td>
    );
  }

  return (
    <div className="committee-decisions-page queue-page">
      {filterLabel && (
        <div className="toolbar">
          <span className="filter-chip">סינון: {filterLabel}</span>
          <button type="button" className="btn-secondary" onClick={() => {
            setActiveFilter(null);
            setLoading(true);
            void load(null);
          }}>
            נקה סינון
          </button>
        </div>
      )}

      {!loading && (
        <div className="summary-cards summary-cards--page-top">
          <div className="summary-card summary-total">
            <span className="summary-label">סה״כ פריטים</span>
            <span className="summary-value">{items.length}</span>
          </div>
          {draftData && (
            <div className="summary-card summary-draft">
              <span className="summary-label">טיוטות</span>
              <span className="summary-value">{draftData.summary.draft}</span>
            </div>
          )}
          {itemStatusMeters.map((meter) => (
            <div key={meter.status} className={meter.className}>
              <span className="summary-label">{meter.label}</span>
              <span className="summary-value">{meter.count}</span>
            </div>
          ))}
        </div>
      )}

      <div className="toolbar">
        {canCreate && (
          <button type="button" onClick={() => setShowCreate(true)}>החלטה חדשה</button>
        )}
        <button type="button" className="btn-secondary" onClick={() => load(activeFilter)}>רענן</button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען...</p>
      ) : (
        <>
          <section
            ref={draftsTableRef}
            className="committee-table-section committee-table-section--drafts queue-pane--secondary"
            style={focusedTable === 'draft_decisions' ? { outline: '2px solid #4338ca', borderRadius: '8px', padding: '0.5rem' } : undefined}
            aria-label="טיוטות החלטות"
          >
            <h2>טיוטות החלטות</h2>
            <div className="table-wrap table-wrap--scroll table-wrap--secondary">
              <table className="org-table">
                <thead>
                  <tr>
                    <th>קוד החלטה</th>
                    <th>משפחה</th>
                    <th>תאריך ישיבה</th>
                    <th>סכום</th>
                    <th className="col-status">סטטוס</th>
                    <th>נוצר ע״י</th>
                    <th>פעולות</th>
                  </tr>
                </thead>
                <tbody>
                  {(draftData?.decisions ?? []).length === 0 && (
                    <tr><td colSpan={7} className="empty-row">אין טיוטות להצגה</td></tr>
                  )}
                  {(draftData?.decisions ?? []).map((d) => (
                    <tr key={d.id}>
                      <td><code>{d.decisionCode}</code></td>
                      <td>{d.familyCode} — {d.familyLastName}</td>
                      <td>{d.meetingDate}</td>
                      <td>{d.totalAmount.toLocaleString('he-IL')} ₪</td>
                      <td className="col-status">
                        <span className={`status-badge status-${d.status}`}>
                          {translateDecisionStatus(d.status)}
                        </span>
                      </td>
                      <td>{d.createdByUserName}</td>
                      {renderDecisionActions(d)}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section
            ref={itemsTableRef}
            className="committee-table-section committee-table-section--items queue-pane--primary"
            style={focusedTable === 'assistance_items' ? { outline: '2px solid #4338ca', borderRadius: '8px', padding: '0.5rem' } : undefined}
            aria-label="פריטי סיוע"
          >
            <h2>פריטי סיוע</h2>
            <div className="table-wrap table-wrap--scroll table-wrap--primary">
              <table className="org-table">
                <thead>
                  <tr>
                    <th>קוד החלטה</th>
                    <th>קוד משפחה</th>
                    <th>שם משפחה</th>
                    <th>סוג סיוע</th>
                    <th className="col-amount">סכום</th>
                    <th>דחוף</th>
                    <th className="col-status">סטטוס פריט</th>
                    <th>תאריך הגשה/יצירה</th>
                    <th>פעולות</th>
                  </tr>
                </thead>
                <tbody>
                  {items.length === 0 && (
                    <tr><td colSpan={9} className="empty-row">אין פריטי סיוע להצגה</td></tr>
                  )}
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td><code>{item.decisionCode}</code></td>
                      <td>{item.familyCode}</td>
                      <td>{item.familyName}</td>
                      <td>{item.assistanceTypeName}</td>
                      <td className="col-amount">
                        {(() => {
                          const amt = formatAmountTracking(
                            item.amount,
                            item.hasAmountAdjustment,
                            item.originalApprovedAmount,
                            item.amountAdjustmentReason,
                            item.amountAdjustmentExplanation,
                          );
                          return (
                            <span className="amount-tracking">
                              <span className="amount-tracking__primary">{renderAmountTrackingPrimary(amt)}</span>
                              {amt.hint && <span className="amount-tracking__hint">{amt.hint}</span>}
                            </span>
                          );
                        })()}
                      </td>
                      <td>{item.isUrgent ? 'כן' : 'לא'}</td>
                      <td className="col-status">
                        <span className={`status-badge status-${item.status}`}>
                          {assistanceItemStatusLabel(item.status)}
                        </span>
                      </td>
                      <td>{(item.submittedAt ?? item.createdAt).slice(0, 10)}</td>
                      {renderItemActions(item)}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}

      {showCreate && (
        <CreateDecisionModal
          families={families}
          onClose={() => setShowCreate(false)}
          onCreated={(d) => { void load(activeFilter); setDetailTarget(d); }}
        />
      )}
      {detailTarget && (
        <DecisionDetailPanel
          decision={detailTarget}
          user={user}
          types={types}
          suppliers={suppliers}
          family={detailFamily}
          onClose={() => setDetailTarget(null)}
          onUpdated={() => load(activeFilter)}
        />
      )}
      {itemTarget && (
        <AssistanceItemDetailsModal
          item={toAssistanceItemDetails(itemTarget)}
          onClose={() => setItemTarget(null)}
        />
      )}
      {pendingReason && (
        <ReasonPromptModal
          title={pendingReason.title}
          onClose={() => setPendingReason(null)}
          onConfirm={async (reason) => {
            await pendingReason.onConfirm(reason);
            setPendingReason(null);
          }}
        />
      )}
      {historyItemId && (
        <AssistanceItemHistoryModal
          assistanceItemId={historyItemId}
          anchorEl={historyAnchorEl}
          onClose={() => {
            setHistoryItemId(null);
            setHistoryAnchorEl(null);
          }}
        />
      )}
    </div>
  );
}
