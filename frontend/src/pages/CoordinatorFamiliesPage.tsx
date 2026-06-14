import { useCallback, useEffect, useState } from 'react';
import {
  listFamilies,
  type FamilyDto,
  type FamilyListResponse,
} from '../api/families';
import { CreateFamilyModal } from '../components/CreateFamilyModal';
import { DeactivateFamilyDialog } from '../components/DeactivateFamilyDialog';
import { EditFamilyModal } from '../components/EditFamilyModal';
import { FamiliesTable } from '../components/FamiliesTable';

interface CoordinatorFamiliesPageProps {
  currentUserId: string;
}

export function CoordinatorFamiliesPage({ currentUserId }: CoordinatorFamiliesPageProps) {
  const [data, setData] = useState<FamilyListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<FamilyDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<FamilyDto | null>(null);
  const [createdFamily, setCreatedFamily] = useState<FamilyDto | null>(null);

  const loadFamilies = useCallback(async () => {
    setError('');
    try {
      const result = await listFamilies();
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadFamilies();
  }, [loadFamilies]);

  function handleFamilyCreated(created: FamilyDto) {
    setShowCreate(false);
    setCreatedFamily(created);
    loadFamilies();
  }

  return (
    <div>
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ משפחות</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">פעילות</span>
            <span className="summary-value">{data.summary.active}</span>
          </div>
          <div className="summary-card summary-suspended">
            <span className="summary-label">לא פעילות</span>
            <span className="summary-value">{data.summary.inactive}</span>
          </div>
        </div>
      )}

      {createdFamily && (
        <div className="success-banner" role="status">
          המשפחה <strong>{createdFamily.familyCode}</strong> נוצרה בהצלחה (
          {createdFamily.headOfHouseholdName}).
          <button
            type="button"
            className="btn-small"
            onClick={() => setCreatedFamily(null)}
          >
            סגור
          </button>
        </div>
      )}

      <div className="toolbar">
        <button type="button" onClick={() => setShowCreate(true)}>משפחה חדשה</button>
        <button type="button" className="btn-secondary" onClick={loadFamilies}>רענן</button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען משפחות...</p>
      ) : (
        <FamiliesTable
          families={data?.families ?? []}
          canManage={(f) => f.assignedCoordinatorId === currentUserId}
          showCoordinator={false}
          onEdit={(f) => setEditTarget(f)}
          onDeactivate={(f) => setDeactivateTarget(f)}
        />
      )}

      {showCreate && (
        <CreateFamilyModal
          onClose={() => setShowCreate(false)}
          onCreated={handleFamilyCreated}
        />
      )}
      {editTarget && (
        <EditFamilyModal
          family={editTarget}
          onClose={() => setEditTarget(null)}
          onUpdated={loadFamilies}
        />
      )}
      {deactivateTarget && (
        <DeactivateFamilyDialog
          family={deactivateTarget}
          onClose={() => setDeactivateTarget(null)}
          onDeactivated={loadFamilies}
        />
      )}
    </div>
  );
}
