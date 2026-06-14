import { useCallback, useEffect, useState } from 'react';
import { listFamilies, type FamilyListResponse } from '../api/families';
import { FamiliesTable } from '../components/FamiliesTable';

export function ManagerFamiliesPage() {
  const [data, setData] = useState<FamilyListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

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

  return (
    <div>
      <p className="read-only-banner">תצוגה ארגונית - צפייה בלבד.</p>
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
      <div className="toolbar">
        <button type="button" className="btn-secondary" onClick={loadFamilies}>רענן</button>
      </div>
      {error && <div className="error" role="alert">{error}</div>}
      {loading ? (
        <p>טוען משפחות...</p>
      ) : (
        <FamiliesTable
          families={data?.families ?? []}
          canManage={() => false}
          showCoordinator={true}
        />
      )}
    </div>
  );
}
