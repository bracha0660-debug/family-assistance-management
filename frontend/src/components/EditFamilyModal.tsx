import { useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import {
  updateFamily,
  type FamilyDto,
  type UpdateFamilyPayload,
} from '../api/families';
import { isValidIsraeliId } from '../validation/israeliId';

interface EditFamilyModalProps {
  family: FamilyDto;
  onClose: () => void;
  onUpdated: () => void;
}

export function EditFamilyModal({ family, onClose, onUpdated }: EditFamilyModalProps) {
  const [headOfHouseholdName, setHeadOfHouseholdName] = useState(family.headOfHouseholdName);
  const [headIdNumber, setHeadIdNumber] = useState(family.headIdNumber ?? '');
  const [phone, setPhone] = useState(family.phone ?? '');
  const [address, setAddress] = useState(family.address ?? '');
  const [householdSize, setHouseholdSize] = useState<string>(String(family.householdSize));
  const [notes, setNotes] = useState(family.notes ?? '');
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

    const trimmedId = headIdNumber.trim();
    if (trimmedId.length > 0 && !isValidIsraeliId(trimmedId)) {
      setError('מספר תעודת זהות אינו תקין');
      return;
    }

    const sizeNumber = householdSize.trim().length > 0 ? Number(householdSize) : 0;
    if (Number.isNaN(sizeNumber) || sizeNumber < 0 || sizeNumber > 50) {
      setError('גודל משק בית חייב להיות בין 0 ל-50');
      return;
    }

    const payload: UpdateFamilyPayload = {};
    const trimmedName = headOfHouseholdName.trim();
    if (trimmedName !== family.headOfHouseholdName) payload.headOfHouseholdName = trimmedName;
    if ((trimmedId.length > 0 ? trimmedId : null) !== (family.headIdNumber ?? null))
      payload.headIdNumber = trimmedId.length > 0 ? trimmedId : null;
    const newPhone = phone.trim().length > 0 ? phone.trim() : null;
    if (newPhone !== (family.phone ?? null)) payload.phone = newPhone;
    const newAddress = address.trim().length > 0 ? address.trim() : null;
    if (newAddress !== (family.address ?? null)) payload.address = newAddress;
    if (sizeNumber !== family.householdSize) payload.householdSize = sizeNumber;
    const newNotes = notes.trim().length > 0 ? notes.trim() : null;
    if (newNotes !== (family.notes ?? null)) payload.notes = newNotes;

    if (Object.keys(payload).length === 0) {
      setError('אין שינויים לעדכון');
      return;
    }

    setLoading(true);
    try {
      await updateFamily(family.id, family.version, payload);
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
        <h2>עריכת משפחה</h2>
        <p>קוד משפחה: <strong>{family.familyCode}</strong></p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="edit-family-name">שם ראש משק בית</label>
          <input
            id="edit-family-name"
            type="text"
            value={headOfHouseholdName}
            onChange={(e) => setHeadOfHouseholdName(e.target.value)}
            disabled={loading}
            required
            minLength={2}
            maxLength={200}
          />
          <label htmlFor="edit-family-id">תעודת זהות (אופציונלי)</label>
          <input
            id="edit-family-id"
            type="text"
            value={headIdNumber}
            onChange={(e) => setHeadIdNumber(e.target.value)}
            disabled={loading}
            inputMode="numeric"
            maxLength={9}
          />
          <label htmlFor="edit-family-phone">טלפון</label>
          <input
            id="edit-family-phone"
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            disabled={loading}
            maxLength={30}
          />
          <label htmlFor="edit-family-address">כתובת</label>
          <input
            id="edit-family-address"
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            disabled={loading}
            maxLength={300}
          />
          <label htmlFor="edit-family-size">גודל משק בית</label>
          <input
            id="edit-family-size"
            type="number"
            value={householdSize}
            onChange={(e) => setHouseholdSize(e.target.value)}
            disabled={loading}
            min={0}
            max={50}
          />
          <label htmlFor="edit-family-notes">הערות</label>
          <textarea
            id="edit-family-notes"
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
