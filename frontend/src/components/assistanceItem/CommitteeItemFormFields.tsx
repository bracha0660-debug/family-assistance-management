import { useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { PAYMENT_TARGETS, type PaymentMethod, type PaymentTarget } from '../../api/committeeDecisions';
import type { AssistanceTypeDto } from '../../api/assistanceTypes';
import type { SupplierDto } from '../../api/suppliers';
import { BankSelect } from '../BankDetailsFields';
import { FieldValidationTooltip } from '../FieldValidation';
import { partitionSuppliersForAssistanceType } from '../../utils/relatedSuppliers';
import { validateBankFieldsForPayment, type BankFields } from '../../validation/bankFields';
import {
  applyPaymentMethodChange,
  applyPaymentTargetChange,
  D8_CONFIRM_MESSAGE,
  formatTransferDetailsSummary,
  getAllowedPaymentMethods,
  hasMeaningfulPaymentData,
  isTransferBankComplete,
  needsTransferBankModal,
  onAssistanceTypeChange,
  PAYEE_NAME_REQUIRED_MESSAGE,
  revalidateAfterBeneficiaryChange,
  resolveCommitteeBankDetailsDisplay,
  type CommitteeItemRowState,
} from '../../validation/committeeItemPayment';
import { toBankFields, translatePaymentMethod, translatePaymentTarget } from './itemFormUtils';

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

export function CommitteeItemFormFields({
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

