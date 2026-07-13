import { ValidatedControl } from './FieldValidation';

interface PhoneInputGroupProps {
  idPrefix: string;
  prefix: string;
  number: string;
  disabled?: boolean;
  prefixError?: string | null;
  numberError?: string | null;
  onPrefixChange: (value: string) => void;
  onNumberChange: (value: string) => void;
  onPrefixBlur?: () => void;
  onNumberBlur?: () => void;
}

/** Compact Israeli phone: prefix first, then number ([054] - [1234567]). */
export function PhoneInputGroup({
  idPrefix,
  prefix,
  number,
  disabled = false,
  prefixError,
  numberError,
  onPrefixChange,
  onNumberChange,
  onPrefixBlur,
  onNumberBlur,
}: PhoneInputGroupProps) {
  const prefixId = `${idPrefix}-prefix`;
  const numberId = `${idPrefix}-number`;

  return (
    <div className="phone-input-group" dir="ltr">
      <ValidatedControl
        error={prefixError}
        errorId={`${prefixId}-error`}
        className="phone-input-field"
      >
        <input
          id={prefixId}
          type="text"
          className="phone-input-prefix"
          value={prefix}
          onChange={(e) => onPrefixChange(e.target.value.replace(/\D/g, '').slice(0, 3))}
          onBlur={onPrefixBlur}
          disabled={disabled}
          inputMode="numeric"
          maxLength={3}
          aria-label="קידומת"
          aria-invalid={prefixError ? true : undefined}
        />
      </ValidatedControl>
      <span className="phone-input-separator" aria-hidden="true">-</span>
      <ValidatedControl
        error={numberError}
        errorId={`${numberId}-error`}
        className="phone-input-field"
      >
        <input
          id={numberId}
          type="tel"
          className="phone-input-number"
          value={number}
          onChange={(e) => onNumberChange(e.target.value.replace(/\D/g, '').slice(0, 7))}
          onBlur={onNumberBlur}
          disabled={disabled}
          inputMode="numeric"
          maxLength={7}
          aria-label="מספר טלפון"
          aria-invalid={numberError ? true : undefined}
        />
      </ValidatedControl>
    </div>
  );
}

export function joinPhoneValue(prefix: string, number: string): string {
  const p = prefix.trim();
  const n = number.trim();
  if (p.length === 0 && n.length === 0) return '';
  if (n.length === 0) return p;
  if (p.length === 0) return n;
  return `${p}-${n}`;
}
