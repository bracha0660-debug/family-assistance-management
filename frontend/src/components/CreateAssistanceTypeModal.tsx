import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  assistanceFrequencies,
  createAssistanceType,
  type AssistanceFrequency,
  type AssistanceTypeDto,
  type CreateAssistanceTypePayload,
  type RelatedSupplierDto,
} from '../api/assistanceTypes';
import { listSuppliers } from '../api/suppliers';
import { translateFrequency } from './roleLabel';
import { FormField, ModalShell } from './ModalShell';
import { RelatedSupplierTags } from './RelatedSupplierTags';
import { focusFirstInvalidField } from '../utils/formValidation';

interface CreateAssistanceTypeModalProps {
  onClose: () => void;
  onCreated: (created: AssistanceTypeDto) => void;
}

const TYPE_CODE_PATTERN = /^[A-Z0-9-]{2,50}$/;

const FOCUS_ORDER = ['new-type-code', 'new-type-name', 'new-type-amount'];

export function CreateAssistanceTypeModal({
  onClose,
  onCreated,
}: CreateAssistanceTypeModalProps) {
  const [typeCode, setTypeCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [defaultAmount, setDefaultAmount] = useState<string>('');
  const [frequency, setFrequency] = useState<AssistanceFrequency>('monthly');
  const [activeSuppliers, setActiveSuppliers] = useState<RelatedSupplierDto[]>([]);
  const [selectedRelatedSuppliers, setSelectedRelatedSuppliers] = useState<RelatedSupplierDto[]>([]);
  const [addSupplierId, setAddSupplierId] = useState('');
  const [suppliersLoading, setSuppliersLoading] = useState(true);
  const [typeCodeError, setTypeCodeError] = useState<string | null>(null);
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
    setTypeCodeError(null);
    setAmountError(null);

    const normalizedCode = typeCode.trim().toUpperCase();
    if (!TYPE_CODE_PATTERN.test(normalizedCode)) {
      setTypeCodeError('קוד סוג הסיוע חייב להיות באותיות גדולות, ספרות ומקף בלבד');
      focusFirstInvalidField(FOCUS_ORDER);
      return;
    }

    let amount: number | null = null;
    if (defaultAmount.trim().length > 0) {
      const parsed = Number(defaultAmount);
      if (Number.isNaN(parsed) || parsed < 0 || parsed > 1000000) {
        setAmountError('סכום ברירת מחדל חייב להיות בין 0 ל-1,000,000');
        focusFirstInvalidField(FOCUS_ORDER);
        return;
      }
      amount = parsed;
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
        defaultAmount: amount,
        frequency,
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
      hint="המטבע הוא ש״ח בלבד."
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
      <FormField id="new-type-amount" label="סכום ברירת מחדל בש״ח (אופציונלי)" error={amountError}>
        <input
          id="new-type-amount"
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
          aria-invalid={amountError ? true : undefined}
        />
      </FormField>
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
