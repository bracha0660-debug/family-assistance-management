import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import type { UserDto } from '../api/auth';
import {
  createFamily,
  getSuggestedAccountingCode,
  type CreateFamilyPayload,
  type FamilyDto,
} from '../api/families';
import { listOrgUsers, type OrgUserDto } from '../api/orgUsers';
import { AddressFields, validateAddressFields, type AddressFieldErrors } from './AddressFields';
import { BankDetailsFields, type BankDetailsValues } from './BankDetailsFields';
import { FormField, ModalShell } from './ModalShell';
import { joinPhoneValue, PhoneInputGroup } from './PhoneInputGroup';
import { usesMyRecordsFamilyScope } from '../hooks/usePermissions';
import { findBankByNumber } from '../data/israeliBanks';
import { focusFirstInvalidField } from '../utils/formValidation';
import type { BankFieldErrors } from '../validation/bankFields';
import { isBankAllEmpty, validateBankFieldErrors } from '../validation/bankFields';
import {
  EMPTY_STRUCTURED_ADDRESS,
  formatFamilyAddress,
  type StructuredAddress,
} from '../validation/familyAddress';
import { isValidIsraeliId } from '../validation/israeliId';
import {
  hasPhoneErrors,
  validateOptionalPhoneParts,
  type PhoneFieldErrors,
} from '../validation/israeliPhone';

interface CreateFamilyModalProps {
  user: UserDto;
  onClose: () => void;
  onCreated: (created: FamilyDto) => void;
}

const ID_PREFIX = 'new-family';

function validateOptionalId(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  if (!isValidIsraeliId(trimmed)) return 'מספר תעודת זהות אינו תקין';
  return null;
}

const FOCUS_FIELD_ORDER = [
  `${ID_PREFIX}-family-last-name`,
  `${ID_PREFIX}-father-id`,
  `${ID_PREFIX}-father-phone-prefix`,
  `${ID_PREFIX}-father-phone-number`,
  `${ID_PREFIX}-mother-id`,
  `${ID_PREFIX}-mother-phone-prefix`,
  `${ID_PREFIX}-mother-phone-number`,
  `${ID_PREFIX}-city`,
  `${ID_PREFIX}-street`,
  `${ID_PREFIX}-bank-number`,
  `${ID_PREFIX}-bank-name`,
  `${ID_PREFIX}-branch-number`,
  `${ID_PREFIX}-account-number`,
  `${ID_PREFIX}-account-holder`,
  `${ID_PREFIX}-accounting-code`,
];

