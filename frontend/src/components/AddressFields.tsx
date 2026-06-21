import { useMemo } from 'react';
import {
  filterLocalities,
  filterStreets,
  isKnownLocality,
  isKnownStreet,
  localityNames,
} from '../data/israeliAddressRegistry';
import type { StructuredAddress } from '../validation/familyAddress';
import { FormField } from './ModalShell';

export interface AddressFieldErrors {
  city?: string | null;
  street?: string | null;
}

interface AddressFieldsProps {
  idPrefix: string;
  values: StructuredAddress;
  disabled?: boolean;
  errors?: AddressFieldErrors;
  onChange: (patch: Partial<StructuredAddress>) => void;
  onBlurField?: (field: keyof StructuredAddress) => void;
  onErrorsChange?: (errors: AddressFieldErrors) => void;
}

export function validateAddressFields(
  values: StructuredAddress,
): AddressFieldErrors {
  const errors: AddressFieldErrors = {};
  const hasAny = Object.values(values).some((v) => v.trim().length > 0);
  if (!hasAny) return errors;

  const city = values.city.trim();
  const street = values.street.trim();

  if (city.length > 0 && !isKnownLocality(city)) {
    errors.city = 'יש לבחור יישוב מהרשימה';
  }
  if (street.length > 0 && city.length > 0 && !isKnownStreet(city, street)) {
    errors.street = 'יש לבחור רחוב מהרשימה';
  }
  if (street.length > 0 && city.length === 0) {
    errors.city = 'יש לבחור יישוב לפני רחוב';
  }

  return errors;
}

export function AddressFields({
  idPrefix,
  values,
  disabled = false,
  errors = {},
  onChange,
  onBlurField,
  onErrorsChange,
}: AddressFieldsProps) {
  const cityListId = `${idPrefix}-city-list`;
  const streetListId = `${idPrefix}-street-list`;

  const cityOptions = useMemo(() => {
    const filtered = filterLocalities(values.city);
    return filtered.length > 0 ? filtered : localityNames().slice(0, 12);
  }, [values.city]);

  const streetOptions = useMemo(() => {
    if (!values.city.trim()) return [];
    return filterStreets(values.city, values.street);
  }, [values.city, values.street]);

  function handleCityChange(city: string) {
    onChange({ city });
    if (values.street && !isKnownStreet(city, values.street)) {
      onChange({ street: '' });
    }
  }

  return (
    <fieldset className="address-fieldset">
      <legend>כתובת (אופציונלי)</legend>

      <div className="form-grid-2">
        <FormField
          id={`${idPrefix}-city`}
          label="יישוב / עיר"
          error={errors.city}
        >
          <input
            id={`${idPrefix}-city`}
            type="text"
            list={cityListId}
            value={values.city}
            onChange={(e) => handleCityChange(e.target.value)}
            onBlur={() => {
              onBlurField?.('city');
              onErrorsChange?.(validateAddressFields(values));
            }}
            disabled={disabled}
            placeholder="בחרו מהרשימה"
            aria-invalid={errors.city ? true : undefined}
            aria-describedby={errors.city ? `${idPrefix}-city-error` : undefined}
          />
          <datalist id={cityListId}>
            {cityOptions.map((name) => (
              <option key={name} value={name} />
            ))}
          </datalist>
        </FormField>

        <FormField
          id={`${idPrefix}-street`}
          label="רחוב"
          error={errors.street}
        >
          <input
            id={`${idPrefix}-street`}
            type="text"
            list={streetListId}
            value={values.street}
            onChange={(e) => onChange({ street: e.target.value })}
            onBlur={() => {
              onBlurField?.('street');
              onErrorsChange?.(validateAddressFields(values));
            }}
            disabled={disabled || !values.city.trim()}
            placeholder={values.city.trim() ? 'בחרו מהרשימה' : 'בחרו יישוב תחילה'}
            aria-invalid={errors.street ? true : undefined}
            aria-describedby={errors.street ? `${idPrefix}-street-error` : undefined}
          />
          <datalist id={streetListId}>
            {streetOptions.map((name) => (
              <option key={name} value={name} />
            ))}
          </datalist>
        </FormField>
      </div>

      <div className="form-grid-4">
        <FormField id={`${idPrefix}-house`} label="מספר בית">
          <input
            id={`${idPrefix}-house`}
            type="text"
            value={values.houseNumber}
            onChange={(e) => onChange({ houseNumber: e.target.value.slice(0, 10) })}
            disabled={disabled}
            inputMode="numeric"
          />
        </FormField>

        <FormField id={`${idPrefix}-apartment`} label="דירה">
          <input
            id={`${idPrefix}-apartment`}
            type="text"
            value={values.apartment}
            onChange={(e) => onChange({ apartment: e.target.value.slice(0, 10) })}
            disabled={disabled}
          />
        </FormField>

        <FormField id={`${idPrefix}-entrance`} label="כניסה">
          <input
            id={`${idPrefix}-entrance`}
            type="text"
            value={values.entrance}
            onChange={(e) => onChange({ entrance: e.target.value.slice(0, 10) })}
            disabled={disabled}
          />
        </FormField>

        <FormField id={`${idPrefix}-floor`} label="קומה">
          <input
            id={`${idPrefix}-floor`}
            type="text"
            value={values.floor}
            onChange={(e) => onChange({ floor: e.target.value.slice(0, 10) })}
            disabled={disabled}
          />
        </FormField>
      </div>
    </fieldset>
  );
}
