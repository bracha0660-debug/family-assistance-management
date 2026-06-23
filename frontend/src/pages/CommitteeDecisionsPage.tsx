import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
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
  PAYMENT_METHODS,
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

function formatPayeeTransfer(item: AssistanceItemDto): string {
  const base = item.supplierName ?? item.payeeName ?? '—';
  if (item.paymentMethod === 'vouchers' && item.voucherType) {
    return base === '—' ? item.voucherType : `${base} (${item.voucherType})`;
  }
  return base;
}

const ITEM_FOCUS_ORDER = [
  'item-assistance-type',
  'item-payment-target',
  'item-payment-method',
  'item-payee-transfer',
  'item-amount',
];

function validateItemFields(
  assistanceTypeId: string,
  amount: string,
  paymentTarget: PaymentTarget | '',
  paymentMethod: PaymentMethod | '',
  supplierId: string,
  payeeName: string,
): string | null {
  const parsedAmount = Number(amount);
  if (!assistanceTypeId || !Number.isFinite(parsedAmount) || parsedAmount <= 0) {
    return 'יש לבחור סוג סיוע ולהזין סכום חיובי';
  }
  if (!paymentTarget) return 'יש לבחור יעד תשלום';
  if (!paymentMethod) return 'יש לבחור אופן תשלום';
  if (paymentTarget === 'supplier' && !supplierId) return 'יש לבחור ספק';
  if (paymentTarget === 'other' && !payeeName.trim()) return 'יש להזין שם מוטב';
  return null;
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
  onAdd,
  disabled,
}: {
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  onAdd: (payload: CreateAssistanceItemPayload) => Promise<void>;
  disabled: boolean;
}) {
  const [assistanceTypeId, setAssistanceTypeId] = useState('');
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [paymentTarget, setPaymentTarget] = useState<PaymentTarget | ''>('');
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | ''>('');
  const [supplierId, setSupplierId] = useState('');
  const [payeeName, setPayeeName] = useState('');
  const [voucherType, setVoucherType] = useState('');
  const [isUrgent, setIsUrgent] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleAdd() {
    setError('');
    const validationError = validateItemFields(
      assistanceTypeId, amount, paymentTarget, paymentMethod, supplierId, payeeName,
    );
    if (validationError) {
      setError(validationError);
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    const parsedAmount = Number(amount);
    setLoading(true);
    try {
      await onAdd({
        assistanceTypeId,
        description: description.trim() || null,
        amount: parsedAmount,
        paymentTarget: paymentTarget as PaymentTarget,
        paymentMethod: paymentMethod as PaymentMethod,
        supplierId: paymentTarget === 'supplier' ? supplierId : null,
        payeeName: paymentTarget === 'other' ? payeeName.trim() : null,
        voucherType: paymentMethod === 'vouchers' ? voucherType.trim() || null : null,
        isUrgent,
      });
      setDescription('');
      setAmount('');
      setPaymentTarget('');
      setPaymentMethod('');
      setSupplierId('');
      setPayeeName('');
      setVoucherType('');
      setIsUrgent(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="item-form-row">
      <div className="item-form-field">
        <label htmlFor="item-assistance-type">סוג סיוע</label>
        <select id="item-assistance-type" value={assistanceTypeId} onChange={(e) => setAssistanceTypeId(e.target.value)} disabled={disabled || loading}>
          <option value="">— בחר —</option>
          {types.filter((t) => t.status === 'active').map((t) => (
            <option key={t.id} value={t.id}>{t.name}</option>
          ))}
        </select>
      </div>
      <div className="item-form-field">
        <label htmlFor="item-description">תיאור</label>
        <input id="item-description" type="text" placeholder="תיאור" value={description} onChange={(e) => setDescription(e.target.value)} disabled={disabled || loading} />
      </div>
      <div className="item-form-field">
        <label htmlFor="item-payment-target">יעד תשלום</label>
        <select
          id="item-payment-target"
          value={paymentTarget}
          onChange={(e) => setPaymentTarget(e.target.value as PaymentTarget | '')}
          disabled={disabled || loading}
          aria-invalid={error.includes('יעד') ? true : undefined}
        >
          <option value="">— בחר —</option>
          {PAYMENT_TARGETS.map((t) => (
            <option key={t} value={t}>{translatePaymentTarget(t)}</option>
          ))}
        </select>
      </div>
      <div className="item-form-field">
        <label htmlFor="item-payment-method">אופן תשלום</label>
        <select
          id="item-payment-method"
          value={paymentMethod}
          onChange={(e) => setPaymentMethod(e.target.value as PaymentMethod | '')}
          disabled={disabled || loading}
          aria-invalid={error.includes('אופן') ? true : undefined}
        >
          <option value="">— בחר —</option>
          {PAYMENT_METHODS.map((m) => (
            <option key={m} value={m}>{translatePaymentMethod(m)}</option>
          ))}
        </select>
      </div>
      <div className="item-form-field item-form-payee">
        <label htmlFor="item-payee-transfer">מוטב / העברה</label>
        {paymentTarget === 'supplier' ? (
          <>
            <select id="item-payee-transfer" value={supplierId} onChange={(e) => setSupplierId(e.target.value)} disabled={disabled || loading}>
              <option value="">— בחר ספק —</option>
              {suppliers.filter((s) => s.status === 'active').map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            {paymentMethod === 'vouchers' && (
              <input type="text" placeholder="סוג שובר" value={voucherType} onChange={(e) => setVoucherType(e.target.value)} disabled={disabled || loading} />
            )}
          </>
        ) : paymentTarget === 'other' ? (
          <input id="item-payee-transfer" type="text" placeholder="שם מוטב" value={payeeName} onChange={(e) => setPayeeName(e.target.value)} disabled={disabled || loading} />
        ) : paymentMethod === 'vouchers' ? (
          <input id="item-payee-transfer" type="text" placeholder="סוג שובר" value={voucherType} onChange={(e) => setVoucherType(e.target.value)} disabled={disabled || loading} />
        ) : (
          <input id="item-payee-transfer" type="text" disabled placeholder="—" />
        )}
      </div>
      <div className="item-form-field">
        <label htmlFor="item-amount">סכום</label>
        <input id="item-amount" type="number" placeholder="סכום" value={amount} onChange={(e) => setAmount(e.target.value)} disabled={disabled || loading} min={0} step={0.01} />
      </div>
      <label className="checkbox-label item-form-urgent">
        <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} disabled={disabled || loading} />
        דחוף
      </label>
      <div className="item-form-actions validated-field-control">
        <button type="button" className="btn-small" onClick={handleAdd} disabled={disabled || loading}>הוסף שורה</button>
        {error && <FieldValidationTooltip id="item-form-error" message={error} />}
      </div>
    </div>
  );
}

function ItemEditModal({
  item,
  types,
  suppliers,
  decisionId,
  onClose,
  onSaved,
}: {
  item: AssistanceItemDto;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  decisionId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [assistanceTypeId, setAssistanceTypeId] = useState(item.assistanceTypeId);
  const [description, setDescription] = useState(item.description ?? '');
  const [amount, setAmount] = useState(String(item.amount));
  const [paymentTarget, setPaymentTarget] = useState<PaymentTarget>(item.paymentTarget as PaymentTarget);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>(item.paymentMethod as PaymentMethod);
  const [supplierId, setSupplierId] = useState(item.supplierId ?? '');
  const [payeeName, setPayeeName] = useState(item.payeeName ?? '');
  const [voucherType, setVoucherType] = useState(item.voucherType ?? '');
  const [isUrgent, setIsUrgent] = useState(item.isUrgent);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    const validationError = validateItemFields(
      assistanceTypeId, amount, paymentTarget, paymentMethod, supplierId, payeeName,
    );
    if (validationError) {
      setError(validationError);
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    const parsedAmount = Number(amount);
    const payload: UpdateAssistanceItemPayload = {
      assistanceTypeId,
      description: description.trim() || null,
      amount: parsedAmount,
      paymentTarget,
      paymentMethod,
      isUrgent,
      voucherType: paymentMethod === 'vouchers' ? voucherType.trim() || null : null,
    };
    if (paymentTarget === 'supplier') {
      payload.supplierId = supplierId;
    } else if (item.supplierId) {
      payload.clearSupplierId = true;
    }
    if (paymentTarget === 'other') {
      payload.payeeName = payeeName.trim();
    }
    setLoading(true);
    try {
      await updateAssistanceItem(decisionId, item.id, item.version, payload);
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
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
      <label htmlFor="edit-item-type">סוג סיוע <span className="field-required">*</span></label>
      <select id="edit-item-type" value={assistanceTypeId} onChange={(e) => setAssistanceTypeId(e.target.value)} disabled={loading}>
        {types.filter((t) => t.status === 'active').map((t) => (
          <option key={t.id} value={t.id}>{t.name}</option>
        ))}
      </select>
      <label htmlFor="edit-item-description">תיאור</label>
      <input id="edit-item-description" type="text" value={description} onChange={(e) => setDescription(e.target.value)} disabled={loading} />
      <label htmlFor="edit-item-target">יעד תשלום <span className="field-required">*</span></label>
      <select id="edit-item-target" value={paymentTarget} onChange={(e) => setPaymentTarget(e.target.value as PaymentTarget)} disabled={loading}>
        {PAYMENT_TARGETS.map((t) => (
          <option key={t} value={t}>{translatePaymentTarget(t)}</option>
        ))}
      </select>
      <label htmlFor="edit-item-method">אופן תשלום <span className="field-required">*</span></label>
      <select id="edit-item-method" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value as PaymentMethod)} disabled={loading}>
        {PAYMENT_METHODS.map((m) => (
          <option key={m} value={m}>{translatePaymentMethod(m)}</option>
        ))}
      </select>
      {paymentTarget === 'supplier' && (
        <>
          <label htmlFor="edit-item-supplier">ספק <span className="field-required">*</span></label>
          <select id="edit-item-supplier" value={supplierId} onChange={(e) => setSupplierId(e.target.value)} disabled={loading}>
            <option value="">— בחר ספק —</option>
            {suppliers.filter((s) => s.status === 'active').map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </>
      )}
      {paymentTarget === 'other' && (
        <>
          <label htmlFor="edit-item-payee">שם מוטב <span className="field-required">*</span></label>
          <input id="edit-item-payee" type="text" value={payeeName} onChange={(e) => setPayeeName(e.target.value)} disabled={loading} />
        </>
      )}
      {paymentMethod === 'vouchers' && (
        <>
          <label htmlFor="edit-item-voucher">סוג שובר</label>
          <input id="edit-item-voucher" type="text" value={voucherType} onChange={(e) => setVoucherType(e.target.value)} disabled={loading} />
        </>
      )}
      <label htmlFor="edit-item-amount">סכום <span className="field-required">*</span></label>
      <input id="edit-item-amount" type="number" value={amount} onChange={(e) => setAmount(e.target.value)} disabled={loading} min={0} step={0.01} />
      <label className="checkbox-label">
        <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} disabled={loading} />
        דחוף
      </label>
    </ModalShell>
  );
}

function DecisionDetailPanel({
  decision: initial,
  user,
  types,
  suppliers,
  onClose,
  onUpdated,
}: {
  decision: CommitteeDecisionDto;
  user: UserDto;
  types: AssistanceTypeDto[];
  suppliers: SupplierDto[];
  onClose: () => void;
  onUpdated: () => void;
}) {
  const [decision, setDecision] = useState(initial);
  const [meetingDate, setMeetingDate] = useState(initial.meetingDate);
  const [summary, setSummary] = useState(initial.summary ?? '');
  const [editItem, setEditItem] = useState<AssistanceItemDto | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

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

  const totalColSpan = showActions ? 2 : 1;

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
        {canAddItems && (
          <ItemFormRow types={types} suppliers={suppliers} onAdd={handleAddItem} disabled={loading} />
        )}

        <div className="table-wrap">
          <table className="org-table items-grid">
            <thead>
              <tr>
                <th>#</th>
                <th>סוג סיוע</th>
                <th>תיאור</th>
                <th>יעד תשלום</th>
                <th>אופן תשלום</th>
                <th>מוטב / העברה</th>
                <th>סכום</th>
                <th>דחוף</th>
                {showActions && <th>פעולות</th>}
              </tr>
            </thead>
            <tbody>
              {decision.items.length === 0 && (
                <tr><td colSpan={showActions ? 9 : 8} className="empty-row">אין פריטים</td></tr>
              )}
              {decision.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.lineNumber}</td>
                  <td>{item.assistanceTypeName}</td>
                  <td>{item.description ?? '—'}</td>
                  <td>{translatePaymentTarget(item.paymentTarget)}</td>
                  <td>{translatePaymentMethod(item.paymentMethod)}</td>
                  <td>{formatPayeeTransfer(item)}</td>
                  <td>{item.amount.toLocaleString('he-IL')} ₪</td>
                  <td>{item.isUrgent ? 'כן' : '—'}</td>
                  {showActions && (
                    <td className="item-actions-cell">
                      {canEditItems && (
                        <button type="button" className="btn-small" onClick={() => setEditItem(item)} disabled={loading}>ערוך</button>
                      )}
                      {canRemoveItems && (
                        <button type="button" className="btn-small btn-danger" onClick={() => handleRemoveItem(item)} disabled={loading}>הסר</button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={6}><strong>סה״כ</strong></td>
                <td><strong>{decision.totalAmount.toLocaleString('he-IL')} ₪</strong></td>
                <td colSpan={totalColSpan} />
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
          onClose={() => setDetailTarget(null)}
          onUpdated={() => load(activeFilter)}
        />
      )}
    </div>
  );
}
