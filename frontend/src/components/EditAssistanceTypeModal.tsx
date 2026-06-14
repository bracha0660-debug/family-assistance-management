import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  assistanceFrequencies,
  updateAssistanceType,
  type AssistanceFrequency,
  type AssistanceTypeDto,
  type UpdateAssistanceTypePayload,
} from '../api/assistanceTypes';
import { translateFrequency } from './roleLabel';

interface EditAssistanceTypeModalProps {
  assistanceType: AssistanceTypeDto;
  onClose: () => void;
  onUpdated: () => void;
}

function isFrequency(value: string): value is AssistanceFrequency {
  return (assistanceFrequencies as readonly string[]).includes(value);
}

export function EditAssistanceTypeModal({
  assistanceType,
  onClose,
  onUpdated,
}: EditAssistanceTypeModalProps) {
  const initialFrequency: AssistanceFrequency = isFrequency(assistanceType.frequency)
    ? assistanceType.frequency
    : 'monthly';

  const [name, setName] = useState(assistanceType.name);
  const [description, setDescription] = useState(assistanceType.description ?? '');
  const [defaultAmount, setDefaultAmount] = useState<string>(
    assistanceType.defaultAmount !== null ? String(assistanceType.defaultAmount) : '',
  );
  const [frequency, setFrequency] = useState<AssistanceFrequency>(initialFrequency);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  function handleClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    if (loading) return;
    onClose();
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');

    const payload: UpdateAssistanceTypePayload = {};
    const trimmedName = name.trim();
    if (trimmedName !== assistanceType.name) payload.name = trimmedName;

    const newDescription = description.trim().length > 0 ? description.trim() : null;
    if (newDescription !== (assistanceType.description ?? null))
      payload.description = newDescription;

    if (defaultAmount.trim().length === 0) {
      if (assistanceType.defaultAmount !== null) {
        payload.clearDefaultAmount = true;
      }
    } else {
      const parsed = Number(defaultAmount);
      if (Number.isNaN(parsed) || parsed < 0 || parsed > 1000000) {
        setError('סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000');
        return;
      }
      if (parsed !== assistanceType.defaultAmount) {
        payload.defaultAmount = parsed;
      }
    }

    if (frequency !== assistanceType.frequency) payload.frequency = frequency;

    if (Object.keys(payload).length === 0) {
      setError('אין שינויים לעדכון');
      return;
    }

    setLoading(true);
    try {
      await updateAssistanceType(assistanceType.id, assistanceType.version, payload);
      onUpdated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>עריכת סוג סיוע</h2>
        <p>קוד: <strong>{assistanceType.typeCode}</strong></p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="edit-type-name">שם סוג הסיוע</label>
          <input
            id="edit-type-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="edit-type-description">תיאור</label>
          <textarea
            id="edit-type-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            disabled={loading}
            rows={3}
            maxLength={1000}
          />
          <label htmlFor="edit-type-amount">סכום ברירת מחדל בש״ח</label>
          <input
            id="edit-type-amount"
            type="number"
            value={defaultAmount}
            onChange={(e) => setDefaultAmount(e.target.value)}
            disabled={loading}
            min={0}
            max={1000000}
            step="0.01"
            placeholder="ריק = ללא ברירת מחדל"
          />
          <label htmlFor="edit-type-frequency">תדירות</label>
          <select
            id="edit-type-frequency"
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
              onClick={handleClose}
              disabled={loading}
            >
              ביטול
            </button>
            <button type="submit" disabled={loading}>
              {loading ? 'שומר...' : 'שמור שינויים'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
