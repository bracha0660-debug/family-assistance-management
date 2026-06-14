import { useCallback, useEffect, useState } from 'react';
import {
  listOrgActivity,
  type ActivityLogEntryDto,
} from '../api/orgActivity';
import { translateAction, translateFieldName } from '../components/roleLabel';

const PAGE_SIZE = 100;

export function OrgActivityLogPage() {
  const [entries, setEntries] = useState<ActivityLogEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [hasMore, setHasMore] = useState(false);

  const loadFirstPage = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const result = await listOrgActivity({ limit: PAGE_SIZE, offset: 0 });
      setEntries(result.entries);
      setHasMore(result.entries.length === PAGE_SIZE);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadFirstPage();
  }, [loadFirstPage]);

  async function handleLoadMore() {
    setLoadingMore(true);
    setError('');
    try {
      const result = await listOrgActivity({ limit: PAGE_SIZE, offset: entries.length });
      setEntries((prev) => [...prev, ...result.entries]);
      setHasMore(result.entries.length === PAGE_SIZE);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoadingMore(false);
    }
  }

  function formatDate(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toLocaleString('he-IL');
  }

  function describeAction(entry: ActivityLogEntryDto): string {
    const base = translateAction(entry.action);
    if (entry.fieldName) {
      const field = translateFieldName(entry.fieldName);
      if (field) {
        return `${base} – ${field}`;
      }
    }
    return base;
  }

  return (
    <div>
      <div className="toolbar">
        <button type="button" className="btn-secondary" onClick={loadFirstPage}>
          רענן
        </button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען יומן פעילות...</p>
      ) : (
        <div className="table-wrap">
          <table className="org-table">
            <thead>
              <tr>
                <th>תאריך</th>
                <th>משתמש</th>
                <th>קוד אירוע</th>
                <th>פעולה</th>
                <th>סיבה</th>
              </tr>
            </thead>
            <tbody>
              {entries.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty-row">אין רישומי פעילות בארגון</td>
                </tr>
              )}
              {entries.map((entry) => (
                <tr key={entry.id}>
                  <td>{formatDate(entry.createdAt)}</td>
                  <td>
                    {entry.actorFullName}
                    <div className="hint-text">{entry.actorUsername}</div>
                  </td>
                  <td><code>{entry.eventCode}</code></td>
                  <td>{describeAction(entry)}</td>
                  <td>{entry.reason ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {hasMore && !loading && (
        <div className="toolbar">
          <button type="button" onClick={handleLoadMore} disabled={loadingMore}>
            {loadingMore ? 'טוען...' : 'טען עוד'}
          </button>
        </div>
      )}
    </div>
  );
}
