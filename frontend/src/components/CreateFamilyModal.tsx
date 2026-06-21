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
  `${ID_PREFIX}-mother-id`,
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
  const [phone, setPhone] = useState('');
  const [structuredAddress, setStructuredAddress] = useState<StructuredAddress>({ ...EMPTY_STRUCTURED_ADDRESS });
  const [bankDetails, setBankDetails] = useState<BankDetailsValues>({
    bankNumber: '',
    branchNumber: '',
    accountNumber: '',
    accountHolderName: '',
  });
  const [lastNameError, setLastNameError] = useState<string | null>(null);
  const [fatherIdError, setFatherIdError] = useState<string | null>(null);
  const [motherIdError, setMotherIdError] = useState<string | null>(null);
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
        phone: phone.trim().length > 0 ? phone.trim() : null,
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

  return (
    <ModalShell
      title="יצירת משפחה חדשה"
      hint="קוד המשפחה יוקצה אוטומטית בפורמט F-000001. מספר חשבונאי מוצע לפי הרכז/ת."
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
            placeholder="9 ספרות"
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
            placeholder="9 ספרות"
            aria-invalid={motherIdError ? true : undefined}
          />
        </FormField>
      </div>

      <FormField id={`${ID_PREFIX}-phone`} label="טלפון (אופציונלי)">
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
    </ModalShell>
  );
}
