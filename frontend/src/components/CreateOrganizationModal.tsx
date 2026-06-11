import { useState } from 'react';
import type { FormEvent } from 'react';
import { createOrganization } from '../api/admin';

interface CreateOrganizationModalProps {
  onClose: () => void;
  onCreated: () => void;
}

export function CreateOrganizationModal({ onClose, onCreated }: CreateOrganizationModalProps) {
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await createOrganization(name, code.toUpperCase());
      onCreated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>יצירת ארגון חדש</h2>
        <form onSubmit={handleSubmit}>
          <label htmlFor="org-name">שם הארגון</label>
          <input
            id="org-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={loading}
            required
          />
          <label htmlFor="org-code">קוד ארגון (אותיות גדולות, מספרים, מקף)</label>
          <input
            id="org-code"
            type="text"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            disabled={loading}
            required
          />
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>
              ביטול
            </button>
            <button type="submit" disabled={loading}>
              {loading ? 'יוצר...' : 'צור ארגון'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
