import { useCallback, useEffect, useState } from 'react';
import {
  listAssistanceTypes,
  type AssistanceTypeListResponse,
} from '../api/assistanceTypes';
import { AssistanceTypesTable } from '../components/AssistanceTypesTable';

export function OrgAdminAssistanceTypesPage() {
  const [data, setData] = useState<AssistanceTypeListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

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

  return (
    <div>
      <p className="read-only-banner">
        תצוגת ניהול - צפייה בלבד. ניהול סוגי הסיוע מתבצע על ידי הכספים.
      </p>
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
      <div className="toolbar">
        <button type="button" className="btn-secondary" onClick={loadTypes}>רענן</button>
      </div>
      {error && <div className="error" role="alert">{error}</div>}
      {loading ? (
        <p>טוען סוגי סיוע...</p>
      ) : (
        <AssistanceTypesTable
          types={data?.assistanceTypes ?? []}
          canManage={false}
        />
      )}
    </div>
  );
}
