import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react';
import type { UserDto } from '../api/auth';
import {
  executePayment,
  listPayments,
  markPaymentPaid,
  returnPaymentToCoordinator,
  uploadPaymentProof,
  type PaymentQueueItemDto,
  type PaymentQueueListResponse,
} from '../api/payments';
import { PERMISSION_KEYS } from '../api/permissions';
import { hasPermission } from '../hooks/usePermissions';

interface PaymentsQueuePageProps {
  user: UserDto;
}

function translatePaymentStatus(status: string): string {
  switch (status) {
    case 'awaiting_payment': return 'ממתין לתשלום';
    case 'executing': return 'בביצוע';
    case 'proof_uploaded': return 'הוכחה הועלתה';
    case 'paid': return 'שולם';
    case 'returned_to_coordinator': return 'הוחזר לרכז';
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
    case 'check': return 'צ׳ק';
    case 'vouchers': return 'שוברים';
    default: return m;
  }
}

export function PaymentsQueuePage({ user }: PaymentsQueuePageProps) {
  const [data, setData] = useState<PaymentQueueListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploadTarget, setUploadTarget] = useState<PaymentQueueItemDto | null>(null);

  const canExecute = hasPermission(user, PERMISSION_KEYS.paymentsExecute);
  const canUploadProof = hasPermission(user, PERMISSION_KEYS.paymentsUploadProof);
  const canMarkPaid = hasPermission(user, PERMISSION_KEYS.paymentsMarkPaid);
  const canReturn = hasPermission(user, PERMISSION_KEYS.paymentsReturnToCoordinator);

  const load = useCallback(async () => {
    setError('');
    try {
      setData(await listPayments());
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

  async function runAction(id: string, fn: () => Promise<void>) {
    setActionLoading(id);
    setError('');
    try {
      await fn();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setActionLoading(null);
    }
  }

  function handleUploadClick(payment: PaymentQueueItemDto) {
    setUploadTarget(payment);
    fileInputRef.current?.click();
  }

  async function handleFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file || !uploadTarget) return;
    await runAction(uploadTarget.id, async () => {
      await uploadPaymentProof(uploadTarget.id, uploadTarget.version, file);
    });
    setUploadTarget(null);
  }

  return (
    <div>
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card">
            <span className="summary-label">ממתינים</span>
            <span className="summary-value">{data.summary.awaitingPayment}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">בביצוע</span>
            <span className="summary-value">{data.summary.executing}</span>
          </div>
          <div className="summary-card">
            <span className="summary-label">הוכחה הועלתה</span>
            <span className="summary-value">{data.summary.proofUploaded}</span>
          </div>
        </div>
      )}

      <div className="toolbar">
        <button type="button" className="btn-secondary" onClick={load}>רענן</button>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept=".pdf,.jpg,.jpeg,.png"
        style={{ display: 'none' }}
        onChange={handleFileSelected}
      />

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען תור תשלומים...</p>
      ) : (
        <div className="table-wrap">
          <table className="org-table">
            <thead>
              <tr>
                <th>החלטה</th>
                <th>משפחה</th>
                <th>סוג סיוע</th>
                <th>סכום</th>
                <th>יעד</th>
                <th>אמצעי</th>
                <th>מוטב</th>
                <th>סטטוס</th>
                <th>פעולות</th>
              </tr>
            </thead>
            <tbody>
              {(data?.payments ?? []).length === 0 && (
                <tr><td colSpan={9} className="empty-row">אין תשלומים בתור</td></tr>
              )}
              {(data?.payments ?? []).map((p) => {
                const busy = actionLoading === p.id;
                return (
                  <tr key={p.id}>
                    <td><code>{p.decisionCode}</code> #{p.lineNumber}</td>
                    <td>{p.familyCode} — {p.familyLastName}</td>
                    <td>{p.assistanceTypeName}</td>
                    <td>{p.amount.toLocaleString('he-IL')} ₪</td>
                    <td>{translatePaymentTarget(p.paymentTarget)}</td>
                    <td>{translatePaymentMethod(p.paymentMethod)}</td>
                    <td>{p.supplierName ?? p.payeeName ?? '—'}</td>
                    <td>
                      <span className={`status-badge status-${p.status}`}>
                        {translatePaymentStatus(p.status)}
                      </span>
                      {p.proofFileName && <div className="hint-text">{p.proofFileName}</div>}
                    </td>
                    <td className="actions-cell">
                      {p.status === 'awaiting_payment' && canExecute && (
                        <button
                          type="button"
                          className="btn-small"
                          disabled={busy}
                          onClick={() => runAction(p.id, async () => {
                            const ref = window.prompt('אסמכתא (אופציונלי):') ?? '';
                            await executePayment(p.id, p.version, ref.trim() || null);
                          })}
                        >
                          בצע
                        </button>
                      )}
                      {p.status === 'executing' && canUploadProof && (
                        <button
                          type="button"
                          className="btn-small"
                          disabled={busy}
                          onClick={() => handleUploadClick(p)}
                        >
                          העלה הוכחה
                        </button>
                      )}
                      {p.status === 'proof_uploaded' && canMarkPaid && (
                        <button
                          type="button"
                          className="btn-small"
                          disabled={busy}
                          onClick={() => runAction(p.id, async () => {
                            await markPaymentPaid(p.id, p.version, p.executionReference);
                          })}
                        >
                          סמן כשולם
                        </button>
                      )}
                      {['awaiting_payment', 'executing', 'proof_uploaded'].includes(p.status) && canReturn && (
                        <button
                          type="button"
                          className="btn-small btn-danger"
                          disabled={busy}
                          onClick={() => runAction(p.id, async () => {
                            const reason = window.prompt('סיבת החזרה לרכז:');
                            if (!reason || reason.trim().length < 3) throw new Error('יש לציין סיבה');
                            await returnPaymentToCoordinator(p.id, p.version, reason.trim());
                          })}
                        >
                          החזר
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
