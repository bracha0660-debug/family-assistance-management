import { useEffect, useRef, useState } from 'react';
import { findBankByName, findBankByNumber, ISRAELI_BANKS } from '../data/israeliBanks';
import type { BankFieldErrors } from '../validation/bankFields';
import { ValidatedControl } from './FieldValidation';

export interface BankDetailsValues {
  bankNumber: string;
  branchNumber: string;
  accountNumber: string;
  accountHolderName: string;
}

interface BankDetailsFieldsProps {
  idPrefix: string;
  values: BankDetailsValues;
  defaultAccountHolderName: string;
  accountHolderHint: string;
  disabled?: boolean;
  fieldErrors?: BankFieldErrors;
  onChange: (patch: Partial<BankDetailsValues>) => void;
  onBlurField?: (field: keyof BankDetailsValues | 'bankName') => void;
}

export function BankDetailsFields({
  idPrefix,
  values,
  defaultAccountHolderName,
  accountHolderHint,
  disabled = false,
  fieldErrors = {},
  onChange,
  onBlurField,
}: BankDetailsFieldsProps) {
  const [selectedBankName, setSelectedBankName] = useState('');
  const [syncHint, setSyncHint] = useState<'number' | 'name' | null>(null);
  const holderEditedRef = useRef(false);
  const previousDefaultHolderRef = useRef(defaultAccountHolderName.trim());

  useEffect(() => {
    const bank = findBankByNumber(values.bankNumber);
    setSelectedBankName(bank?.name ?? '');
  }, [values.bankNumber]);

  useEffect(() => {
    if (values.accountHolderName.trim() !== defaultAccountHolderName.trim()
      && values.accountHolderName.trim().length > 0) {
      holderEditedRef.current = true;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const trimmedDefault = defaultAccountHolderName.trim();
    if (trimmedDefault.length === 0 || holderEditedRef.current) {
      previousDefaultHolderRef.current = trimmedDefault;
      return;
    }

    const current = values.accountHolderName.trim();
    const previousDefault = previousDefaultHolderRef.current;
    if (current.length === 0 || current === previousDefault) {
      if (current !== trimmedDefault) {
        onChange({ accountHolderName: trimmedDefault });
      }
    }

    previousDefaultHolderRef.current = trimmedDefault;
  }, [defaultAccountHolderName, values.accountHolderName, onChange]);

  function handleBankNumberChange(raw: string) {
    const digits = raw.replace(/\D/g, '').slice(0, 3);
    onChange({ bankNumber: digits });
    const bank = findBankByNumber(digits);
    if (bank) {
      setSelectedBankName(bank.name);
      setSyncHint('number');
    } else {
      setSelectedBankName('');
      setSyncHint(null);
    }
  }

  function handleBankNameChange(name: string) {
    setSelectedBankName(name);
    const bank = findBankByName(name);
    if (bank) {
      onChange({ bankNumber: bank.number });
      setSyncHint('name');
    } else {
      setSyncHint(null);
    }
  }

  function handleAccountHolderChange(value: string) {
    holderEditedRef.current = true;
    onChange({ accountHolderName: value });
  }

  const bankNumberId = `${idPrefix}-bank-number`;
  const bankNameId = `${idPrefix}-bank-name`;
  const branchId = `${idPrefix}-branch-number`;
  const accountId = `${idPrefix}-account-number`;
  const holderId = `${idPrefix}-account-holder`;

  return (
    <fieldset className="bank-fieldset">
      <legend>פרטי בנק</legend>

      {fieldErrors.partialBank && (
        <div className="error" role="alert" id={`${idPrefix}-partial-bank-error`}>
          {fieldErrors.partialBank}
        </div>
      )}

      <div className="bank-sync-row">
        <div className="bank-sync-field">
          <label htmlFor={bankNumberId}>מספר בנק</label>
          <ValidatedControl error={fieldErrors.bankNumber} errorId={`${bankNumberId}-error`}>
            <input
              id={bankNumberId}
              type="text"
              value={values.bankNumber}
              onChange={(e) => handleBankNumberChange(e.target.value)}
              onBlur={() => onBlurField?.('bankNumber')}
              disabled={disabled}
              inputMode="numeric"
              maxLength={3}
              aria-invalid={fieldErrors.bankNumber ? true : undefined}
              aria-describedby={fieldErrors.bankNumber ? `${bankNumberId}-error` : undefined}
            />
          </ValidatedControl>
          {syncHint === 'name' && (
            <span className="bank-sync-hint">ⓘ הוזן לפי שם בנק</span>
          )}
        </div>

        <span className="bank-sync-icon" aria-hidden="true">↔</span>

        <div className="bank-sync-field">
          <label htmlFor={bankNameId}>שם הבנק</label>
          <ValidatedControl error={fieldErrors.bankName} errorId={`${bankNameId}-error`}>
            <input
              id={bankNameId}
              type="text"
              list={`${idPrefix}-bank-name-list`}
              value={selectedBankName}
              onChange={(e) => handleBankNameChange(e.target.value)}
              onBlur={() => onBlurField?.('bankName')}
              disabled={disabled}
              placeholder="בחרו או הקלידו שם בנק"
              aria-invalid={fieldErrors.bankName ? true : undefined}
              aria-describedby={fieldErrors.bankName ? `${bankNameId}-error` : undefined}
            />
            <datalist id={`${idPrefix}-bank-name-list`}>
              {ISRAELI_BANKS.map((bank) => (
                <option key={bank.number} value={bank.name} />
              ))}
            </datalist>
          </ValidatedControl>
          {syncHint === 'number' && (
            <span className="bank-sync-hint">ⓘ הוזן לפי מספר בנק</span>
          )}
        </div>
      </div>

      <label htmlFor={branchId}>מספר סניף</label>
      <ValidatedControl error={fieldErrors.branchNumber} errorId={`${branchId}-error`}>
        <input
          id={branchId}
          type="text"
          value={values.branchNumber}
          onChange={(e) => onChange({ branchNumber: e.target.value.replace(/\D/g, '').slice(0, 5) })}
          onBlur={() => onBlurField?.('branchNumber')}
          disabled={disabled}
          inputMode="numeric"
          maxLength={5}
          aria-invalid={fieldErrors.branchNumber ? true : undefined}
          aria-describedby={fieldErrors.branchNumber ? `${branchId}-error` : undefined}
        />
      </ValidatedControl>

      <label htmlFor={accountId}>מספר חשבון</label>
      <ValidatedControl error={fieldErrors.accountNumber} errorId={`${accountId}-error`}>
        <input
          id={accountId}
          type="text"
          value={values.accountNumber}
          onChange={(e) => onChange({ accountNumber: e.target.value.replace(/\D/g, '').slice(0, 20) })}
          onBlur={() => onBlurField?.('accountNumber')}
          disabled={disabled}
          inputMode="numeric"
          maxLength={20}
          aria-invalid={fieldErrors.accountNumber ? true : undefined}
          aria-describedby={fieldErrors.accountNumber ? `${accountId}-error` : undefined}
        />
      </ValidatedControl>

      <label htmlFor={holderId}>שם בעל החשבון</label>
      <ValidatedControl error={fieldErrors.accountHolderName} errorId={`${holderId}-error`}>
        <input
          id={holderId}
          type="text"
          value={values.accountHolderName}
          onChange={(e) => handleAccountHolderChange(e.target.value)}
          onBlur={() => onBlurField?.('accountHolderName')}
          disabled={disabled}
          maxLength={200}
          aria-invalid={fieldErrors.accountHolderName ? true : undefined}
          aria-describedby={fieldErrors.accountHolderName ? `${holderId}-error` : undefined}
        />
      </ValidatedControl>
      {!fieldErrors.accountHolderName && (
        <p className="bank-field-hint">{accountHolderHint}</p>
      )}
    </fieldset>
  );
}

/** Resolves synced bank display name for validation. */
export function resolveBankDisplayName(bankNumber: string, bankNameInput: string): string {
  const fromNumber = findBankByNumber(bankNumber)?.name;
  if (fromNumber) return fromNumber;
  return bankNameInput.trim();
}
