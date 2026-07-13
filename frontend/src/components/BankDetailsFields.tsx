import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import {
  filterBanks,
  findBankByName,
  findBankByNumber,
  formatBankOption,
  ISRAELI_BANKS,
  type IsraeliBank,
} from '../data/israeliBanks';
import { buildSuggestedAccountHolderName } from '../utils/accountHolderName';
import type { BankFieldErrors } from '../validation/bankFields';
import { ValidatedControl } from './FieldValidation';

export interface BankDetailsValues {
  bankNumber: string;
  bankName: string;
  branchNumber: string;
  accountNumber: string;
  accountHolderName: string;
}

interface SuggestedAccountHolderParts {
  familyLastName: string;
  fatherName: string;
  motherName: string;
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
  layoutVariant?: 'default' | 'create-family';
  strictBankSelection?: boolean;
  /** Single BankSelect (number+name search), same as committee transfer popover. */
  unifiedBankSearch?: boolean;
  accountHolderSuggestOnFocus?: boolean;
  suggestedAccountHolderParts?: SuggestedAccountHolderParts;
}

export type BankSelectDisplayMode = 'name' | 'number-name';

export interface BankSelectProps {
  id: string;
  listId: string;
  value: string;
  displayMode?: BankSelectDisplayMode;
  disabled: boolean;
  error?: string | null;
  errorId: string;
  onSelect: (bank: IsraeliBank) => void;
  onClear: () => void;
  onBlur?: () => void;
  placeholder?: string;
}

function displayValueForBank(value: string, displayMode: BankSelectDisplayMode): string {
  if (displayMode === 'name') return value;
  const bank = findBankByNumber(value);
  return bank ? formatBankOption(bank) : '';
}

function resolveBankFromQuery(query: string, displayMode: BankSelectDisplayMode): IsraeliBank | undefined {
  const trimmed = query.trim();
  if (!trimmed) return undefined;
  if (displayMode === 'name') {
    return findBankByName(trimmed);
  }
  const byNumber = findBankByNumber(trimmed);
  if (byNumber) return byNumber;
  const byName = findBankByName(trimmed);
  if (byName) return byName;
  return ISRAELI_BANKS.find((bank) => formatBankOption(bank) === trimmed);
}

function shouldAutoCommitQuery(query: string, bank: IsraeliBank, displayMode: BankSelectDisplayMode): boolean {
  if (displayMode === 'name') return true;
  const trimmed = query.trim();
  return trimmed === bank.number || formatBankOption(bank) === trimmed;
}

