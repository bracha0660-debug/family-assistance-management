import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import type { UserDto } from '../api/auth';
import {
  AMOUNT_ADJUSTMENT_REASONS,
  amountAdjustmentReasonLabel,
  cancelExportBatch,
  cancelExportBatchItem,
  createExportBatch,
  downloadExportBatch,
  editPaymentRow,
  enterPaymentReference,
  ExportBatchCreateError,
  formatExportRowValidationMessage,
  listExportBatches,
  listPaymentRows,
  type ExportBatchDto,
  type ExportBatchRowValidationError,
  type PaymentRowDto,
  type PaymentRowListResponse,
} from '../api/exportBatches';
import type { HomeNavigationTarget } from '../api/workflow';
import { ModalShell, FormField } from '../components/ModalShell';
import { AssistanceItemDetailsModal } from '../components/AssistanceItemDetailsModal';
import { AssistanceItemHistoryModal } from '../components/AssistanceItemHistoryModal';
import { HistoryIconButton } from '../components/history/HistoryIconButton';
import { HistoryValueTransition } from '../components/history/HistoryValueTransition';
import { listAssistanceTypes, type AssistanceTypeDto } from '../api/assistanceTypes';
import { listSuppliers, type SupplierDto } from '../api/suppliers';
import { isPendingPaymentFilter, workflowFilterLabel } from './home/workflowStatus';
import {
  assistanceItemStatusLabel,
  workflowActionButtonClass,
  workflowActionLabel,
} from './home/workflowLabels';

interface PaymentsQueuePageProps {
  user: UserDto;
  initialFilter?: HomeNavigationTarget | null;
}

type ColumnId =
  | 'decisionCode'
  | 'familyAccountingCode'
  | 'assistanceTypeName'
  | 'amount'
  | 'payee'
  | 'paymentMethod'
  | 'exportBatchNumber'
  | 'executionReference'
  | 'status'
  | 'familyCode'
  | 'familyLastName'
  | 'assistanceTypeCode'
  | 'paymentTarget'
  | 'originalAmount';

const COLUMN_STORAGE_KEY = 'fam.payments.columns.v2';

const COLUMN_DEFS: { id: ColumnId; label: string; defaultVisible: boolean; locked?: boolean }[] = [
  { id: 'decisionCode', label: 'קוד החלטה', defaultVisible: true },
  { id: 'familyLastName', label: 'שם משפחה', defaultVisible: true },
  { id: 'familyAccountingCode', label: 'קוד משפחה בהנ"ח', defaultVisible: true },
  { id: 'assistanceTypeName', label: 'סוג סיוע', defaultVisible: true },
  { id: 'amount', label: 'סכום לתשלום', defaultVisible: true },
  { id: 'paymentTarget', label: 'יעד תשלום', defaultVisible: true },
  { id: 'payee', label: 'מוטב', defaultVisible: true },
  { id: 'paymentMethod', label: 'אמצעי תשלום / אופן תשלום', defaultVisible: true },
  { id: 'exportBatchNumber', label: 'מספר אצוות ייצוא', defaultVisible: true },
  { id: 'executionReference', label: 'אסמכתא', defaultVisible: true },
  { id: 'status', label: 'סטטוס', defaultVisible: true, locked: true },
  { id: 'familyCode', label: 'קוד משפחה', defaultVisible: false },
  { id: 'assistanceTypeCode', label: 'קוד סוג סיוע', defaultVisible: false },
  { id: 'originalAmount', label: 'סכום מאושר מקורי', defaultVisible: false },
];

function loadVisibleColumns(): Set<ColumnId> {
  try {
    const raw = localStorage.getItem(COLUMN_STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as ColumnId[];
      if (Array.isArray(parsed) && parsed.length > 0) {
        const set = new Set(parsed);
        set.add('status');
        return set;
      }
    }
  } catch {
    /* ignore */
  }
  return new Set(COLUMN_DEFS.filter((c) => c.defaultVisible).map((c) => c.id));
}

