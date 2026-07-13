import type { AssistanceTypeDto } from '../api/assistanceTypes';
import { RelatedSupplierTags } from './RelatedSupplierTags';
import { translateStatus } from './roleLabel';

interface AssistanceTypesTableProps {
  types: AssistanceTypeDto[];
  canManage: boolean;
  onEdit?: (type: AssistanceTypeDto) => void;
  onDeactivate?: (type: AssistanceTypeDto) => void;
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
            <th>ספקים קשורים</th>
            <th>סטטוס</th>
            <th>פעולות</th>
          </tr>
        </thead>
        <tbody>
          {types.length === 0 && (
            <tr>
              <td colSpan={6} className="empty-row">אין סוגי סיוע להצגה</td>
            </tr>
          )}
          {types.map((t) => (
            <tr key={t.id} className={t.status === 'inactive' ? 'row-disabled' : undefined}>
              <td><code>{t.typeCode}</code></td>
              <td>{t.name}</td>
              <td>{t.description ?? '—'}</td>
              <td>
                <RelatedSupplierTags
                  suppliers={t.relatedSuppliers ?? []}
                  editable={false}
                  compact
                />
              </td>
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
