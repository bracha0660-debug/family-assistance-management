import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import {
  assistanceFrequencies,
  updateAssistanceType,
  type AssistanceFrequency,
  type AssistanceTypeDto,
  type RelatedSupplierDto,
  type UpdateAssistanceTypePayload,
} from '../api/assistanceTypes';
import { listSuppliers } from '../api/suppliers';
import { translateFrequency } from './roleLabel';
import { FormField, ModalShell } from './ModalShell';
import { RelatedSupplierTags } from './RelatedSupplierTags';
import { focusFirstInvalidField } from '../utils/formValidation';

interface EditAssistanceTypeModalProps {
  assistanceType: AssistanceTypeDto;
  onClose: () => void;
  onUpdated: () => void;
}

function isFrequency(value: string): value is AssistanceFrequency {
  return (assistanceFrequencies as readonly string[]).includes(value);
}

function relatedSupplierIdsEqual(a: RelatedSupplierDto[], b: RelatedSupplierDto[]): boolean {
  if (a.length !== b.length) return false;
  const aIds = a.map((s) => s.id).sort();
  const bIds = b.map((s) => s.id).sort();
  return aIds.every((id, index) => id === bIds[index]);
}

const FOCUS_ORDER = ['edit-type-name', 'edit-type-amount'];

export function EditAssistanceTypeModal({
  assistanceType,
  onClose,
  onUpdated,
}: EditAssistanceTypeModalProps) {
  const initialRelated = useMemo(
    () => assistanceType.relatedSuppliers ?? [],
    [assistanceType.relatedSuppliers],
  );

  const initialFrequency: AssistanceFrequency = isFrequency(assistanceType.frequency)
    ? assistanceType.frequency
    : 'monthly';

  const [name, setName] = useState(assistanceType.name);
  const [description, setDescription] = useState(assistanceType.description ?? '');
  const [defaultAmount, setDefaultAmount] = useState<string>(
    assistanceType.defaultAmount !== null ? String(assistanceType.defaultAmount) : '',
  );
  const [frequency, setFrequency] = useState<AssistanceFrequency>(initialFrequency);
  const [activeSuppliers, setActiveSuppliers] = useState<RelatedSupplierDto[]>([]);
  const [selectedRelatedSuppliers, setSelectedRelatedSuppliers] = useState<RelatedSupplierDto[]>(initialRelated);
  const [addSupplierId, setAddSupplierId] = useState('');
  const [suppliersLoading, setSuppliersLoading] = useState(true);
  const [amountError, setAmountError] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setSuppliersLoading(true);
    listSuppliers()
      .then((res) => {
        if (cancelled) return;
        setActiveSuppliers(
          res.suppliers
            .filter((s) => s.status === 'active')
            .map((s) => ({ id: s.id, name: s.name })),
        );
      })
      .catch(() => {
        if (!cancelled) setActiveSuppliers([]);
      })
      .finally(() => {
        if (!cancelled) setSuppliersLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const availableToAdd = activeSuppliers.filter(
    (s) => !selectedRelatedSuppliers.some((r) => r.id === s.id),
  );

  function handleAddSupplier() {
    if (!addSupplierId) return;
    const supplier = activeSuppliers.find((s) => s.id === addSupplierId);
    if (!supplier) return;
    setSelectedRelatedSuppliers((prev) => [...prev, supplier]);
    setAddSupplierId('');
  }

  function handleRemoveRelatedSupplier(supplierId: string) {
    setSelectedRelatedSuppliers((prev) => prev.filter((s) => s.id !== supplierId));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    setAmountError(null);

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
        setAmountError('סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000');
        focusFirstInvalidField(FOCUS_ORDER);
        return;
      }
      if (parsed !== assistanceType.defaultAmount) {
        payload.defaultAmount = parsed;
      }
    }

    if (frequency !== assistanceType.frequency) payload.frequency = frequency;

    if (!relatedSupplierIdsEqual(selectedRelatedSuppliers, initialRelated)) {
      payload.relatedSupplierIds = selectedRelatedSuppliers.map((s) => s.id);
    }

    if (Object.keys(payload).length === 0) {
      setError('אין שינויים לעדכון');
      return;
    }

    const form = e.currentTarget as HTMLFormElement;
    if (!form.reportValidity()) {
      focusFirstInvalidField(FOCUS_ORDER);
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
    <ModalShell
      title="עריכת סוג סיוע"
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formNoValidate={false}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={() => onClose()} disabled={loading}>
            ביטול
          </button>
          <button type="submit" disabled={loading}>
            {loading ? 'שומר...' : 'שמור שינויים'}
          </button>
        </>
      )}
    >
      <p>קוד: <strong>{assistanceType.typeCode}</strong></p>
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
      <FormField id="edit-type-amount" label="סכום ברירת מחדל בש״ח" error={amountError}>
        <input
          id="edit-type-amount"
          type="number"
          value={defaultAmount}
          onChange={(e) => {
            setDefaultAmount(e.target.value);
            if (amountError) setAmountError(null);
          }}
          disabled={loading}
          min={0}
          max={1000000}
          step="0.01"
          placeholder="ריק = ללא ברירת מחדל"
          aria-invalid={amountError ? true : undefined}
        />
      </FormField>
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
      <label htmlFor="edit-type-add-supplier">הוסף ספק מקושר</label>
      <div className="related-supplier-picker">
        <select
          id="edit-type-add-supplier"
          value={addSupplierId}
          onChange={(e) => setAddSupplierId(e.target.value)}
          disabled={loading || suppliersLoading || availableToAdd.length === 0}
        >
          <option value="">— בחר ספק —</option>
          {availableToAdd.map((s) => (
            <option key={s.id} value={s.id}>{s.name}</option>
          ))}
        </select>
        <button
          type="button"
          className="btn-small"
          onClick={handleAddSupplier}
          disabled={loading || suppliersLoading || !addSupplierId}
        >
          הוסף
        </button>
      </div>
      <label>ספקים קשורים</label>
      <RelatedSupplierTags
        suppliers={selectedRelatedSuppliers}
        editable
        disabled={loading}
        onRemove={handleRemoveRelatedSupplier}
        emptyLabel="אין ספקים קשורים"
      />
    </ModalShell>
  );
}
