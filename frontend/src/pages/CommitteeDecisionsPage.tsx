import { useCallback, useEffect, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import type { UserDto } from '../api/auth';
import { listAssistanceTypes, type AssistanceTypeDto } from '../api/assistanceTypes';
import {
  addAssistanceItem,
  cancelCommitteeDecision,
  createCommitteeDecision,
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
import { listFamilies, type FamilyDto } from '../api/families';
import { PERMISSION_KEYS } from '../api/permissions';
import { listSuppliers, type SupplierDto } from '../api/suppliers';
import type { HomeNavigationTarget } from '../api/workflow';
import { workflowFilterLabel } from './home/workflowStatus';
import { hasPermission } from '../hooks/usePermissions';
import { FieldValidationTooltip } from '../components/FieldValidation';
import { ModalShell } from '../components/ModalShell';
import { focusFirstInvalidField } from '../utils/formValidation';
import { partitionSuppliersForAssistanceType } from '../utils/relatedSuppliers';
import { type BankFields } from '../validation/bankFields';
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
  type CommitteeItemRowState,
  validateCommitteeItemRow,
} from '../validation/committeeItemPayment';
import { firstBankFieldError, validateBankFieldErrors } from '../validation/bankFields';

interface CommitteeDecisionsPageProps {
  user: UserDto;
  initialFilter?: HomeNavigationTarget | null;
}

function listFilterToOptions(filter: HomeNavigationTarget | null | undefined) {
  if (!filter || filter.targetTab !== 'decisions') return undefined;
  return {
    section: filter.section,
    status: filter.status,
    ownership: filter.ownership,
    minAgeDays: filter.minAgeDays,
  };
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

function TransferBankModal({
  initial,
  payeeName,
  onSave,
  onClose,
}: {
  initial: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>;
  payeeName: string;
  onSave: (values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) => void;
  onClose: () => void;
}) {
  const [bankNumber, setBankNumber] = useState(initial.transferBankNumber);
  const [branchNumber, setBranchNumber] = useState(initial.transferBranchNumber);
  const [accountNumber, setAccountNumber] = useState(initial.transferAccountNumber);
  const [error, setError] = useState('');

  function handleSave(e: FormEvent) {
    e.preventDefault();
    const validationError = firstBankFieldError(validateBankFieldErrors(
      bankNumber,
      branchNumber,
      accountNumber,
      payeeName,
    ));
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
    <ModalShell
      title="פרטי העברה בנקאית"
      onClose={onClose}
      onSubmit={handleSave}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={onClose}>ביטול</button>
          <button type="submit">שמור</button>
        </>
      )}
    >
      <p className="hint-text">שם בעל החשבון: <strong>{payeeName.trim() || '—'}</strong></p>
      <label htmlFor="transfer-bank-number">קוד בנק <span className="field-required">*</span></label>
      <input
        id="transfer-bank-number"
        type="text"
        inputMode="numeric"
        value={bankNumber}
        onChange={(e) => setBankNumber(e.target.value)}
      />
      <label htmlFor="transfer-branch-number">סניף <span className="field-required">*</span></label>
      <input
        id="transfer-branch-number"
        type="text"
        inputMode="numeric"
        value={branchNumber}
        onChange={(e) => setBranchNumber(e.target.value)}
      />
      <label htmlFor="transfer-account-number">מספר חשבון <span className="field-required">*</span></label>
      <input
        id="transfer-account-number"
        type="text"
        inputMode="numeric"
        value={accountNumber}
        onChange={(e) => setAccountNumber(e.target.value)}
      />
    </ModalShell>
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
  onOpenTransferModal,
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
  onOpenTransferModal: () => void;
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
  const showTransferColumn = state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer';

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
      onOpenTransferModal();
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

      <div className="committee-item-form__field">
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

      <div className="committee-item-form__field">
        <label htmlFor={fieldId('transfer-details')}>פרטי העברה</label>
        {showTransferColumn ? (
          <button
            id={fieldId('transfer-details')}
            type="button"
            className="btn-secondary btn-small committee-transfer-summary-btn"
            onClick={onOpenTransferModal}
            disabled={disabled}
            title={transferSummary}
          >
            {isTransferBankComplete(state) ? transferSummary : 'הזן פרטים'}
          </button>
        ) : (
          <input id={fieldId('transfer-details')} type="text" disabled value="—" />
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
          <label>פעולות</label>
          <div className="validated-field-control">
            <button type="button" className="btn-small" onClick={onAdd} disabled={disabled || addLoading}>
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
  const [transferModalOpen, setTransferModalOpen] = useState(false);

  const selectedSupplier = state.supplierId
    ? suppliers.find((s) => s.id === state.supplierId) ?? null
    : null;
  const supplierBank = selectedSupplier ? toBankFields(selectedSupplier) : null;

  function openTransferModal() {
    if (needsTransferBankModal(state)) {
      setTransferModalOpen(true);
    }
  }

  function handleTransferModalCancel() {
    setTransferModalOpen(false);
    setState((prev) => applyPaymentMethodChange('', prev));
  }

  function handleTransferModalSave(values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) {
    setState((prev) => ({ ...prev, ...values }));
    setTransferModalOpen(false);
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
          onOpenTransferModal={openTransferModal}
        />
      </div>
      {transferModalOpen && (
        <TransferBankModal
          initial={state}
          payeeName={state.payeeName}
          onSave={handleTransferModalSave}
          onClose={handleTransferModalCancel}
        />
      )}
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
  const [transferModalOpen, setTransferModalOpen] = useState(false);

  const selectedSupplier = state.supplierId
    ? suppliers.find((s) => s.id === state.supplierId) ?? null
    : null;
  const supplierBank = selectedSupplier ? toBankFields(selectedSupplier) : null;

  function openTransferModal() {
    if (needsTransferBankModal(state)) {
      setTransferModalOpen(true);
    }
  }

  function handleTransferModalCancel() {
    setTransferModalOpen(false);
    setState((prev) => applyPaymentMethodChange('', prev));
  }

  function handleTransferModalSave(values: Pick<CommitteeItemRowState, 'transferBankNumber' | 'transferBranchNumber' | 'transferAccountNumber'>) {
    setState((prev) => ({ ...prev, ...values }));
    setTransferModalOpen(false);
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
              onOpenTransferModal={openTransferModal}
            />
          </div>
        </div>
      </ModalShell>
      {transferModalOpen && (
        <TransferBankModal
          initial={state}
          payeeName={state.payeeName}
          onSave={handleTransferModalSave}
          onClose={handleTransferModalCancel}
        />
      )}
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
  const canCancel = hasPermission(user, PERMISSION_KEYS.committeeDecisionsCancel);
  const showActions = canEditItems || canRemoveItems;

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

  async function handleCancel() {
    const reason = window.prompt('סיבת ביטול:');
    if (!reason || reason.trim().length < 3) return;
    setLoading(true);
    try {
      await cancelCommitteeDecision(decision.id, decision.version, reason.trim());
      await refresh();
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
        wide
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
                <th>פרטי העברה</th>
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
                  <td>{formatTransferDetailsSummary(
                    item.paymentTarget,
                    item.paymentMethod,
                    item.transferBankNumber,
                    item.transferBranchNumber,
                    item.transferAccountNumber,
                  )}</td>
                  <td>{item.amount.toLocaleString('he-IL')} ₪</td>
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

        {canCancel && decision.status !== 'cancelled' && (
          <button type="button" className="btn-secondary btn-danger decision-cancel-btn" onClick={handleCancel} disabled={loading}>בטל החלטה</button>
        )}
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

export function CommitteeDecisionsPage({ user, initialFilter }: CommitteeDecisionsPageProps) {
  const [data, setData] = useState<CommitteeDecisionListResponse | null>(null);
  const [families, setFamilies] = useState<FamilyDto[]>([]);
  const [types, setTypes] = useState<AssistanceTypeDto[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [detailTarget, setDetailTarget] = useState<CommitteeDecisionDto | null>(null);
  const [activeFilter, setActiveFilter] = useState<HomeNavigationTarget | null | undefined>(initialFilter);

  const load = useCallback(async (filter?: HomeNavigationTarget | null) => {
    setError('');
    try {
      const listOptions = listFilterToOptions(filter);
      const [decisions, familiesRes, typesRes, suppliersRes] = await Promise.all([
        listCommitteeDecisions(listOptions),
        listFamilies(),
        listAssistanceTypes(),
        listSuppliers(),
      ]);
      setData(decisions);
      setFamilies(familiesRes.families);
      setTypes(typesRes.assistanceTypes);
      setSuppliers(suppliersRes.suppliers);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    setActiveFilter(initialFilter);
    setLoading(true);
    load(initialFilter);
  }, [initialFilter, load]);

  const canCreate = hasPermission(user, PERMISSION_KEYS.committeeDecisionsCreate);
  const filterLabel = workflowFilterLabel(activeFilter);
  const detailFamily = detailTarget
    ? families.find((f) => f.id === detailTarget.familyId) ?? null
    : null;

  return (
    <div>
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
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card">
            <span className="summary-label">טיוטות</span>
            <span className="summary-value">{data.summary.draft}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">הוגשו</span>
            <span className="summary-value">{data.summary.submitted}</span>
          </div>
          <div className="summary-card">
            <span className="summary-label">אושרו</span>
            <span className="summary-value">{data.summary.approved}</span>
          </div>
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
        <p>טוען החלטות...</p>
      ) : (
        <div className="table-wrap">
          <table className="org-table">
            <thead>
              <tr>
                <th>קוד</th>
                <th>משפחה</th>
                <th>תאריך ישיבה</th>
                <th>סכום</th>
                <th>סטטוס</th>
                <th>נוצר ע״י</th>
                <th>פעולות</th>
              </tr>
            </thead>
            <tbody>
              {(data?.decisions ?? []).length === 0 && (
                <tr><td colSpan={7} className="empty-row">אין החלטות להצגה</td></tr>
              )}
              {(data?.decisions ?? []).map((d) => (
                <tr key={d.id}>
                  <td><code>{d.decisionCode}</code></td>
                  <td>{d.familyCode} — {d.familyLastName}</td>
                  <td>{d.meetingDate}</td>
                  <td>{d.totalAmount.toLocaleString('he-IL')} ₪</td>
                  <td>
                    <span className={`status-badge status-${d.status}`}>{translateDecisionStatus(d.status)}</span>
                  </td>
                  <td>{d.createdByUserName}</td>
                  <td>
                    <button type="button" className="btn-small" onClick={() => setDetailTarget(d)}>פתח</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCreate && (
        <CreateDecisionModal
          families={families}
          onClose={() => setShowCreate(false)}
          onCreated={(d) => { load(activeFilter); setDetailTarget(d); }}
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
    </div>
  );
}
