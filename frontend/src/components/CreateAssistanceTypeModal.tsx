import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  assistanceFrequencies,
  createAssistanceType,
  type AssistanceFrequency,
  type AssistanceTypeDto,
  type CreateAssistanceTypePayload,
} from '../api/assistanceTypes';
import { translateFrequency } from './roleLabel';

interface CreateAssistanceTypeModalProps {
  onClose: () => void;
  onCreated: (created: AssistanceTypeDto) => void;
}

const TYPE_CODE_PATTERN = /^[A-Z0-9-]{2,50}$/;

export function CreateAssistanceTypeModal({
  onClose,
  onCreated,
}: CreateAssistanceTypeModalProps) {
  const [typeCode, setTypeCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [defaultAmount, setDefaultAmount] = useState<string>('');
  const [frequency, setFrequency] = useState<AssistanceFrequency>('monthly');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  function handleOverlayClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    if (loading) return;
    onClose();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    const normalizedCode = typeCode.trim().toUpperCase();
    if (!TYPE_CODE_PATTERN.test(normalizedCode)) {
      setError('קוד סוג הסיוע חייב להיות באותיות גדולות, ספרות ומקף בלבד');
      return;
    }

    let amount: number | null = null;
    if (defaultAmount.trim().length > 0) {
      const parsed = Number(defaultAmount);
      if (Number.isNaN(parsed) || parsed < 0 || parsed > 1000000) {
        setError('סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000');
        return;
      }
      amount = parsed;
    }

    setLoading(true);
    try {
      const payload: CreateAssistanceTypePayload = {
        typeCode: normalizedCode,
        name: name.trim(),
        description: description.trim().length > 0 ? description.trim() : null,
        defaultAmount: amount,
        frequency,
      };
      const created = await createAssistanceType(payload);
      onCreated(created);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={handleOverlayClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>יצירת סוג סיוע חדש</h2>
        <p className="hint-text">המטבע הוא ש״ח בלבד.</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="new-type-code">קוד סוג סיוע</label>
          <input
            id="new-type-code"
            type="text"
            value={typeCode}
            onChange={(e) => setTypeCode(e.target.value.toUpperCase())}
            disabled={loading}
            required
            minLength={2}
            maxLength={50}
            placeholder="A-Z, 0-9, -"
          />
          <label htmlFor="new-type-name">שם סוג הסיוע</label>
          <input
            id="new-type-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="new-type-description">תיאור (אופציונלי)</label>
          <textarea
            id="new-type-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            disabled={loading}
            rows={3}
            maxLength={1000}
          />
          <label htmlFor="new-type-amount">סכום ברירת מחדל בש״ח (אופציונלי)</label>
          <input
            id="new-type-amount"
            type="number"
            value={defaultAmount}
            onChange={(e) => setDefaultAmount(e.target.value)}
            disabled={loading}
            min={0}
            max={1000000}
            step="0.01"
          />
          <label htmlFor="new-type-frequency">תדירות</label>
          <select
            id="new-type-frequency"
            value={frequency}
            onChange={(e) => setFrequency(e.target.value as AssistanceFrequency)}
            disabled={loading}
            required
          >
            {assistanceFrequencies.map((f) => (
              <option key={f} value={f}>
                {translateFrequency(f)}
              </option>
            ))}
          </select>
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={handleOverlayClose}
              disabled={loading}
            >
              ביטול
            </button>
            <button type="submit" disabled={loading}>
              {loading ? 'יוצר...' : 'צור סוג סיוע'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