export function CreateFamilyModal({ user, onClose, onCreated }: CreateFamilyModalProps) {
  const showCoordinatorSelect = !usesMyRecordsFamilyScope(user);
  const [coordinators, setCoordinators] = useState<OrgUserDto[]>([]);
  const [coordinatorId, setCoordinatorId] = useState(user.id);
  const [accountingCode, setAccountingCode] = useState('');
  const [familyLastName, setFamilyLastName] = useState('');
  const [fatherName, setFatherName] = useState('');
  const [fatherIsraeliId, setFatherIsraeliId] = useState('');
  const [motherName, setMotherName] = useState('');
  const [motherIsraeliId, setMotherIsraeliId] = useState('');
  const [fatherPhonePrefix, setFatherPhonePrefix] = useState('');
  const [fatherPhoneNumber, setFatherPhoneNumber] = useState('');
  const [motherPhonePrefix, setMotherPhonePrefix] = useState('');
  const [motherPhoneNumber, setMotherPhoneNumber] = useState('');
  const [structuredAddress, setStructuredAddress] = useState<StructuredAddress>({ ...EMPTY_STRUCTURED_ADDRESS });
  const [bankDetails, setBankDetails] = useState<BankDetailsValues>({
    bankNumber: '',
    bankName: '',
    branchNumber: '',
    accountNumber: '',
    accountHolderName: '',
  });
  const [lastNameError, setLastNameError] = useState<string | null>(null);
  const [fatherIdError, setFatherIdError] = useState<string | null>(null);
  const [motherIdError, setMotherIdError] = useState<string | null>(null);
  const [fatherPhoneErrors, setFatherPhoneErrors] = useState<PhoneFieldErrors>({});
  const [motherPhoneErrors, setMotherPhoneErrors] = useState<PhoneFieldErrors>({});
  const [accountingError, setAccountingError] = useState<string | null>(null);
  const [addressErrors, setAddressErrors] = useState<AddressFieldErrors>({});
  const [bankErrors, setBankErrors] = useState<BankFieldErrors>({});
  const [formError, setFormError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleBankChange = useCallback((patch: Partial<BankDetailsValues>) => {
    setBankDetails((prev) => ({ ...prev, ...patch }));
  }, []);

  const handleAddressChange = useCallback((patch: Partial<StructuredAddress>) => {
    setStructuredAddress((prev) => ({ ...prev, ...patch }));
  }, []);

  const loadSuggestedCode = useCallback(async (coordId: string) => {
    try {
      const result = await getSuggestedAccountingCode(coordId);
      setAccountingCode(String(result.suggestedAccountingCode));
    } catch {
      // suggestion is optional UX
    }
  }, []);

  useEffect(() => {
    if (showCoordinatorSelect) {
      listOrgUsers()
        .then((res) => setCoordinators(res.users.filter((u) => u.status === 'active')))
        .catch(() => {});
    }
    loadSuggestedCode(coordinatorId);
  }, [showCoordinatorSelect, coordinatorId, loadSuggestedCode]);

  function handleCoordinatorChange(id: string) {
    setCoordinatorId(id);
    loadSuggestedCode(id);
  }

  function resolveBankName(): string {
    return bankDetails.bankName.trim() || findBankByNumber(bankDetails.bankNumber)?.name || '';
  }

  function validateBank(showAll: boolean, field?: keyof BankFieldErrors) {
    const errors = validateBankFieldErrors(
      bankDetails.bankNumber,
      bankDetails.branchNumber,
      bankDetails.accountNumber,
      bankDetails.accountHolderName,
      resolveBankName(),
    );
    if (field && !showAll) {
      setBankErrors((prev) => ({
        ...prev,
        [field]: errors[field] ?? null,
      }));
      return errors;
    }
    setBankErrors(errors);
    return errors;
  }

  function validateFatherPhone(showAll = true): PhoneFieldErrors {
    const errors = validateOptionalPhoneParts(fatherPhonePrefix, fatherPhoneNumber);
    if (showAll) setFatherPhoneErrors(errors);
    return errors;
  }

  function validateMotherPhone(showAll = true): PhoneFieldErrors {
    const errors = validateOptionalPhoneParts(motherPhonePrefix, motherPhoneNumber);
    if (showAll) setMotherPhoneErrors(errors);
    return errors;
  }

  function validateAll(): boolean {
    setFormError('');
    let valid = true;

    const trimmedLastName = familyLastName.trim();
    const lnErr = trimmedLastName.length === 0 || trimmedLastName.length < 2
      ? 'שם משפחה הוא שדה חובה'
      : null;
    setLastNameError(lnErr);
    if (lnErr) valid = false;

    const fErr = validateOptionalId(fatherIsraeliId);
    setFatherIdError(fErr);
    if (fErr) valid = false;

    const mErr = validateOptionalId(motherIsraeliId);
    setMotherIdError(mErr);
    if (mErr) valid = false;

    const fatherPhoneErrs = validateFatherPhone(true);
    if (hasPhoneErrors(fatherPhoneErrs)) valid = false;

    const motherPhoneErrs = validateMotherPhone(true);
    if (hasPhoneErrors(motherPhoneErrs)) valid = false;

    const addrErrs = validateAddressFields(structuredAddress);
    setAddressErrors(addrErrs);
    if (addrErrs.city || addrErrs.street) valid = false;

    const bErrs = validateBank(true);
    if (Object.values(bErrs).some(Boolean)) valid = false;

    let acctErr: string | null = null;
    if (accountingCode.trim().length > 0) {
      const parsed = Number(accountingCode.trim());
      if (!Number.isInteger(parsed) || parsed <= 0) {
        acctErr = 'מספר חשבונאי חייב להיות מספר חיובי';
        valid = false;
      }
    }
    setAccountingError(acctErr);

    if (!valid) {
      focusFirstInvalidField(FOCUS_FIELD_ORDER);
    }
    return valid;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!validateAll()) return;

    const trimmedLastName = familyLastName.trim();
    let parsedAccounting: number | null = null;
    if (accountingCode.trim().length > 0) {
      parsedAccounting = Number(accountingCode.trim());
    }

    setLoading(true);
    try {
      const payload: CreateFamilyPayload = {
        familyLastName: trimmedLastName,
        accountingCode: parsedAccounting,
        assignedCoordinatorId: showCoordinatorSelect ? coordinatorId : undefined,
        fatherName: fatherName.trim().length > 0 ? fatherName.trim() : null,
        fatherIsraeliId: fatherIsraeliId.trim().length > 0 ? fatherIsraeliId.trim() : null,
        motherName: motherName.trim().length > 0 ? motherName.trim() : null,
        motherIsraeliId: motherIsraeliId.trim().length > 0 ? motherIsraeliId.trim() : null,
        phone: joinPhoneValue(fatherPhonePrefix, fatherPhoneNumber).length > 0
          ? joinPhoneValue(fatherPhonePrefix, fatherPhoneNumber)
          : null,
        address: formatFamilyAddress(structuredAddress),
      };
      if (!isBankAllEmpty(
        bankDetails.bankNumber,
        bankDetails.branchNumber,
        bankDetails.accountNumber,
        bankDetails.accountHolderName,
        resolveBankName(),
      )) {
        payload.bankNumber = bankDetails.bankNumber.trim();
        payload.branchNumber = bankDetails.branchNumber.trim();
        payload.accountNumber = bankDetails.accountNumber.trim();
        payload.accountHolderName = bankDetails.accountHolderName.trim();
      }
      const created = await createFamily(payload);
      onCreated(created);
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  function handleFatherPhonePrefixChange(value: string) {
    setFatherPhonePrefix(value);
    if (hasPhoneErrors(fatherPhoneErrors)) {
      setFatherPhoneErrors(validateOptionalPhoneParts(value, fatherPhoneNumber));
    }
  }

  function handleFatherPhoneNumberChange(value: string) {
    setFatherPhoneNumber(value);
    if (hasPhoneErrors(fatherPhoneErrors)) {
      setFatherPhoneErrors(validateOptionalPhoneParts(fatherPhonePrefix, value));
    }
  }

  function handleMotherPhonePrefixChange(value: string) {
    setMotherPhonePrefix(value);
    if (hasPhoneErrors(motherPhoneErrors)) {
      setMotherPhoneErrors(validateOptionalPhoneParts(value, motherPhoneNumber));
    }
  }

  function handleMotherPhoneNumberChange(value: string) {
    setMotherPhoneNumber(value);
    if (hasPhoneErrors(motherPhoneErrors)) {
      setMotherPhoneErrors(validateOptionalPhoneParts(motherPhonePrefix, value));
    }
  }

  return (
    <ModalShell
      title="יצירת משפחה חדשה"
      wide
      bodyClassName="create-family-modal-body"
      loading={loading}
      onClose={onClose}
      onSubmit={handleSubmit}
      formError={formError}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={() => onClose()} disabled={loading}>
            ביטול
          </button>
          <button type="submit" disabled={loading}>
            {loading ? 'יוצר...' : 'צור משפחה'}
          </button>
        </>
      )}
    >
      <FormField id={`${ID_PREFIX}-code`} label="קוד משפחה">
        <input id={`${ID_PREFIX}-code`} type="text" value="יוקצה אוטומטית" disabled readOnly />
      </FormField>

      {showCoordinatorSelect && (
        <FormField id={`${ID_PREFIX}-coordinator`} label={<>רכז/ת <span className="field-required">*</span></>}>
          <select
            id={`${ID_PREFIX}-coordinator`}
            value={coordinatorId}
            onChange={(e) => handleCoordinatorChange(e.target.value)}
            disabled={loading}
          >
            {coordinators.map((c) => (
              <option key={c.id} value={c.id}>{c.fullName}</option>
            ))}
          </select>
        </FormField>
      )}

      <div className="family-details-grid">
        <FormField
          id={`${ID_PREFIX}-accounting-code`}
          label="מספר חשבונאי"
          className="family-details-grid__col-name"
          error={accountingError}
          helperText="ניתן לערוך במידת הצורך"
        >
          <input
            id={`${ID_PREFIX}-accounting-code`}
            type="number"
            min={1}
            step={1}
            value={accountingCode}
            onChange={(e) => {
              setAccountingCode(e.target.value);
              if (accountingError) setAccountingError(null);
            }}
            disabled={loading}
            inputMode="numeric"
            placeholder="מוצע לפי הרכז"
            aria-invalid={accountingError ? true : undefined}
          />
        </FormField>

        <FormField
          id={`${ID_PREFIX}-family-last-name`}
          label={<>שם משפחה <span className="field-required">*</span></>}
          className="family-details-grid__col-phone"
          error={lastNameError}
        >
          <input
            id={`${ID_PREFIX}-family-last-name`}
            type="text"
            value={familyLastName}
            onChange={(e) => {
              setFamilyLastName(e.target.value);
              if (lastNameError) setLastNameError(null);
            }}
            disabled={loading}
            maxLength={200}
            aria-invalid={lastNameError ? true : undefined}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-father-name`} label="שם האב" className="family-details-grid__col-name">
          <input
            id={`${ID_PREFIX}-father-name`}
            type="text"
            value={fatherName}
            onChange={(e) => setFatherName(e.target.value)}
            disabled={loading}
            maxLength={200}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-father-id`} label="ת.ז. האב" className="family-details-grid__col-id" error={fatherIdError}>
          <input
            id={`${ID_PREFIX}-father-id`}
            type="text"
            value={fatherIsraeliId}
            onChange={(e) => {
              setFatherIsraeliId(e.target.value);
              if (fatherIdError) setFatherIdError(validateOptionalId(e.target.value));
            }}
            onBlur={() => setFatherIdError(validateOptionalId(fatherIsraeliId))}
            disabled={loading}
            inputMode="numeric"
            maxLength={9}
            placeholder="9 ספרות"
            aria-invalid={fatherIdError ? true : undefined}
          />
        </FormField>

        <FormField
          id={`${ID_PREFIX}-father-phone-number`}
          label="טלפון האב"
          className="family-details-grid__col-phone form-field--phone"
        >
          <PhoneInputGroup
            idPrefix={`${ID_PREFIX}-father-phone`}
            prefix={fatherPhonePrefix}
            number={fatherPhoneNumber}
            disabled={loading}
            prefixError={fatherPhoneErrors.prefix}
            numberError={fatherPhoneErrors.number}
            onPrefixChange={handleFatherPhonePrefixChange}
            onNumberChange={handleFatherPhoneNumberChange}
            onPrefixBlur={() => setFatherPhoneErrors(validateOptionalPhoneParts(fatherPhonePrefix, fatherPhoneNumber))}
            onNumberBlur={() => setFatherPhoneErrors(validateOptionalPhoneParts(fatherPhonePrefix, fatherPhoneNumber))}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-mother-name`} label="שם האם" className="family-details-grid__col-name">
          <input
            id={`${ID_PREFIX}-mother-name`}
            type="text"
            value={motherName}
            onChange={(e) => setMotherName(e.target.value)}
            disabled={loading}
            maxLength={200}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-mother-id`} label="ת.ז. האם" className="family-details-grid__col-id" error={motherIdError}>
          <input
            id={`${ID_PREFIX}-mother-id`}
            type="text"
            value={motherIsraeliId}
            onChange={(e) => {
              setMotherIsraeliId(e.target.value);
              if (motherIdError) setMotherIdError(validateOptionalId(e.target.value));
            }}
            onBlur={() => setMotherIdError(validateOptionalId(motherIsraeliId))}
            disabled={loading}
            inputMode="numeric"
            maxLength={9}
            placeholder="9 ספרות"
            aria-invalid={motherIdError ? true : undefined}
          />
        </FormField>

        <FormField
          id={`${ID_PREFIX}-mother-phone-number`}
          label="טלפון האם"
          className="family-details-grid__col-phone form-field--phone"
        >
          <PhoneInputGroup
            idPrefix={`${ID_PREFIX}-mother-phone`}
            prefix={motherPhonePrefix}
            number={motherPhoneNumber}
            disabled={loading}
            prefixError={motherPhoneErrors.prefix}
            numberError={motherPhoneErrors.number}
            onPrefixChange={handleMotherPhonePrefixChange}
            onNumberChange={handleMotherPhoneNumberChange}
            onPrefixBlur={() => setMotherPhoneErrors(validateOptionalPhoneParts(motherPhonePrefix, motherPhoneNumber))}
            onNumberBlur={() => setMotherPhoneErrors(validateOptionalPhoneParts(motherPhonePrefix, motherPhoneNumber))}
          />
        </FormField>
      </div>

      <AddressFields
        idPrefix={ID_PREFIX}
        values={structuredAddress}
        disabled={loading}
        errors={addressErrors}
        onChange={handleAddressChange}
        onErrorsChange={setAddressErrors}
      />

      <BankDetailsFields
        idPrefix={ID_PREFIX}
        values={bankDetails}
        defaultAccountHolderName=""
        accountHolderHint="(נלקח אוטומטית משם המשפחה, ניתן לעריכה במידת הצורך)"
        disabled={loading}
        fieldErrors={bankErrors}
        layoutVariant="create-family"
        strictBankSelection
        accountHolderSuggestOnFocus
        suggestedAccountHolderParts={{
          familyLastName,
          fatherName,
          motherName,
        }}
        onChange={handleBankChange}
        onBlurField={(field) => {
          validateBank(false, field === 'bankName' ? 'bankName' : field);
        }}
      />
    </ModalShell>
  );
}
