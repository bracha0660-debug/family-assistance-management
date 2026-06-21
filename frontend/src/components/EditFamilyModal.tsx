import { useCallback, useState } from 'react';
import type { FormEvent } from 'react';
import {
  updateFamily,
  type FamilyDto,
  type UpdateFamilyPayload,
} from '../api/families';
import { AddressFields, validateAddressFields, type AddressFieldErrors } from './AddressFields';
import { BankDetailsFields, type BankDetailsValues } from './BankDetailsFields';
import { FormField, ModalShell } from './ModalShell';
import { findBankByNumber } from '../data/israeliBanks';
import { focusFirstInvalidField } from '../utils/formValidation';
import type { BankFieldErrors } from '../validation/bankFields';
import { isBankAllEmpty, validateBankFieldErrors } from '../validation/bankFields';
import {
  formatFamilyAddress,
  parseFamilyAddress,
  type StructuredAddress,
} from '../validation/familyAddress';
import { isValidIsraeliId } from '../validation/israeliId';

interface EditFamilyModalProps {
  family: FamilyDto;
  onClose: () => void;
  onUpdated: () => void;
}

const ID_PREFIX = 'edit-family';

function validateOptionalId(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  if (!isValidIsraeliId(trimmed)) return 'מספר תעודת זהות אינו תקין';
  return null;
}

function isMaterialChange(
  family: FamilyDto,
  payload: UpdateFamilyPayload,
): boolean {
  if (payload.accountingCode !== undefined && payload.accountingCode !== family.accountingCode) return true;
  if (payload.fatherIsraeliId !== undefined && payload.fatherIsraeliId !== (family.fatherIsraeliId ?? null)) return true;
  if (payload.motherIsraeliId !== undefined && payload.motherIsraeliId !== (family.motherIsraeliId ?? null)) return true;
  if (payload.bankNumber !== undefined && payload.bankNumber !== (family.bankNumber ?? null)) return true;
  if (payload.branchNumber !== undefined && payload.branchNumber !== (family.branchNumber ?? null)) return true;
  if (payload.accountNumber !== undefined && payload.accountNumber !== (family.accountNumber ?? null)) return true;
  if (payload.accountHolderName !== undefined && payload.accountHolderName !== (family.accountHolderName ?? null)) return true;
  if (payload.assignedCoordinatorId !== undefined && payload.assignedCoordinatorId !== family.assignedCoordinatorId) return true;
  return false;
}

const FOCUS_FIELD_ORDER = [
  `${ID_PREFIX}-family-last-name`,
  `${ID_PREFIX}-father-id`,
  `${ID_PREFIX}-mother-id`,
  `${ID_PREFIX}-city`,
  `${ID_PREFIX}-street`,
  `${ID_PREFIX}-bank-number`,
  `${ID_PREFIX}-bank-name`,
  `${ID_PREFIX}-branch-number`,
  `${ID_PREFIX}-account-number`,
  `${ID_PREFIX}-account-holder`,
  `${ID_PREFIX}-accounting-code`,
  `${ID_PREFIX}-material-reason`,
];

