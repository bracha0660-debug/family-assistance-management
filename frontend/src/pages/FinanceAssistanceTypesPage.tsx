import { useCallback, useEffect, useState } from 'react';
import type { UserDto } from '../api/auth';
import {
  listAssistanceTypes,
  type AssistanceTypeDto,
  type AssistanceTypeListResponse,
} from '../api/assistanceTypes';
import { PERMISSION_KEYS } from '../api/permissions';
import { AssistanceTypesTable } from '../components/AssistanceTypesTable';
import { CreateAssistanceTypeModal } from '../components/CreateAssistanceTypeModal';
import { DeactivateAssistanceTypeDialog } from '../components/DeactivateAssistanceTypeDialog';
import { EditAssistanceTypeModal } from '../components/EditAssistanceTypeModal';
import { hasPermission } from '../hooks/usePermissions';

interface FinanceAssistanceTypesPageProps {
  user: UserDto;
}

export function FinanceAssistanceTypesPage({ user }: FinanceAssistanceTypesPageProps) {
  const [data, setData] = useState<AssistanceTypeListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<AssistanceTypeDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<AssistanceTypeDto | null>(null);
  const [createdType, setCreatedType] = useState<AssistanceTypeDto | null>(null);

  const loadTypes = useCallback(async () => {
    setError('');
    try {
      const result = await listAssistanceTypes();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadTypes();
  }, [loadTypes]);

  function handleTypeCreated(created: AssistanceTypeDto) {
    setShowCreate(false);
    setCreatedType(created);
    loadTypes();
  }

  return (
    <div>
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ סוגי סיוע</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">פעילים</span>
            <span className="summary-value">{data.summary.active}</span>
          </div>
          <div className="summary-card summary-suspended">
            <span className="summary-label">לא פעילים</span>
            <span className="summary-value">{data.summary.inactive}</span>
          </div>
        </div>
      )}

      {createdType && (
        <div className="success-banner" role="status">
          סוג הסיוע <strong>{createdType.typeCode}</strong> נוצר בהצלחה ({createdType.name}).
          <button
            type="button"
            className="btn-small"
            onClick={() => setCreatedType(null)}
          >
            סגור
          </button>
        </div>
      )}

      <div className="toolbar">
        {hasPermission(user, PERMISSION_KEYS.assistanceTypesCreate) && (
          <button type="button" onClick={() => setShowCreate(true)}>סוג סיוע חדש</button>
        )}
        <button type="button" className="btn-secondary" onClick={loadTypes}>רענן</button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען סוגי סיוע...</p>
      ) : (
        <AssistanceTypesTable
          types={data?.assistanceTypes ?? []}
          canManage={
            hasPermission(user, PERMISSION_KEYS.assistanceTypesEdit)
            || hasPermission(user, PERMISSION_KEYS.assistanceTypesDeactivate)
          }
          onEdit={hasPermission(user, PERMISSION_KEYS.assistanceTypesEdit) ? (t) => setEditTarget(t) : undefined}
          onDeactivate={hasPermission(user, PERMISSION_KEYS.assistanceTypesDeactivate) ? (t) => setDeactivateTarget(t) : undefined}
        />
      )}

      {showCreate && (
        <CreateAssistanceTypeModal
          onClose={() => setShowCreate(false)}
          onCreated={handleTypeCreated}
        />
      )}
      {editTarget && (
        <EditAssistanceTypeModal
          assistanceType={editTarget}
          onClose={() => setEditTarget(null)}
          onUpdated={loadTypes}
        />
      )}
      {deactivateTarget && (
        <DeactivateAssistanceTypeDialog
          assistanceType={deactivateTarget}
          onClose={() => setDeactivateTarget(null)}
          onDeactivated={loadTypes}
        />
      )}
    </div>
  );
}
