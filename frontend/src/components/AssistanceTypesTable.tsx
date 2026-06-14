import type { AssistanceTypeDto } from '../api/assistanceTypes';
import { translateFrequency, translateStatus } from './roleLabel';

interface AssistanceTypesTableProps {
  types: AssistanceTypeDto[];
  canManage: boolean;
  onEdit?: (type: AssistanceTypeDto) => void;
  onDeactivate?: (type: AssistanceTypeDto) => void;
}

function formatAmount(amount: number | null, currency: string): string {
  if (amount === null) return '—';
  return `${amount.toLocaleString('he-IL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
}

export function AssistanceTypesTable({
  types,
  canManage,
  onEdit,
  onDeactivate,
}: AssistanceTypesTableProps) {
  return (
    <div className="table-wrap">
      <table className="org-table">
        <thead>
          <tr>
            <th>קוד</th>
            <th>שם</th>
            <th>תיאור</th>
            <th>סכום ברירת מחדל</th>
            <th>תדירות</th>
            <th>סטטוס</th>
            <th>פעולות</th>
          </tr>
        </thead>
        <tbody>
          {types.length === 0 && (
            <tr>
              <td colSpan={7} className="empty-row">אין סוגי סיוע להצגה</td>
            </tr>
          )}
          {types.map((t) => (
            <tr key={t.id} className={t.status === 'inactive' ? 'row-disabled' : undefined}>
              <td><code>{t.typeCode}</code></td>
              <td>{t.name}</td>
              <td>{t.description ?? '—'}</td>
              <td>{formatAmount(t.defaultAmount, t.currency)}</td>
              <td>{translateFrequency(t.frequency)}</td>
              <td>
                <span
                  className={`status-badge status-${
                    t.status === 'inactive' ? 'suspended' : t.status
                  }`}
                >
                  {translateStatus(t.status)}
                </span>
              </td>
              <td className="actions-cell">
                {canManage && t.status === 'active' && onEdit && (
                  <button
                    type="button"
                    className="btn-small"
                    onClick={() => onEdit(t)}
                  >
                    ערוך
                  </button>
                )}
                {canManage && t.status === 'active' && onDeactivate && (
                  <button
                    type="button"
                    className="btn-small btn-danger"
                    onClick={() => onDeactivate(t)}
                  >
                    השבת
                  </button>
                )}
                {!canManage && <span className="hint-text">צפייה בלבד</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