function saveVisibleColumns(cols: Set<ColumnId>) {
  localStorage.setItem(COLUMN_STORAGE_KEY, JSON.stringify([...cols]));
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

function translateBatchStatus(status: string): string {
  switch (status) {
    case 'open': return 'פתוח';
    case 'partially_cancelled': return 'בוטל חלקית';
    case 'cancelled': return 'בוטל';
    default: return status;
  }
}

function formatMoney(amount: number): string {
  return `${amount.toLocaleString('he-IL')} ₪`;
}

function renderAmountCell(row: PaymentRowDto) {
  if (row.hasAmountAdjustment && row.originalApprovedAmount != null) {
    return (
      <HistoryValueTransition
        previousValue={formatMoney(row.originalApprovedAmount)}
        newValue={formatMoney(row.amount)}
      />
    );
  }
  return formatMoney(row.amount);
}

function payeeLabel(row: PaymentRowDto): string {
  return row.supplierName ?? row.payeeName ?? '—';
}

type ModalState =
  | { type: 'confirm_export'; count: number }
  | { type: 'enter_reference'; row: PaymentRowDto }
  | { type: 'edit'; row: PaymentRowDto }
  | { type: 'view_history'; row: PaymentRowDto; anchor: HTMLElement }
  | { type: 'cancel_item'; row: PaymentRowDto }
  | { type: 'cancel_batch'; batch: ExportBatchDto }
  | { type: 'row_details'; row: PaymentRowDto }
  | null;

export function PaymentsQueuePage({ initialFilter }: PaymentsQueuePageProps) {
  const [data, setData] = useState<PaymentRowListResponse | null>(null);
  const [batches, setBatches] = useState<ExportBatchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [activeFilter, setActiveFilter] = useState<HomeNavigationTarget | null | undefined>(initialFilter);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [visibleColumns, setVisibleColumns] = useState<Set<ColumnId>>(loadVisibleColumns);
  const [gearOpen, setGearOpen] = useState(false);
  const [modal, setModal] = useState<ModalState>(null);
  const [modalError, setModalError] = useState('');
  const [exportRowErrors, setExportRowErrors] = useState<ExportBatchRowValidationError[]>([]);
  const [showExportErrorDetails, setShowExportErrorDetails] = useState(true);

  const load = useCallback(async (filter?: HomeNavigationTarget | null) => {
    setError('');
    try {
      const listOptions = filter?.targetTab === 'payments'
        ? { section: filter.section, minAgeDays: filter.minAgeDays, limit: 200 }
        : { limit: 200 };
      const [rows, batchList] = await Promise.all([
        listPaymentRows(listOptions),
        listExportBatches().catch(() => ({ batches: [] as ExportBatchDto[] })),
      ]);
      setData(rows);
      setBatches(batchList.batches);
      setSelectedIds((prev) => {
        const next = new Set<string>();
        for (const id of prev) {
          if (rows.items.some((r) => r.assistanceItemId === id && r.eligibleForExport)) {
            next.add(id);
          }
        }
        return next;
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    setActiveFilter(initialFilter);
    setLoading(true);
    void load(initialFilter);
  }, [initialFilter, load]);

  const rows = useMemo(() => data?.items ?? [], [data]);
  const eligibleRows = useMemo(() => rows.filter((r) => r.eligibleForExport), [rows]);
  const selectedEligible = useMemo(
    () => rows.filter((r) => selectedIds.has(r.assistanceItemId) && r.eligibleForExport),
    [rows, selectedIds],
  );

  const visibleDefs = COLUMN_DEFS.filter((c) => visibleColumns.has(c.id));

  function toggleColumn(id: ColumnId) {
    const def = COLUMN_DEFS.find((c) => c.id === id);
    if (def?.locked) return;
    setVisibleColumns((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      next.add('status');
      saveVisibleColumns(next);
      return next;
    });
  }

  function selectAllEligible() {
    setSelectedIds(new Set(eligibleRows.map((r) => r.assistanceItemId)));
  }

  function clearSelection() {
    setSelectedIds(new Set());
  }

  function toggleRow(row: PaymentRowDto) {
    if (!row.eligibleForExport) return;
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(row.assistanceItemId)) next.delete(row.assistanceItemId);
      else next.add(row.assistanceItemId);
      return next;
    });
  }

  async function runAction(key: string, fn: () => Promise<void>) {
    setActionLoading(key);
    setError('');
    setModalError('');
    try {
      await fn();
      setExportRowErrors([]);
      setModal(null);
      await load(activeFilter);
    } catch (err) {
      if (err instanceof ExportBatchCreateError) {
        setModalError(err.message);
        setExportRowErrors(err.rowErrors);
        setShowExportErrorDetails(true);
      } else {
        const message = err instanceof Error ? err.message : 'שגיאת מערכת';
        if (modal) setModalError(message);
        else setError(message);
        setExportRowErrors([]);
      }
    } finally {
      setActionLoading(null);
    }
  }

  function handleRowAction(row: PaymentRowDto, action: string) {
    if (action === 'enter_reference') {
      setModalError('');
      setModal({ type: 'enter_reference', row });
      return;
    }
    if (action === 'edit' || action === 'adjust_amount') {
      setModalError('');
      setModal({ type: 'edit', row });
      return;
    }
    if (action === 'view_history') {
      return;
    }
    if (action === 'cancel_export_item') {
      setModalError('');
      setModal({ type: 'cancel_item', row });
    }
  }

  function handleBatchAction(batch: ExportBatchDto, action: string) {
    if (action === 'download') {
      void runAction(`batch-${batch.id}`, async () => {
        await downloadExportBatch(batch.id, batch.fileName ?? undefined);
      });
      return;
    }
    if (action === 'cancel_batch') {
      setModalError('');
      setModal({ type: 'cancel_batch', batch });
    }
  }

  async function confirmCreateBatch() {
    const items = selectedEligible.map((r) => ({
      assistanceItemId: r.assistanceItemId,
      version: r.version,
    }));
    await runAction('create-batch', async () => {
      const batch = await createExportBatch(items);
      setSelectedIds(new Set());
      if (batch.availableActions.includes('download')) {
        await downloadExportBatch(batch.id, batch.fileName ?? undefined);
      }
    });
  }

  const filterLabel = workflowFilterLabel(activeFilter);
  const busy = actionLoading !== null;

  function renderCell(row: PaymentRowDto, col: ColumnId) {
    switch (col) {
      case 'decisionCode':
        return <code>{row.decisionCode}</code>;
      case 'familyAccountingCode':
        return row.familyAccountingCode;
      case 'assistanceTypeName':
        return row.assistanceTypeName;
      case 'amount':
        return (
          <span className="amount-tracking">
            <span className="amount-tracking__primary">{renderAmountCell(row)}</span>
            {row.hasAmountAdjustment && (
              <span className="amount-tracking__hint">
                סיבה: {amountAdjustmentReasonLabel(row.amountAdjustmentReason)}
                {row.amountAdjustmentReason === 'other' && row.amountAdjustmentExplanation
                  ? ` — ${row.amountAdjustmentExplanation}`
                  : ''}
              </span>
            )}
          </span>
        );
      case 'payee':
        return payeeLabel(row);
      case 'paymentMethod':
        return translatePaymentMethod(row.paymentMethod);
      case 'exportBatchNumber':
        return row.activeExportBatchNumber ?? '—';
      case 'executionReference':
        return row.executionReference ?? '—';
      case 'status':
        return (
          <span className={`status-badge status-${row.status}`}>
            {assistanceItemStatusLabel(row.status)}
          </span>
        );
      case 'familyCode':
        return row.familyCode;
      case 'familyLastName':
        return row.familyLastName;
      case 'assistanceTypeCode':
        return row.assistanceTypeCode || '—';
      case 'paymentTarget':
        return translatePaymentTarget(row.paymentTarget);
      case 'originalAmount':
        return row.originalApprovedAmount != null
          ? formatMoney(row.originalApprovedAmount)
          : '—';
      default:
        return '—';
    }
  }

  return (
    <div className="payments-queue-page queue-page">
      {filterLabel && (
        <div className="toolbar">
          <span className={`filter-chip${isPendingPaymentFilter(activeFilter) ? ' filter-chip-pending-payment' : ''}`}>
            סינון: {filterLabel}
          </span>
          <button
            type="button"
            className="btn-secondary"
            onClick={() => {
              setActiveFilter(null);
              setLoading(true);
              void load(null);
            }}
          >
            נקה סינון
          </button>
        </div>
      )}

      {data && (
        <div className="summary-cards summary-cards--page-top">
          <div className="summary-card summary-total">
            <span className="summary-label">סה״כ</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card summary-approved">
            <span className="summary-label">אושר</span>
            <span className="summary-value">{data.summary.approved}</span>
          </div>
          <div className="summary-card summary-waiting">
            <span className="summary-label">בביצוע</span>
            <span className="summary-value">{data.summary.waitingForReference}</span>
          </div>
          <div className="summary-card summary-paid">
            <span className="summary-label">שולם</span>
            <span className="summary-value">{data.summary.paid}</span>
          </div>
        </div>
      )}

      <div className="toolbar payments-toolbar">
        <button type="button" className="btn-secondary" onClick={() => void load(activeFilter)} disabled={busy}>
          רענן
        </button>
        <button
          type="button"
          className="btn-secondary"
          onClick={selectAllEligible}
          disabled={busy || eligibleRows.length === 0}
        >
          סמן הכל
        </button>
        <button
          type="button"
          className="btn-secondary"
          onClick={clearSelection}
          disabled={busy || selectedIds.size === 0}
        >
          נקה בחירה
        </button>
        <button
          type="button"
          disabled={busy || selectedEligible.length === 0}
          onClick={() => {
            setModalError('');
            setExportRowErrors([]);
            setShowExportErrorDetails(true);
            setModal({ type: 'confirm_export', count: selectedEligible.length });
          }}
        >
          העבר לביצוע ({selectedEligible.length})
        </button>
        <div className="payments-column-gear">
          <button
            type="button"
            className="btn-secondary"
            aria-expanded={gearOpen}
            aria-label="הגדרות עמודות"
            onClick={() => setGearOpen((o) => !o)}
          >
            ⚙ עמודות
          </button>
          {gearOpen && (
            <div className="payments-column-panel" role="menu">
              <p className="hint-text">תצוגה בלבד — לא משפיע על ייצוא</p>
              {COLUMN_DEFS.map((col) => (
                <label key={col.id} className="payments-column-option">
                  <input
                    type="checkbox"
                    checked={visibleColumns.has(col.id)}
                    disabled={col.locked}
                    onChange={() => toggleColumn(col.id)}
                  />
                  {col.label}
                </label>
              ))}
            </div>
          )}
        </div>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען תור תשלומים...</p>
      ) : (
        <section className="payments-queue-section payments-queue-section--rows queue-pane--primary" aria-label="פרטי סיוע לתשלום">
          <div className="table-wrap table-wrap--scroll table-wrap--primary">
            <table className="org-table org-table--compact">
              <thead>
                <tr>
                  <th className="payments-check-col" aria-label="בחירה" />
                  {visibleDefs.map((col) => (
                    <th key={col.id} className={col.id === 'status' ? 'col-status' : undefined}>{col.label}</th>
                  ))}
                  <th>פעולות</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={visibleDefs.length + 2} className="empty-row">
                      אין פריטי תשלום בתור
                    </td>
                  </tr>
                )}
                {rows.map((row) => {
                  const rowBusy = actionLoading === row.assistanceItemId;
                  return (
                    <tr key={row.assistanceItemId}>
                      <td className="payments-check-col">
                        <input
                          type="checkbox"
                          checked={selectedIds.has(row.assistanceItemId)}
                          disabled={!row.eligibleForExport || busy}
                          onChange={() => toggleRow(row)}
                          aria-label={`בחירת ${row.decisionCode}`}
                        />
                      </td>
                      {visibleDefs.map((col) => (
                        <td key={col.id} className={col.id === 'status' ? 'col-status' : col.id === 'amount' ? 'col-amount' : undefined}>{renderCell(row, col.id)}</td>
                      ))}
                      <td className="actions-cell actions-cell--with-history">
                        <div className="actions-cell__business">
                          <button
                            type="button"
                            className="btn-small btn-action-neutral"
                            disabled={busy}
                            onClick={() => setModal({ type: 'row_details', row })}
                          >
                            פרטים
                          </button>
                          {row.availableActions
                            .filter((action) => action !== 'view_history')
                            .map((action) => (
                            <button
                              key={action}
                              type="button"
                              className={workflowActionButtonClass(action)}
                              disabled={rowBusy || busy}
                              onClick={() => handleRowAction(row, action)}
                            >
                              {workflowActionLabel(action)}
                            </button>
                          ))}
                          {row.activeExportBatchId
                            && row.availableActions.length === 0
                            && batches.find((b) => b.id === row.activeExportBatchId)?.availableActions.includes('download') && (
                            <button
                              type="button"
                              className="btn-small btn-secondary"
                              disabled={busy}
                              onClick={() => {
                                const batch = batches.find((b) => b.id === row.activeExportBatchId);
                                if (batch) handleBatchAction(batch, 'download');
                              }}
                            >
                              {workflowActionLabel('download')}
                            </button>
                          )}
                        </div>
                        {row.availableActions.includes('view_history') && (
                          <HistoryIconButton
                            disabled={rowBusy || busy}
                            onClick={(anchor) => {
                              setModalError('');
                              setModal({ type: 'view_history', row, anchor });
                            }}
                          />
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {batches.length > 0 && (
        <section className="payments-batches-section queue-pane--secondary" aria-label="אצוות ייצוא">
          <h3>אצוות ייצוא</h3>
          <div className="table-wrap table-wrap--scroll table-wrap--secondary">
            <table className="org-table org-table--compact">
              <thead>
                <tr>
                  <th>מספר אצווה</th>
                  <th>סטטוס</th>
                  <th>פריטים פעילים</th>
                  <th>נוצר</th>
                  <th>פעולות</th>
                </tr>
              </thead>
              <tbody>
                {batches.map((batch) => (
                  <tr key={batch.id}>
                    <td><code>{batch.batchNumber}</code></td>
                    <td>{translateBatchStatus(batch.status)}</td>
                    <td>{batch.activeItemCount} / {batch.totalItemCount}</td>
                    <td>{new Date(batch.createdAt).toLocaleString('he-IL')}</td>
                    <td className="actions-cell">
                      {batch.availableActions.map((action) => (
                        <button
                          key={action}
                          type="button"
                          className={workflowActionButtonClass(action)}
                          disabled={busy}
                          onClick={() => handleBatchAction(batch, action)}
                        >
                          {workflowActionLabel(action)}
                        </button>
                      ))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {modal?.type === 'confirm_export' && (
        <ModalShell
          title="העברה לביצוע"
          onClose={() => !busy && setModal(null)}
          loading={busy}
          formError={modalError}
          footer={(
            <>
              <button type="button" className="btn-secondary" disabled={busy} onClick={() => setModal(null)}>
                ביטול
              </button>
              <button type="button" disabled={busy} onClick={() => void confirmCreateBatch()}>
                אישור
              </button>
            </>
          )}
        >
          <p>
            {`נבחרו ${modal.count} פריטים להעברה לביצוע. האם ליצור גליון ייצוא?`}
          </p>
          {exportRowErrors.length > 0 && (
            <div className="payments-export-errors" role="alert">
              <div className="payments-export-errors__toolbar">
                <strong>פירוט שגיאות לפי פריט</strong>
                <button
                  type="button"
                  className="btn-small btn-secondary"
                  onClick={() => setShowExportErrorDetails((v) => !v)}
                >
                  {showExportErrorDetails ? 'הסתר פירוט שגיאות' : 'הצג פירוט שגיאות'}
                </button>
              </div>
              {showExportErrorDetails && (
                <ul className="payments-export-errors__list">
                  {exportRowErrors.map((row) => (
                    <li key={`${row.assistanceItemId}-${row.message}`}>
                      {formatExportRowValidationMessage(row)}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </ModalShell>
      )}

      {modal?.type === 'enter_reference' && (
        <ReferenceModal
          busy={busy}
          error={modalError}
          onClose={() => !busy && setModal(null)}
          onSubmit={(reference) => {
            void runAction(modal.row.assistanceItemId, async () => {
              await enterPaymentReference(modal.row.assistanceItemId, reference);
            });
          }}
        />
      )}

      {modal?.type === 'edit' && (
        <EditPaymentModal
          row={modal.row}
          busy={busy}
          error={modalError}
          onClose={() => !busy && setModal(null)}
          onSubmit={(fields, amountReason, amountExplanation) => {
            void runAction(modal.row.assistanceItemId, async () => {
              await editPaymentRow(
                modal.row.assistanceItemId,
                modal.row.version,
                fields,
                amountReason,
                amountExplanation,
              );
            });
          }}
        />
      )}

      {modal?.type === 'view_history' && (
        <AssistanceItemHistoryModal
          assistanceItemId={modal.row.assistanceItemId}
          anchorEl={modal.anchor}
          onClose={() => setModal(null)}
        />
      )}

      {modal?.type === 'cancel_item' && (
        <ReasonModal
          title={workflowActionLabel('cancel_export_item')}
          busy={busy}
          error={modalError}
          onClose={() => !busy && setModal(null)}
          onSubmit={(reason) => {
            const batchId = modal.row.activeExportBatchId;
            const itemId = modal.row.activeExportBatchItemId;
            if (!batchId || !itemId) {
              setModalError('לא נמצא פריט ייצוא פעיל');
              return;
            }
            void runAction(modal.row.assistanceItemId, async () => {
              await cancelExportBatchItem(batchId, itemId, reason);
            });
          }}
        />
      )}

      {modal?.type === 'cancel_batch' && (
        <ReasonModal
          title={workflowActionLabel('cancel_batch')}
          busy={busy}
          error={modalError}
          onClose={() => !busy && setModal(null)}
          onSubmit={(reason) => {
            void runAction(`batch-${modal.batch.id}`, async () => {
              await cancelExportBatch(modal.batch.id, reason);
            });
          }}
        />
      )}

      {modal?.type === 'row_details' && (
        <AssistanceItemDetailsModal
          item={{
            decisionCode: modal.row.decisionCode,
            familyCode: modal.row.familyCode,
            familyAccountingCode: modal.row.familyAccountingCode,
            familyLastName: modal.row.familyLastName,
            assistanceTypeName: modal.row.assistanceTypeName,
            assistanceTypeCode: modal.row.assistanceTypeCode,
            description: modal.row.description,
            amount: modal.row.amount,
            originalApprovedAmount: modal.row.originalApprovedAmount,
            previousPaymentAmount: modal.row.previousPaymentAmount,
            amountAdjustmentReason: modal.row.amountAdjustmentReason,
            amountAdjustmentExplanation: modal.row.amountAdjustmentExplanation,
            hasAmountAdjustment: modal.row.hasAmountAdjustment,
            paymentTarget: modal.row.paymentTarget,
            paymentMethod: modal.row.paymentMethod,
            payeeName: modal.row.payeeName,
            supplierName: modal.row.supplierName,
            supplierAccountingCode: modal.row.supplierAccountingCode,
            transferBankNumber: modal.row.transferBankNumber,
            transferBranchNumber: modal.row.transferBranchNumber,
            transferAccountNumber: modal.row.transferAccountNumber,
            accountHolderName: modal.row.accountHolderName,
            voucherType: modal.row.voucherType,
            isUrgent: modal.row.isUrgent,
            activeExportBatchNumber: modal.row.activeExportBatchNumber,
            executionReference: modal.row.executionReference,
            status: modal.row.status,
            createdAt: modal.row.createdAt,
            updatedAt: modal.row.updatedAt,
            version: modal.row.version,
          }}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}

function ReferenceModal({
  busy,
  error,
  onClose,
  onSubmit,
}: {
  busy: boolean;
  error: string;
  onClose: () => void;
  onSubmit: (reference: string) => void;
}) {
  const [reference, setReference] = useState('');
  return (
    <ModalShell
      title={workflowActionLabel('enter_reference')}
      hint="הזנת אסמכתא טקסטואלית בלבד — ללא העלאת קובץ"
      onClose={onClose}
      loading={busy}
      formError={error}
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        const value = reference.trim();
        if (value.length < 1) {
          return;
        }
        onSubmit(value);
      }}
      footer={(
        <>
          <button type="button" className="btn-secondary" disabled={busy} onClick={onClose}>ביטול</button>
          <button type="submit" disabled={busy || reference.trim().length < 1}>אישור</button>
        </>
      )}
    >
      <FormField id="payment-reference" label="אסמכתא">
        <input
          id="payment-reference"
          type="text"
          value={reference}
          onChange={(e) => setReference(e.target.value)}
          required
          autoFocus
        />
      </FormField>
    </ModalShell>
  );
}

function EditPaymentModal({
  row,
  busy,
  error,
  onClose,
  onSubmit,
}: {
  row: PaymentRowDto;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSubmit: (
    fields: Record<string, string | null>,
    amountReason: string | null,
    amountExplanation: string | null,
  ) => void;
}) {
  const [types, setTypes] = useState<AssistanceTypeDto[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [assistanceTypeId, setAssistanceTypeId] = useState(row.assistanceTypeId);
  const [description, setDescription] = useState(row.description ?? '');
  const [amount, setAmount] = useState(String(row.amount));
  const [paymentTarget, setPaymentTarget] = useState(row.paymentTarget);
  const [paymentMethod, setPaymentMethod] = useState(row.paymentMethod);
  const [supplierId, setSupplierId] = useState(row.supplierId ?? '');
  const [beneficiary, setBeneficiary] = useState(row.payeeName ?? '');
  const [bankNumber, setBankNumber] = useState(row.transferBankNumber ?? '');
  const [branchNumber, setBranchNumber] = useState(row.transferBranchNumber ?? '');
  const [accountNumber, setAccountNumber] = useState(row.transferAccountNumber ?? '');
  const [accountHolderName, setAccountHolderName] = useState(row.accountHolderName ?? '');
  const [reason, setReason] = useState('typing_error');
  const [explanation, setExplanation] = useState('');
  const [explanationError, setExplanationError] = useState<string | null>(null);
  const amountChanged = Number(amount) !== Number(row.amount);
  const needsExplanation = amountChanged && reason === 'other';

  useEffect(() => {
    void (async () => {
      try {
        const [typeRes, supplierRes] = await Promise.all([listAssistanceTypes(), listSuppliers()]);
        setTypes(typeRes.assistanceTypes.filter((t) => t.status === 'active'));
        setSuppliers(supplierRes.suppliers.filter((s) => s.status === 'active'));
      } catch {
        /* dropdowns may fail without view permission; text fields still work */
      }
    })();
  }, []);

  return (
    <ModalShell
      title={workflowActionLabel('edit')}
      onClose={onClose}
      loading={busy}
      formError={error}
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        const parsed = Number(amount);
        if (!(parsed > 0)) return;
        if (needsExplanation) {
          const trimmed = explanation.trim();
          if (trimmed.length < 3) {
            setExplanationError('יש למלא הסבר לשינוי הסכום');
            document.getElementById('edit-explanation')?.focus();
            return;
          }
          setExplanationError(null);
        }
        const fields: Record<string, string | null> = {
          assistance_type_id: assistanceTypeId,
          description: description.trim() || null,
          amount: String(parsed),
          payment_target: paymentTarget,
          payment_method: paymentMethod,
          supplier_id: supplierId || null,
          beneficiary: beneficiary.trim() || null,
          bank_number: bankNumber.trim() || null,
          branch_number: branchNumber.trim() || null,
          account_number: accountNumber.trim() || null,
          account_holder_name: accountHolderName.trim() || null,
        };
        onSubmit(
          fields,
          amountChanged ? reason : null,
          amountChanged && needsExplanation ? explanation.trim() : null,
        );
      }}
      footer={(
        <>
          <button type="button" className="btn-secondary" disabled={busy} onClick={onClose}>ביטול</button>
          <button type="submit" disabled={busy}>שמירה</button>
        </>
      )}
    >
      <p className="hint-text">
        סכום לתשלום: {formatMoney(row.amount)}
        {row.originalApprovedAmount != null && ` · מקורי: ${formatMoney(row.originalApprovedAmount)}`}
      </p>
      <FormField id="edit-type" label="סוג סיוע">
        <select id="edit-type" value={assistanceTypeId} onChange={(e) => setAssistanceTypeId(e.target.value)}>
          {types.length === 0 && <option value={row.assistanceTypeId}>{row.assistanceTypeName}</option>}
          {types.map((t) => (
            <option key={t.id} value={t.id}>{t.name}</option>
          ))}
        </select>
      </FormField>
      <FormField id="edit-description" label="תיאור">
        <textarea id="edit-description" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
      </FormField>
      <FormField id="edit-amount" label="סכום לתשלום">
        <input id="edit-amount" type="number" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
      </FormField>
      {amountChanged && (
        <>
          <FormField id="edit-reason" label="סיבת שינוי סכום">
            <select
              id="edit-reason"
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
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
              id="edit-explanation"
              label={<>הסבר <span className="field-required">*</span></>}
              error={explanationError}
            >
              <textarea
                id="edit-explanation"
                rows={3}
                value={explanation}
                onChange={(e) => {
                  setExplanation(e.target.value);
                  if (explanationError) setExplanationError(null);
                }}
                required
                aria-invalid={explanationError ? true : undefined}
              />
            </FormField>
          )}
        </>
      )}
      <FormField id="edit-target" label="יעד תשלום">
        <select id="edit-target" value={paymentTarget} onChange={(e) => setPaymentTarget(e.target.value)}>
          <option value="family">משפחה</option>
          <option value="supplier">ספק</option>
          <option value="other">אחר</option>
        </select>
      </FormField>
      <FormField id="edit-method" label="אמצעי תשלום">
        <select id="edit-method" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
          <option value="bank_transfer">העברה בנקאית</option>
          <option value="check">צ׳ק</option>
          <option value="vouchers">תווים</option>
        </select>
      </FormField>
      {paymentTarget === 'supplier' && (
        <FormField id="edit-supplier" label="ספק">
          <select id="edit-supplier" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">—</option>
            {suppliers.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </FormField>
      )}
      <FormField id="edit-beneficiary" label="מוטב">
        <input id="edit-beneficiary" value={beneficiary} onChange={(e) => setBeneficiary(e.target.value)} />
      </FormField>
      <FormField id="edit-bank" label="מספר בנק">
        <input id="edit-bank" value={bankNumber} onChange={(e) => setBankNumber(e.target.value)} />
      </FormField>
      <FormField id="edit-branch" label="מספר סניף">
        <input id="edit-branch" value={branchNumber} onChange={(e) => setBranchNumber(e.target.value)} />
      </FormField>
      <FormField id="edit-account" label="מספר חשבון">
        <input id="edit-account" value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} />
      </FormField>
      <FormField id="edit-holder" label="שם בעל החשבון">
        <input id="edit-holder" value={accountHolderName} onChange={(e) => setAccountHolderName(e.target.value)} />
      </FormField>
    </ModalShell>
  );
}

function ReasonModal({
  title,
  busy,
  error,
  onClose,
  onSubmit,
}: {
  title: string;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSubmit: (reason: string) => void;
}) {
  const [reason, setReason] = useState('');
  return (
    <ModalShell
      title={title}
      onClose={onClose}
      loading={busy}
      formError={error}
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        const value = reason.trim();
        if (value.length < 3) return;
        onSubmit(value);
      }}
      footer={(
        <>
          <button type="button" className="btn-secondary" disabled={busy} onClick={onClose}>ביטול</button>
          <button type="submit" disabled={busy || reason.trim().length < 3}>אישור</button>
        </>
      )}
    >
      <FormField id="cancel-reason" label="סיבה">
        <input
          id="cancel-reason"
          type="text"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          required
          minLength={3}
          autoFocus
        />
      </FormField>
    </ModalShell>
  );
}