export function EditFamilyModal({ family, onClose, onUpdated }: EditFamilyModalProps) {
  const [accountingCode, setAccountingCode] = useState(String(family.accountingCode));
  const [familyLastName, setFamilyLastName] = useState(family.familyLastName);
  const [fatherName, setFatherName] = useState(family.fatherName ?? '');
  const [fatherIsraeliId, setFatherIsraeliId] = useState(family.fatherIsraeliId ?? '');
  const [motherName, setMotherName] = useState(family.motherName ?? '');
  const [motherIsraeliId, setMotherIsraeliId] = useState(family.motherIsraeliId ?? '');
  const [phone, setPhone] = useState(family.phone ?? '');
  const [structuredAddress, setStructuredAddress] = useState<StructuredAddress>(() => parseFamilyAddress(family.address));
  const [bankDetails, setBankDetails] = useState<BankDetailsValues>({
    bankNumber: family.bankNumber ?? '',
    branchNumber: family.branchNumber ?? '',
    accountNumber: family.accountNumber ?? '',
    accountHolderName: family.accountHolderName ?? '',
  });
  const [reason, setReason] = useState('');
  const [lastNameError, setLastNameError] = useState<string | null>(null);
  const [fatherIdError, setFatherIdError] = useState<string | null>(null);
  const [motherIdError, setMotherIdError] = useState<string | null>(null);
  const [accountingError, setAccountingError] = useState<string | null>(null);
  const [reasonError, setReasonError] = useState<string | null>(null);
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

  function resolveBankName(): string {
    return findBankByNumber(bankDetails.bankNumber)?.name ?? '';
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

  const needsReason =
    accountingCode.trim() !== String(family.accountingCode)
    || fatherIsraeliId.trim() !== (family.fatherIsraeliId ?? '')
    || motherIsraeliId.trim() !== (family.motherIsraeliId ?? '')
    || bankDetails.bankNumber.trim() !== (family.bankNumber ?? '')
    || bankDetails.branchNumber.trim() !== (family.branchNumber ?? '')
    || bankDetails.accountNumber.trim() !== (family.accountNumber ?? '')
    || bankDetails.accountHolderName.trim() !== (family.accountHolderName ?? '');

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

    const parsedAccounting = Number(accountingCode.trim());
    if (!Number.isInteger(parsedAccounting) || parsedAccounting <= 0) {
      setAccountingError('מספר חשבונאי חייב להיות מספר חיובי');
      valid = false;
    } else {
      setAccountingError(null);
    }

    const addrErrs = validateAddressFields(structuredAddress);
    setAddressErrors(addrErrs);
    if (addrErrs.city || addrErrs.street) valid = false;

    const bErrs = validateBank(true);
    if (Object.values(bErrs).some(Boolean)) valid = false;

    if (needsReason && reason.trim().length < 3) {
      setReasonError('יש לציין סיבה לשינוי מהותי (מספר חשבונאי, ת.ז. או פרטי בנק)');
      valid = false;
    } else {
      setReasonError(null);
    }

    if (!valid) {
      focusFirstInvalidField(FOCUS_FIELD_ORDER);
    }
    return valid;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!validateAll()) return;

    const trimmedLastName = familyLastName.trim();
    const parsedAccounting = Number(accountingCode.trim());

    const payload: UpdateFamilyPayload = {};
    if (trimmedLastName !== family.familyLastName) payload.familyLastName = trimmedLastName;
    if (parsedAccounting !== family.accountingCode) payload.accountingCode = parsedAccounting;
    const newFatherName = fatherName.trim().length > 0 ? fatherName.trim() : null;
    if (newFatherName !== (family.fatherName ?? null)) payload.fatherName = newFatherName;
    const newFatherId = fatherIsraeliId.trim().length > 0 ? fatherIsraeliId.trim() : null;
    if (newFatherId !== (family.fatherIsraeliId ?? null)) payload.fatherIsraeliId = newFatherId;
    const newMotherName = motherName.trim().length > 0 ? motherName.trim() : null;
    if (newMotherName !== (family.motherName ?? null)) payload.motherName = newMotherName;
    const newMotherId = motherIsraeliId.trim().length > 0 ? motherIsraeliId.trim() : null;
    if (newMotherId !== (family.motherIsraeliId ?? null)) payload.motherIsraeliId = newMotherId;
    const newPhone = phone.trim().length > 0 ? phone.trim() : null;
    if (newPhone !== (family.phone ?? null)) payload.phone = newPhone;
    const newAddress = formatFamilyAddress(structuredAddress);
    if (newAddress !== (family.address ?? null)) payload.address = newAddress;
    const bankEmpty = isBankAllEmpty(
      bankDetails.bankNumber,
      bankDetails.branchNumber,
      bankDetails.accountNumber,
      bankDetails.accountHolderName,
      resolveBankName(),
    );
    const hadBank = !isBankAllEmpty(
      family.bankNumber ?? '',
      family.branchNumber ?? '',
      family.accountNumber ?? '',
      family.accountHolderName ?? '',
    );
    const bankChanged = bankEmpty
      ? hadBank
      : bankDetails.bankNumber.trim() !== (family.bankNumber ?? '')
        || bankDetails.branchNumber.trim() !== (family.branchNumber ?? '')
        || bankDetails.accountNumber.trim() !== (family.accountNumber ?? '')
        || bankDetails.accountHolderName.trim() !== (family.accountHolderName ?? '');

    if (bankChanged) {
      if (bankEmpty) {
        payload.bankNumber = null;
        payload.branchNumber = null;
        payload.accountNumber = null;
        payload.accountHolderName = null;
      } else {
        payload.bankNumber = bankDetails.bankNumber.trim();
        payload.branchNumber = bankDetails.branchNumber.trim();
        payload.accountNumber = bankDetails.accountNumber.trim();
        payload.accountHolderName = bankDetails.accountHolderName.trim();
      }
    }

    if (Object.keys(payload).length === 0) {
      setFormError('אין שינויים לעדכון');
      return;
    }

    if (isMaterialChange(family, payload)) {
      payload.reason = reason.trim();
    }

    setLoading(true);
    try {
      await updateFamily(family.id, family.version, payload);
      onUpdated();
      onClose();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <ModalShell
      title="עריכת משפחה"
      wide
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
            {loading ? 'שומר...' : 'שמור שינויים'}
          </button>
        </>
      )}
    >
      <FormField id={`${ID_PREFIX}-code`} label="קוד משפחה">
        <input id={`${ID_PREFIX}-code`} type="text" value={family.familyCode} disabled readOnly />
      </FormField>

      <div className="form-grid-2">
        <FormField
          id={`${ID_PREFIX}-accounting-code`}
          label="מספר חשבונאי"
          error={accountingError}
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
            aria-invalid={accountingError ? true : undefined}
          />
        </FormField>

        <FormField
          id={`${ID_PREFIX}-family-last-name`}
          label={<>שם משפחה <span className="field-required">*</span></>}
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
      </div>

      <div className="form-grid-2">
        <FormField id={`${ID_PREFIX}-father-name`} label="שם האב">
          <input
            id={`${ID_PREFIX}-father-name`}
            type="text"
            value={fatherName}
            onChange={(e) => setFatherName(e.target.value)}
            disabled={loading}
            maxLength={200}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-father-id`} label="ת.ז. האב" error={fatherIdError}>
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
            aria-invalid={fatherIdError ? true : undefined}
          />
        </FormField>
      </div>

      <div className="form-grid-2">
        <FormField id={`${ID_PREFIX}-mother-name`} label="שם האם">
          <input
            id={`${ID_PREFIX}-mother-name`}
            type="text"
            value={motherName}
            onChange={(e) => setMotherName(e.target.value)}
            disabled={loading}
            maxLength={200}
          />
        </FormField>

        <FormField id={`${ID_PREFIX}-mother-id`} label="ת.ז. האם" error={motherIdError}>
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
            aria-invalid={motherIdError ? true : undefined}
          />
        </FormField>
      </div>

      <FormField id={`${ID_PREFIX}-phone`} label="טלפון">
        <input
          id={`${ID_PREFIX}-phone`}
          type="tel"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          disabled={loading}
          maxLength={30}
        />
      </FormField>

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
        defaultAccountHolderName={familyLastName.trim()}
        accountHolderHint="(נלקח אוטומטית משם המשפחה, ניתן לעריכה במידת הצורך)"
        disabled={loading}
        fieldErrors={bankErrors}
        onChange={handleBankChange}
        onBlurField={(field) => {
          validateBank(false, field === 'bankName' ? 'bankName' : field);
        }}
      />

      {needsReason && (
        <FormField
          id={`${ID_PREFIX}-material-reason`}
          label={<>סיבת שינוי מהותי <span className="field-required">*</span></>}
          error={reasonError}
        >
          <textarea
            id={`${ID_PREFIX}-material-reason`}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value);
              if (reasonError) setReasonError(null);
            }}
            disabled={loading}
            rows={3}
            minLength={3}
            maxLength={500}
            aria-invalid={reasonError ? true : undefined}
          />
        </FormField>
      )}
    </ModalShell>
  );
}