export function BankSelect({
  id,
  listId,
  value,
  displayMode = 'name',
  disabled,
  error,
  errorId,
  onSelect,
  onClear,
  onBlur,
  placeholder = 'בחרו מהרשימה',
}: BankSelectProps) {
  const [query, setQuery] = useState(() => displayValueForBank(value, displayMode));
  const [open, setOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  const queryActiveRef = useRef(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!queryActiveRef.current) {
      setQuery(displayValueForBank(value, displayMode));
    }
  }, [value, displayMode]);

  const filteredBanks = useMemo(() => filterBanks(query), [query]);
  const options = filteredBanks.length > 0 ? filteredBanks : ISRAELI_BANKS;

  function optionLabel(bank: IsraeliBank): string {
    return displayMode === 'name' ? bank.name : formatBankOption(bank);
  }

  function commitSelection(bank: IsraeliBank) {
    queryActiveRef.current = false;
    setQuery(optionLabel(bank));
    setOpen(false);
    onSelect(bank);
  }

  function handleInputChange(nextQuery: string) {
    queryActiveRef.current = true;
    setQuery(nextQuery);
    setOpen(true);
    setHighlightedIndex(0);

    const exact = resolveBankFromQuery(nextQuery, displayMode);
    if (exact && shouldAutoCommitQuery(nextQuery, exact, displayMode)) {
      commitSelection(exact);
    }
  }

  function handleBlur() {
    queryActiveRef.current = false;
    setOpen(false);

    const trimmed = query.trim();
    if (trimmed.length === 0) {
      setQuery('');
      onClear();
      onBlur?.();
      return;
    }

    const bank = resolveBankFromQuery(trimmed, displayMode);
    if (bank) {
      commitSelection(bank);
    } else {
      setQuery(displayValueForBank(value, displayMode));
    }
    onBlur?.();
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!open && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      setOpen(true);
      return;
    }
    if (!open) return;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlightedIndex((prev) => Math.min(prev + 1, options.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightedIndex((prev) => Math.max(prev - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const bank = options[highlightedIndex];
      if (bank) commitSelection(bank);
    } else if (e.key === 'Escape') {
      setOpen(false);
      setQuery(displayValueForBank(value, displayMode));
      queryActiveRef.current = false;
    }
  }

  return (
    <div className="bank-combobox" ref={rootRef}>
      <ValidatedControl error={error} errorId={errorId}>
        <input
          id={id}
          type="text"
          className="bank-combobox-input"
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          value={query}
          onChange={(e) => handleInputChange(e.target.value)}
          onFocus={() => setOpen(true)}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          placeholder={placeholder}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
        />
      </ValidatedControl>
      {open && !disabled && options.length > 0 && (
        <ul id={listId} className="bank-combobox-list" role="listbox">
          {options.map((bank, index) => (
            <li
              key={bank.number}
              role="option"
              aria-selected={index === highlightedIndex}
              className={[
                'bank-combobox-option',
                index === highlightedIndex ? 'bank-combobox-option--highlighted' : '',
              ].filter(Boolean).join(' ')}
              onMouseDown={(e) => {
                e.preventDefault();
                commitSelection(bank);
              }}
            >
              {optionLabel(bank)}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function BankNameCombobox(props: Omit<BankSelectProps, 'displayMode' | 'placeholder'>) {
  return <BankSelect {...props} displayMode="name" placeholder="בחרו מהרשימה" />;
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
  layoutVariant = 'default',
  strictBankSelection = false,
  unifiedBankSearch = false,
  accountHolderSuggestOnFocus = false,
  suggestedAccountHolderParts,
}: BankDetailsFieldsProps) {
  const [syncHint, setSyncHint] = useState<'number' | 'name' | null>(null);
  const holderEditedRef = useRef(false);
  const holderSuggestAppliedRef = useRef(false);
  const previousDefaultHolderRef = useRef(defaultAccountHolderName.trim());
  const bankListId = useId();

  useEffect(() => {
    if (accountHolderSuggestOnFocus) return;
    if (values.accountHolderName.trim() !== defaultAccountHolderName.trim()
      && values.accountHolderName.trim().length > 0) {
      holderEditedRef.current = true;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (accountHolderSuggestOnFocus) return;

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
  }, [accountHolderSuggestOnFocus, defaultAccountHolderName, values.accountHolderName, onChange]);

  function selectBank(bank: IsraeliBank, source: 'number' | 'name') {
    onChange({ bankNumber: bank.number, bankName: bank.name });
    setSyncHint(source);
  }

  function handleBankNumberChange(raw: string) {
    const digits = raw.replace(/\D/g, '').slice(0, 3);
    const bank = findBankByNumber(digits);
    if (bank) {
      onChange({ bankNumber: digits, bankName: bank.name });
      setSyncHint('number');
    } else {
      onChange({ bankNumber: digits, bankName: '' });
      setSyncHint(null);
    }
  }

  function handleBankNameChange(name: string) {
    const bank = findBankByName(name);
    if (bank) {
      selectBank(bank, 'name');
    } else {
      onChange({ bankName: name });
      setSyncHint(null);
    }
  }

  function handleAccountHolderChange(value: string) {
    holderEditedRef.current = true;
    onChange({ accountHolderName: value });
  }

  function handleAccountHolderFocus() {
    if (!accountHolderSuggestOnFocus || holderSuggestAppliedRef.current) return;
    if (values.accountHolderName.trim().length > 0) return;

    const parts = suggestedAccountHolderParts;
    if (!parts) return;

    const suggested = buildSuggestedAccountHolderName(
      parts.familyLastName,
      parts.fatherName,
      parts.motherName,
    );
    if (suggested.length === 0) return;

    onChange({ accountHolderName: suggested });
    holderSuggestAppliedRef.current = true;
  }

  const bankNumberId = `${idPrefix}-bank-number`;
  const bankNameId = `${idPrefix}-bank-name`;
  const branchId = `${idPrefix}-branch-number`;
  const accountId = `${idPrefix}-account-number`;
  const holderId = `${idPrefix}-account-holder`;
  const isCreateLayout = layoutVariant === 'create-family';
  const fieldsetClass = isCreateLayout
    ? 'bank-fieldset bank-fieldset--create-layout'
    : 'bank-fieldset';

  return (
    <fieldset className={fieldsetClass}>
      <legend>פרטי בנק</legend>

      {fieldErrors.partialBank && (
        <div className="error" role="alert" id={`${idPrefix}-partial-bank-error`}>
          {fieldErrors.partialBank}
        </div>
      )}

      <div className={isCreateLayout ? 'bank-create-row bank-create-row--primary' : 'bank-sync-row'}>
        {unifiedBankSearch ? (
          <div className="bank-sync-field bank-field--unified">
            <label htmlFor={bankNumberId}>בנק</label>
            <BankSelect
              id={bankNumberId}
              listId={bankListId}
              value={values.bankNumber}
              displayMode="number-name"
              disabled={disabled}
              error={fieldErrors.bankNumber ?? fieldErrors.bankName}
              errorId={`${bankNumberId}-error`}
              placeholder="חיפוש לפי מספר או שם"
              onSelect={(bank) => selectBank(bank, 'number')}
              onClear={() => {
                onChange({ bankNumber: '', bankName: '' });
                setSyncHint(null);
              }}
              onBlur={() => onBlurField?.('bankNumber')}
            />
          </div>
        ) : (
          <>
            <div className="bank-sync-field bank-field--number">
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

            {!isCreateLayout && <span className="bank-sync-icon" aria-hidden="true">↔</span>}

            <div className="bank-sync-field bank-field--name">
              <label htmlFor={bankNameId}>שם הבנק</label>
              {strictBankSelection ? (
                <BankNameCombobox
                  id={bankNameId}
                  listId={bankListId}
                  value={values.bankName}
                  disabled={disabled}
                  error={fieldErrors.bankName}
                  errorId={`${bankNameId}-error`}
                  onSelect={(bank) => selectBank(bank, 'name')}
                  onClear={() => {
                    onChange({ bankName: '', bankNumber: '' });
                    setSyncHint(null);
                  }}
                  onBlur={() => onBlurField?.('bankName')}
                />
              ) : (
                <ValidatedControl error={fieldErrors.bankName} errorId={`${bankNameId}-error`}>
                  <input
                    id={bankNameId}
                    type="text"
                    list={`${idPrefix}-bank-name-list`}
                    value={values.bankName}
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
              )}
              {syncHint === 'number' && (
                <span className="bank-sync-hint">ⓘ הוזן לפי מספר בנק</span>
              )}
            </div>
          </>
        )}
      </div>

      <div className={isCreateLayout ? 'bank-create-row bank-create-row--branch' : undefined}>
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
      </div>

      <div className={isCreateLayout ? 'bank-create-row bank-create-row--account' : undefined}>
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
      </div>

      <div className={isCreateLayout ? 'bank-create-row bank-create-row--holder' : undefined}>
        <label htmlFor={holderId}>שם בעל החשבון</label>
        <ValidatedControl error={fieldErrors.accountHolderName} errorId={`${holderId}-error`}>
          <input
            id={holderId}
            type="text"
            value={values.accountHolderName}
            onChange={(e) => handleAccountHolderChange(e.target.value)}
            onFocus={handleAccountHolderFocus}
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
      </div>
    </fieldset>
  );
}

/** Resolves synced bank display name for validation. */
export function resolveBankDisplayName(bankNumber: string, bankNameInput: string): string {
  const trimmedName = bankNameInput.trim();
  if (trimmedName.length > 0) return trimmedName;
  return findBankByNumber(bankNumber)?.name ?? '';
}
