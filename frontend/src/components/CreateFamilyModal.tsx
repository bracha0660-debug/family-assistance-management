import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  createFamily,
  type CreateFamilyPayload,
  type FamilyDto,
} from '../api/families';
import { isValidIsraeliId } from '../validation/israeliId';

interface CreateFamilyModalProps {
  onClose: () => void;
  onCreated: (created: FamilyDto) => void;
}

export function CreateFamilyModal({ onClose, onCreated }: CreateFamilyModalProps) {
  const [headOfHouseholdName, setHeadOfHouseholdName] = useState('');
  const [headIdNumber, setHeadIdNumber] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');
  const [householdSize, setHouseholdSize] = useState<string>('');
  const [notes, setNotes] = useState('');
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

    const trimmedId = headIdNumber.trim();
    if (trimmedId.length > 0 && !isValidIsraeliId(trimmedId)) {
      setError('מספר תעודת זהות אינו תקין');
      return;
    }

    const sizeNumber = householdSize.trim().length > 0 ? Number(householdSize) : null;
    if (sizeNumber !== null && (Number.isNaN(sizeNumber) || sizeNumber < 0 || sizeNumber > 50)) {
      setError('גודל משק בית חייב להיות בין 0 ל-50');
      return;
    }

    setLoading(true);
    try {
      const payload: CreateFamilyPayload = {
        headOfHouseholdName: headOfHouseholdName.trim(),
        headIdNumber: trimmedId.length > 0 ? trimmedId : null,
        phone: phone.trim().length > 0 ? phone.trim() : null,
        address: address.trim().length > 0 ? address.trim() : null,
        householdSize: sizeNumber,
        notes: notes.trim().length > 0 ? notes.trim() : null,
      };
      const created = await createFamily(payload);
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
        <h2>יצירת משפחה חדשה</h2>
        <p className="hint-text">קוד המשפחה יוקצה אוטומטית בפורמט F-000001.</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="new-family-name">שם ראש משק בית</label>
          <input
            id="new-family-name"
            type="text"
            value={headOfHouseholdName}
            onChange={(e) => setHeadOfHouseholdName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="new-family-id">תעודת זהות (אופציונלי)</label>
          <input
            id="new-family-id"
            type="text"
            value={headIdNumber}
            onChange={(e) => setHeadIdNumber(e.target.value)}
            disabled={loading}
            inputMode="numeric"
            maxLength={9}
            placeholder="9 ספרות"
          />
          <label htmlFor="new-family-phone">טלפון (אופציונלי)</label>
          <input
            id="new-family-phone"
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            disabled={loading}
            maxLength={30}
          />
          <label htmlFor="new-family-address">כתובת (אופציונלי)</label>
          <input
            id="new-family-address"
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            disabled={loading}
            maxLength={300}
          />
          <label htmlFor="new-family-size">גודל משק בית</label>
          <input
            id="new-family-size"
            type="number"
            value={householdSize}
            onChange={(e) => setHouseholdSize(e.target.value)}
            disabled={loading}
            min={0}
            max={50}
          />
          <label htmlFor="new-family-notes">הערות (אופציונלי)</label>
          <textarea
            id="new-family-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            disabled={loading}
            rows={3}
            maxLength={2000}
          />
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
              {loading ? 'יוצר...' : 'צור משפחה'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
