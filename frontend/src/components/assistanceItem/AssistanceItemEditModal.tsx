import { useState, type FormEvent } from 'react';
import {
  updateAssistanceItem,
  type AssistanceItemDto,
} from '../../api/committeeDecisions';
import type { AssistanceTypeDto } from '../../api/assistanceTypes';
import type { SupplierDto } from '../../api/suppliers';
import { FormField, ModalShell } from '../ModalShell';
import { focusFirstInvalidField } from '../../utils/formValidation';
import type { BankFields } from '../../validation/bankFields';
import {
  validateCommitteeItemRow,
  type CommitteeItemRowState,
} from '../../validation/committeeItemPayment';
import { AMOUNT_ADJUSTMENT_REASONS } from '../../api/exportBatches';
import { CommitteeItemFormFields } from './CommitteeItemFormFields';
import {
  buildUpdatePayload,
  EDIT_ITEM_FOCUS_ORDER,
  itemToRowState,
  toBankFields,
} from './itemFormUtils';

/** Minimal item shape accepted by the shared single-item editor. */
export type AssistanceItemEditFields = Pick<
  AssistanceItemDto,
  | 'id'
  | 'assistanceTypeId'
  | 'description'
  | 'amount'
  | 'paymentTarget'
  | 'paymentMethod'
  | 'supplierId'
  | 'payeeName'
  | 'transferBankNumber'
  | 'transferBranchNumber'
  | 'transferAccountNumber'
  | 'voucherType'
  | 'isUrgent'
  | 'version'
> & {
  lineNumber?: number;
  supplierId: string | null;
};

export type AssistanceItemEditSaveResult =
  | { mode: 'committee' }
  | {
      mode: 'payment';
      fields: Record<string, string | null>;
      amountAdjustmentReason: string | null;
      amountAdjustmentExplanation: string | null;
    };

export function AssistanceItemEditModal({
  item,
  types,
  suppliers,
  familyLastName,
  familyBank,
  decisionId,
  saveMode = 'committee',
  initialAmount,
  onClose,
  onSaved,
  onPaymentSave,
}: {
  item: AssistanceItemEditFields;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  familyLastName: string;
  familyBank: BankFields | null;
  decisionId: string;
  saveMode?: 'committee' | 'payment';
  initialAmount?: number;
  onClose: () => void;
  onSaved: () => void;
  onPaymentSave?: (
    fields: Record<string, string | null>,
    amountReason: string | null,
    amountExplanation: string | null,
  ) => Promise<void>;
}) {
  const baselineAmount = initialAmount ?? item.amount;
  const [state, setState] = useState<CommitteeItemRowState>(() => {
    const row = itemToRowState(item as AssistanceItemDto);
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
  const [amountReason, setAmountReason] = useState('typing_error');
  const [amountExplanation, setAmountExplanation] = useState('');
  const [explanationError, setExplanationError] = useState<string | null>(null);

  const selectedSupplier = state.supplierId
    ? suppliers.find((s) => s.id === state.supplierId) ?? null
    : null;
  const supplierBank = selectedSupplier ? toBankFields(selectedSupplier) : null;
  const amountChanged = Number(state.amount) !== Number(baselineAmount);
  const needsExplanation = saveMode === 'payment' && amountChanged && amountReason === 'other';

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
    setExplanationError(null);
    const validationError = validateCommitteeItemRow(state, familyBank, supplierBank);
    if (validationError) {
      setError(validationError);
      focusFirstInvalidField(EDIT_ITEM_FOCUS_ORDER);
      return;
    }

    if (needsExplanation) {
      const trimmed = amountExplanation.trim();
      if (trimmed.length < 3) {
        setExplanationError('יש למלא הסבר לשינוי הסכום');
        document.getElementById('edit-item-amount-explanation')?.focus();
        return;
      }
    }

    setLoading(true);
    try {
      if (saveMode === 'payment') {
        if (!onPaymentSave) throw new Error('שגיאת מערכת');
        const fields: Record<string, string | null> = {
          assistance_type_id: state.assistanceTypeId,
          description: state.description.trim() || null,
          amount: String(Number(state.amount)),
          payment_target: state.paymentTarget || null,
          payment_method: state.paymentMethod || null,
          supplier_id: state.paymentTarget === 'supplier' ? (state.supplierId || null) : null,
          beneficiary: (state.paymentTarget === 'family' || state.paymentTarget === 'other')
            ? (state.payeeName.trim() || null)
            : null,
          bank_number: state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer'
            ? state.transferBankNumber.trim() || null
            : null,
          branch_number: state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer'
            ? state.transferBranchNumber.trim() || null
            : null,
          account_number: state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer'
            ? state.transferAccountNumber.trim() || null
            : null,
          account_holder_name: state.paymentTarget === 'other' && state.paymentMethod === 'bank_transfer'
            ? (state.payeeName.trim() || null)
            : null,
        };
        await onPaymentSave(
          fields,
          amountChanged ? amountReason : null,
          amountChanged && needsExplanation ? amountExplanation.trim() : null,
        );
      } else {
        await updateAssistanceItem(
          decisionId,
          item.id,
          item.version,
          buildUpdatePayload(state, item.supplierId),
        );
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  const title = item.lineNumber != null
    ? `עריכת פריט #${item.lineNumber}`
    : 'עריכת פריט סיוע';

  return (
    <ModalShell
      title={title}
      sizeClassName="modal-committee-expanded modal-item-edit"
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
        {saveMode === 'payment' && amountChanged && (
          <div className="committee-item-form" style={{ marginTop: '0.75rem' }}>
            <FormField id="edit-item-amount-reason" label="סיבת שינוי סכום">
              <select
                id="edit-item-amount-reason"
                value={amountReason}
                onChange={(e) => {
                  setAmountReason(e.target.value);
                  setExplanationError(null);
                }}
              >
                {AMOUNT_ADJUSTMENT_REASONS.map((r) => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
            </FormField>
            {needsExplanation && (
              <FormField
                id="edit-item-amount-explanation"
                label={<>הסבר <span className="field-required">*</span></>}
                error={explanationError}
              >
                <textarea
                  id="edit-item-amount-explanation"
                  rows={3}
                  value={amountExplanation}
                  onChange={(e) => {
                    setAmountExplanation(e.target.value);
                    if (explanationError) setExplanationError(null);
                  }}
                  required
                  aria-invalid={explanationError ? true : undefined}
                />
              </FormField>
            )}
          </div>
        )}
      </div>
    </ModalShell>
  );
}
