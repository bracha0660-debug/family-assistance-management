import { maskBankAccount, type FamilyDto } from '../api/families';
import { translateStatus } from './roleLabel';

interface FamiliesTableProps {
  families: FamilyDto[];
  canManage: (family: FamilyDto) => boolean;
  showCoordinator: boolean;
  onEdit?: (family: FamilyDto) => void;
  onDeactivate?: (family: FamilyDto) => void;
  onRestore?: (family: FamilyDto) => void;
}

export function FamiliesTable({
  families,
  canManage,
  showCoordinator,
  onEdit,
  onDeactivate,
  onRestore,
}: FamiliesTableProps) {
  const colSpan = showCoordinator ? 10 : 9;

  return (
    <div className="table-wrap">
      <table className="org-table">
        <thead>
          <tr>
            <th>קוד משפחה</th>
            <th>מספר חשבונאי</th>
            <th>שם משפחה</th>
            <th>שם האב</th>
            <th>ת.ז. האב</th>
            <th>חשבון בנק</th>
            <th>טלפון</th>
            {showCoordinator && <th>מתאם/ת</th>}
            <th>סטטוס</th>
            <th>פעולות</th>
          </tr>
        </thead>
        <tbody>
          {families.length === 0 && (
            <tr>
              <td colSpan={colSpan} className="empty-row">
                אין משפחות להצגה
              </td>
            </tr>
          )}
          {families.map((f) => {
            const manageable = canManage(f);
            return (
              <tr key={f.id} className={f.status === 'inactive' ? 'row-disabled' : undefined}>
                <td><code>{f.familyCode}</code></td>
                <td>{f.accountingCode}</td>
                <td>{f.familyLastName}</td>
                <td>{f.fatherName ?? '—'}</td>
                <td>{f.fatherIsraeliId ?? '—'}</td>
                <td><code>{maskBankAccount(f)}</code></td>
                <td>{f.phone ?? '—'}</td>
                {showCoordinator && <td>{f.assignedCoordinatorName}</td>}
                <td>
                  <span
                    className={`status-badge status-${
                      f.status === 'inactive' ? 'suspended' : f.status
                    }`}
                  >
                    {translateStatus(f.status)}
                  </span>
                </td>
                <td className="actions-cell">
                  {manageable && f.status === 'active' && onEdit && (
                    <button
                      type="button"
                      className="btn-small"
                      onClick={() => onEdit(f)}
                    >
                      ערוך
                    </button>
                  )}
                  {manageable && f.status === 'active' && onDeactivate && (
                    <button
                      type="button"
                      className="btn-small btn-danger"
                      onClick={() => onDeactivate(f)}
                    >
                      השבת
                    </button>
                  )}
                  {manageable && f.status === 'inactive' && onRestore && (
                    <button
                      type="button"
                      className="btn-small"
                      onClick={() => onRestore(f)}
                    >
                      שחזר
                    </button>
                  )}
                  {!manageable && <span className="hint-text">צפייה בלבד</span>}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
