import { ModalShell } from './ModalShell';
import { HistoryValueTransition } from './history/HistoryValueTransition';
import { amountAdjustmentReasonLabel } from '../api/exportBatches';
import { findBankByNumber, formatBankOption } from '../data/israeliBanks';
import { assistanceItemStatusLabel } from '../pages/home/workflowLabels';
import { translatePaymentMethod, translatePaymentTarget } from './assistanceItem';

/** Shared detail fields for payments queue and committee decisions item views. */
export type AssistanceItemDetailsFields = {
  decisionCode: string;
  familyCode: string;
  familyAccountingCode: number | string | null | undefined;
  familyLastName: string;
  assistanceTypeName: string;
  assistanceTypeCode?: string | null;
  description?: string | null;
  amount: number;
  originalApprovedAmount?: number | null;
  previousPaymentAmount?: number | null;
  amountAdjustmentReason?: string | null;
  amountAdjustmentExplanation?: string | null;
  hasAmountAdjustment: boolean;
  paymentTarget: string;
  paymentMethod: string;
  payeeName?: string | null;
  supplierName?: string | null;
  supplierAccountingCode?: string | null;
  transferBankNumber?: string | null;
  transferBranchNumber?: string | null;
  transferAccountNumber?: string | null;
  accountHolderName?: string | null;
  voucherType?: string | null;
  isUrgent: boolean;
  activeExportBatchNumber?: string | null;
  executionReference?: string | null;
  status: string;
  createdAt: string;
  updatedAt?: string | null;
  version: number;
};

function formatMoney(amount: number): string {
  return `${amount.toLocaleString('he-IL')} ₪`;
}

function formatBankLabel(bankNumber: string | null | undefined): string {
  if (!bankNumber) return '—';
  const bank = findBankByNumber(bankNumber);
  return bank ? formatBankOption(bank) : bankNumber;
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString('he-IL');
}

function payeeLabel(item: AssistanceItemDetailsFields): string {
  return item.supplierName ?? item.payeeName ?? '—';
}

function renderAmountAdjustment(item: AssistanceItemDetailsFields) {
  if (item.hasAmountAdjustment && item.originalApprovedAmount != null) {
    return (
      <HistoryValueTransition
        previousValue={formatMoney(item.originalApprovedAmount)}
        newValue={formatMoney(item.amount)}
      />
    );
  }
  return formatMoney(item.amount);
}

export function AssistanceItemDetailsModal({
  item,
  titlePrefix = 'פרטי תשלום',
  onClose,
}: {
  item: AssistanceItemDetailsFields;
  titlePrefix?: string;
  onClose: () => void;
}) {
  const isBankTransfer = item.paymentMethod === 'bank_transfer';
  const isVouchers = item.paymentMethod === 'vouchers';
  const isSupplier = item.paymentTarget === 'supplier';
  const accountingCode =
    item.familyAccountingCode === null || item.familyAccountingCode === undefined || item.familyAccountingCode === ''
      ? '—'
      : String(item.familyAccountingCode);

  return (
    <ModalShell
      title={`${titlePrefix} — ${item.decisionCode}`}
      wide
      onClose={onClose}
      footer={(
        <button type="button" className="btn-secondary" onClick={onClose}>סגור</button>
      )}
    >
      <dl className="payments-detail-grid">
        <dt>קוד החלטה</dt><dd><code>{item.decisionCode}</code></dd>
        <dt>קוד משפחה</dt><dd>{item.familyCode}</dd>
        <dt>קוד משפחה בהנ&quot;ח</dt><dd>{accountingCode}</dd>
        <dt>שם משפחה</dt><dd>{item.familyLastName}</dd>
        <dt>סוג סיוע</dt><dd>{item.assistanceTypeName}</dd>
        <dt>קוד סוג סיוע</dt><dd>{item.assistanceTypeCode || '—'}</dd>
        <dt>תיאור</dt><dd>{item.description?.trim() || '—'}</dd>
        <dt>סכום נוכחי</dt><dd>{formatMoney(item.amount)}</dd>
        <dt>סכום מאושר מקורי</dt>
        <dd>{item.originalApprovedAmount != null ? formatMoney(item.originalApprovedAmount) : '—'}</dd>
        <dt>סכום קודם</dt>
        <dd>{item.previousPaymentAmount != null ? formatMoney(item.previousPaymentAmount) : '—'}</dd>
        <dt>התאמת סכום</dt>
        <dd>
          {item.hasAmountAdjustment ? (
            <>
              {renderAmountAdjustment(item)}
              {' · '}
              {amountAdjustmentReasonLabel(item.amountAdjustmentReason)}
              {item.amountAdjustmentReason === 'other' && item.amountAdjustmentExplanation
                ? ` — ${item.amountAdjustmentExplanation}`
                : ''}
            </>
          ) : (
            'אין'
          )}
        </dd>
        <dt>יעד תשלום</dt><dd>{translatePaymentTarget(item.paymentTarget)}</dd>
        <dt>אמצעי תשלום</dt><dd>{translatePaymentMethod(item.paymentMethod)}</dd>
        <dt>מוטב</dt><dd>{payeeLabel(item)}</dd>
        {isSupplier && (
          <>
            <dt>ספק</dt><dd>{item.supplierName ?? '—'}</dd>
            <dt>קוד ספק בהנ&quot;ח</dt><dd>{item.supplierAccountingCode ?? '—'}</dd>
          </>
        )}
        {isBankTransfer && (
          <>
            <dt>בנק</dt><dd>{formatBankLabel(item.transferBankNumber)}</dd>
            <dt>סניף</dt><dd>{item.transferBranchNumber ?? '—'}</dd>
            <dt>מספר חשבון</dt><dd>{item.transferAccountNumber ?? '—'}</dd>
            <dt>שם בעל החשבון</dt><dd>{item.accountHolderName ?? '—'}</dd>
          </>
        )}
        {isVouchers && (
          <><dt>סוג שובר</dt><dd>{item.voucherType?.trim() || '—'}</dd></>
        )}
        <dt>דחוף</dt><dd>{item.isUrgent ? 'כן' : 'לא'}</dd>
        <dt>מספר אצוות ייצוא</dt><dd>{item.activeExportBatchNumber ?? '—'}</dd>
        <dt>אסמכתא</dt><dd>{item.executionReference ?? '—'}</dd>
        <dt>סטטוס</dt><dd>{assistanceItemStatusLabel(item.status)}</dd>
        <dt>נוצר</dt><dd>{formatDateTime(item.createdAt)}</dd>
        <dt>עודכן</dt><dd>{formatDateTime(item.updatedAt)}</dd>
        <dt>גרסה</dt><dd>{item.version}</dd>
      </dl>
    </ModalShell>
  );
}
