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
  updateCommitteeDecision,
  PAYMENT_METHODS,
  PAYMENT_TARGETS,
  type AssistanceItemDto,
  type CommitteeDecisionDto,
  type CommitteeDecisionListResponse,
  type CreateAssistanceItemPayload,
  type PaymentMethod,
  type PaymentTarget,
} from '../api/committeeDecisions';
import { listFamilies, type FamilyDto } from '../api/families';
import { PERMISSION_KEYS } from '../api/permissions';
import { listSuppliers, type SupplierDto } from '../api/suppliers';
import { hasPermission } from '../hooks/usePermissions';
import { FieldValidationTooltip } from '../components/FieldValidation';
import { ModalShell } from '../components/ModalShell';
import { focusFirstInvalidField } from '../utils/formValidation';

interface CommitteeDecisionsPageProps {
  user: UserDto;
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
  const [isUrgent, setIsUrgent] = useState(false);
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
        isUrgent,
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
      <label className="checkbox-label">
        <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} disabled={loading} />
        דחוף
      </label>
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
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const ITEM_FOCUS_ORDER = [
    'item-assistance-type',
    'item-amount',
    'item-payment-target',
    'item-payment-method',
    'item-supplier',
    'item-payee',
  ];

  async function handleAdd() {
    setError('');
    const parsedAmount = Number(amount);
    if (!assistanceTypeId || !Number.isFinite(parsedAmount) || parsedAmount <= 0) {
      setError('יש לבחור סוג סיוע ולהזין סכום חיובי');
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    if (!paymentTarget) {
      setError('יש לבחור יעד תשלום');
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    if (!paymentMethod) {
      setError('יש לבחור אופן תשלום');
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    if (paymentTarget === 'supplier' && !supplierId) {
      setError('יש לבחור ספק');
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
    if (paymentTarget === 'other' && !payeeName.trim()) {
      setError('יש להזין שם מוטב');
      focusFirstInvalidField(ITEM_FOCUS_ORDER);
      return;
    }
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
      });
      setDescription('');
      setAmount('');
      setPaymentTarget('');
      setPaymentMethod('');
      setSupplierId('');
      setPayeeName('');
      setVoucherType('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="item-form-row">
      <select id="item-assistance-type" value={assistanceTypeId} onChange={(e) => setAssistanceTypeId(e.target.value)} disabled={disabled || loading}>
        <option value="">סוג סיוע</option>
        {types.filter((t) => t.status === 'active').map((t) => (
          <option key={t.id} value={t.id}>{t.name}</option>
        ))}
      </select>
      <input type="text" placeholder="תיאור" value={description} onChange={(e) => setDescription(e.target.value)} disabled={disabled || loading} />
      <input id="item-amount" type="number" placeholder="סכום" value={amount} onChange={(e) => setAmount(e.target.value)} disabled={disabled || loading} min={0} step={0.01} />
      <div className="item-form-field">
        <label htmlFor="item-payment-target">יעד תשלום</label>
        <select
          id="item-payment-target"
          value={paymentTarget}
          onChange={(e) => setPaymentTarget(e.target.value as PaymentTarget | '')}
          disabled={disabled || loading}
          aria-invalid={error.includes('יעד') ? true : undefined}
        >
          <option value="">בחר יעד תשלום</option>
          {PAYMENT_TARGETS.map((t) => (
            <option key={t} value={t}>{translatePaymentTarget(t)}</option>
          ))}
        </select>
      </div>
      <div className="item-form-field">
        <label htmlFor="item-payment-method">אופן התשלום</label>
        <select
          id="item-payment-method"
          value={paymentMethod}
          onChange={(e) => setPaymentMethod(e.target.value as PaymentMethod | '')}
          disabled={disabled || loading}
          aria-invalid={error.includes('אופן') ? true : undefined}
        >
          <option value="">בחר אופן תשלום</option>
          {PAYMENT_METHODS.map((m) => (
            <option key={m} value={m}>{translatePaymentMethod(m)}</option>
          ))}
        </select>
      </div>
      {paymentTarget === 'supplier' && (
        <select id="item-supplier" value={supplierId} onChange={(e) => setSupplierId(e.target.value)} disabled={disabled || loading}>
          <option value="">ספק</option>
          {suppliers.filter((s) => s.status === 'active').map((s) => (
            <option key={s.id} value={s.id}>{s.name}</option>
          ))}
        </select>
      )}
      {paymentTarget === 'other' && (
        <input id="item-payee" type="text" placeholder="שם מוטב" value={payeeName} onChange={(e) => setPayeeName(e.target.value)} disabled={disabled || loading} />
      )}
      {paymentMethod === 'vouchers' && (
        <input type="text" placeholder="סוג שובר" value={voucherType} onChange={(e) => setVoucherType(e.target.value)} disabled={disabled || loading} />
      )}
      <div className="item-form-actions validated-field-control">
        <button type="button" className="btn-small" onClick={handleAdd} disabled={disabled || loading}>הוסף שורה</button>
        {error && <FieldValidationTooltip id="item-form-error" message={error} />}
      </div>
    </div>
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
  const [isUrgent, setIsUrgent] = useState(initial.isUrgent);
  const [summary, setSummary] = useState(initial.summary ?? '');
  const [submitReason, setSubmitReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const editable = ['draft', 'returned_for_revision'].includes(decision.status);
  const canEditDraft = editable && hasPermission(user, PERMISSION_KEYS.committeeDecisionsEditDraft);
  const canAddItems = editable && hasPermission(user, PERMISSION_KEYS.assistanceItemsCreate);
  const canRemoveItems = editable && hasPermission(user, PERMISSION_KEYS.assistanceItemsRemoveDraft);
  const canSubmit = editable && hasPermission(user, PERMISSION_KEYS.committeeDecisionsSubmit);
  const canCancel = hasPermission(user, PERMISSION_KEYS.committeeDecisionsCancel);

  async function refresh() {
    const fresh = await getCommitteeDecision(decision.id);
    setDecision(fresh);
    onUpdated();
  }

  async function handleSaveHeader(e: FormEvent) {
    e.preventDefault();
    if (!canEditDraft) return;
    setLoading(true);
    setError('');
    try {
      const updated = await updateCommitteeDecision(decision.id, decision.version, {
        meetingDate,
        isUrgent,
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
    if (submitReason.trim().length < 3) {
      setError('יש לציין סיבה להגשה');
      return;
    }
    setLoading(true);
    try {
      const updated = await submitCommitteeDecision(decision.id, decision.version, submitReason.trim());
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
    <ModalShell
      title={`החלטה ${decision.decisionCode}`}
      wide
      loading={loading}
      onClose={onClose}
      formError={error}
      footer={(
        <button type="button" className="btn-secondary" onClick={onClose}>סגור</button>
      )}
    >
        <p>
          משפחה: <strong>{decision.familyCode}</strong> — {decision.familyLastName}
          {' · '}
          <span className={`status-badge status-${decision.status}`}>{translateDecisionStatus(decision.status)}</span>
        </p>

        {canEditDraft && (
          <form onSubmit={handleSaveHeader} className="decision-header-form">
            <label htmlFor="edit-meeting-date">תאריך ישיבה</label>
            <input id="edit-meeting-date" type="date" value={meetingDate} onChange={(e) => setMeetingDate(e.target.value)} disabled={loading} />
            <label className="checkbox-label">
              <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} disabled={loading} />
              דחוף
            </label>
            <label htmlFor="edit-summary">סיכום</label>
            <textarea id="edit-summary" value={summary} onChange={(e) => setSummary(e.target.value)} disabled={loading} rows={2} />
            <button type="submit" className="btn-small" disabled={loading}>שמור כותרת</button>
          </form>
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
                <th>סכום</th>
                <th>יעד</th>
                <th>אמצעי</th>
                <th>מוטב</th>
                {canRemoveItems && <th>פעולות</th>}
              </tr>
            </thead>
            <tbody>
              {decision.items.length === 0 && (
                <tr><td colSpan={canRemoveItems ? 8 : 7} className="empty-row">אין פריטים</td></tr>
              )}
              {decision.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.lineNumber}</td>
                  <td>{item.assistanceTypeName}</td>
                  <td>{item.description ?? '—'}</td>
                  <td>{item.amount.toLocaleString('he-IL')} ₪</td>
                  <td>{translatePaymentTarget(item.paymentTarget)}</td>
                  <td>{translatePaymentMethod(item.paymentMethod)}</td>
                  <td>{item.supplierName ?? item.payeeName ?? '—'}</td>
                  {canRemoveItems && (
                    <td>
                      <button type="button" className="btn-small btn-danger" onClick={() => handleRemoveItem(item)} disabled={loading}>הסר</button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={3}><strong>סה״כ</strong></td>
                <td colSpan={canRemoveItems ? 5 : 4}><strong>{decision.totalAmount.toLocaleString('he-IL')} ₪</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>

        {canSubmit && decision.items.length > 0 && (
          <div className="submit-section">
            <label htmlFor="submit-reason">סיבת הגשה</label>
            <input id="submit-reason" type="text" value={submitReason} onChange={(e) => setSubmitReason(e.target.value)} disabled={loading} />
            <button type="button" onClick={handleSubmitDecision} disabled={loading}>הגש לוועדה</button>
          </div>
        )}

        {canCancel && decision.status !== 'cancelled' && (
          <button type="button" className="btn-secondary btn-danger" onClick={handleCancel} disabled={loading}>בטל החלטה</button>
        )}
    </ModalShell>
  );
}

export function CommitteeDecisionsPage({ user }: CommitteeDecisionsPageProps) {
  const [data, setData] = useState<CommitteeDecisionListResponse | null>(null);
  const [families, setFamilies] = useState<FamilyDto[]>([]);
  const [types, setTypes] = useState<AssistanceTypeDto[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [detailTarget, setDetailTarget] = useState<CommitteeDecisionDto | null>(null);

  const load = useCallback(async () => {
    setError('');
    try {
      const [decisions, familiesRes, typesRes, suppliersRes] = await Promise.all([
        listCommitteeDecisions(),
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
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const canCreate = hasPermission(user, PERMISSION_KEYS.committeeDecisionsCreate);

  return (
    <div>
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
        <button type="button" className="btn-secondary" onClick={load}>רענן</button>
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
                <th>דחוף</th>
                <th>סכום</th>
                <th>סטטוס</th>
                <th>נוצר ע״י</th>
                <th>פעולות</th>
              </tr>
            </thead>
            <tbody>
              {(data?.decisions ?? []).length === 0 && (
                <tr><td colSpan={8} className="empty-row">אין החלטות להצגה</td></tr>
              )}
              {(data?.decisions ?? []).map((d) => (
                <tr key={d.id}>
                  <td><code>{d.decisionCode}</code></td>
                  <td>{d.familyCode} — {d.familyLastName}</td>
                  <td>{d.meetingDate}</td>
                  <td>{d.isUrgent ? 'כן' : '—'}</td>
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
          onCreated={(d) => { load(); setDetailTarget(d); }}
        />
      )}
      {detailTarget && (
        <DecisionDetailPanel
          decision={detailTarget}
          user={user}
          types={types}
          suppliers={suppliers}
          onClose={() => setDetailTarget(null)}
          onUpdated={load}
        />
      )}
    </div>
  );
}
