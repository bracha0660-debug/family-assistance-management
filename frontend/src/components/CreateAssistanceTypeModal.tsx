import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  createAssistanceType,
  type AssistanceTypeDto,
  type CreateAssistanceTypePayload,
  type RelatedSupplierDto,
} from '../api/assistanceTypes';
import { listSuppliers } from '../api/suppliers';
import { FormField, ModalShell } from './ModalShell';
import { RelatedSupplierTags } from './RelatedSupplierTags';
import { focusFirstInvalidField } from '../utils/formValidation';

interface CreateAssistanceTypeModalProps {
  onClose: () => void;
  onCreated: (created: AssistanceTypeDto) => void;
}

const TYPE_CODE_PATTERN = /^[A-Z0-9-]{2,50}$/;

const FOCUS_ORDER = ['new-type-code', 'new-type-name'];

export function CreateAssistanceTypeModal({
  onClose,
  onCreated,
}: CreateAssistanceTypeModalProps) {
  const [typeCode, setTypeCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [activeSuppliers, setActiveSuppliers] = useState<RelatedSupplierDto[]>([]);
  const [selectedRelatedSuppliers, setSelectedRelatedSuppliers] = useState<RelatedSupplierDto[]>([]);
  const [addSupplierId, setAddSupplierId] = useState('');
  const [suppliersLoading, setSuppliersLoading] = useState(true);
  const [typeCodeError, setTypeCodeError] = useState<string | null>(null);
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
    setTypeCodeError(null);

    const normalizedCode = typeCode.trim().toUpperCase();
    if (!TYPE_CODE_PATTERN.test(normalizedCode)) {
      setTypeCodeError('קוד סוג הסיוע חייב להיות באותיות גדולות, ספרות ומקף בלבד');
      focusFirstInvalidField(FOCUS_ORDER);
      return;
    }

    const form = e.currentTarget as HTMLFormElement;
    if (!form.reportValidity()) {
      focusFirstInvalidField(FOCUS_ORDER);
      return;
    }

    setLoading(true);
    try {
      const payload: CreateAssistanceTypePayload = {
        typeCode: normalizedCode,
        name: name.trim(),
        description: description.trim().length > 0 ? description.trim() : null,
        relatedSupplierIds: selectedRelatedSuppliers.map((s) => s.id),
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
    <ModalShell
      title="יצירת סוג סיוע חדש"
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
            {loading ? 'יוצר...' : 'צור סוג סיוע'}
          </button>
        </>
      )}
    >
      <FormField id="new-type-code" label="קוד סוג הסיוע" error={typeCodeError}>
        <input
          id="new-type-code"
          type="text"
          value={typeCode}
          onChange={(e) => {
            setTypeCode(e.target.value.toUpperCase());
            if (typeCodeError) setTypeCodeError(null);
          }}
          disabled={loading}
          required
          minLength={2}
          maxLength={50}
          placeholder="A-Z, 0-9, -"
          aria-invalid={typeCodeError ? true : undefined}
        />
      </FormField>
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
      <label htmlFor="new-type-add-supplier">הוסף ספק מקושר</label>
      <div className="related-supplier-picker">
        <select
          id="new-type-add-supplier"
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
