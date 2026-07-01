import { useCallback, useEffect, useState } from 'react';
import type { FormEvent, MouseEvent } from 'react';
import type { UserDto } from '../api/auth';
import { PERMISSION_KEYS } from '../api/permissions';
import {
  createSupplier,
  deactivateSupplier,
  listSuppliers,
  maskSupplierBank,
  restoreSupplier,
  updateSupplier,
  SupplierApiError,
  type CreateSupplierPayload,
  type InactiveSupplierConflictDetails,
  type SupplierDto,
  type SupplierListResponse,
  type UpdateSupplierPayload,
} from '../api/suppliers';
import { BankDetailsFields, type BankDetailsValues } from '../components/BankDetailsFields';
import { ModalShell, FormField } from '../components/ModalShell';
import { PhoneInputGroup, joinPhoneValue } from '../components/PhoneInputGroup';
import { hasPermission } from '../hooks/usePermissions';
import { findBankByNumber } from '../data/israeliBanks';
import { focusFirstInvalidField } from '../utils/formValidation';
import type { BankFieldErrors } from '../validation/bankFields';
import { isBankAllEmpty, validateBankFieldErrors } from '../validation/bankFields';
import {
  hasPhoneErrors,
  parsePhoneValue,
  validateOptionalPhoneParts,
  type PhoneFieldErrors,
} from '../validation/israeliPhone';
import { validateSupplierRegistrationNumber } from '../validation/supplierRegistrationNumber';
import { validateOptionalEmail } from '../validation/email';
import { translateStatus } from '../components/roleLabel';

interface SuppliersPageProps {

  user: UserDto;
}

function InactiveSupplierConflictModal({
  conflict,
  loading,
  onRestore,
  onCreateNew,
  onCancel,
}: {
  conflict: InactiveSupplierConflictDetails;
  loading: boolean;
  onRestore: (reason: string) => Promise<void>;
  onCreateNew: () => Promise<void>;
  onCancel: () => void;
}) {
  const [showReason, setShowReason] = useState(false);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');

  async function handleRestoreSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = reason.trim();
    if (trimmed.length < 3) {
      setError('סיבה חייבת להכיל לפחות 3 תווים');
      return;
    }
    setError('');
    await onRestore(trimmed);
  }

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>ספק מושבת עם אותו מספר</h2>
        <p>
          קיים ספק מושבת <strong>{conflict.existingSupplierCode}</strong> — {conflict.existingSupplierName} עם מספר עוסק / ח.פ. זה.
        </p>
        {showReason ? (
          <form onSubmit={handleRestoreSubmit}>
            <label htmlFor="inactive-supplier-restore-reason">סיבה (חובה)</label>
            <textarea
              id="inactive-supplier-restore-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={4}
              minLength={3}
              maxLength={500}
              required
              disabled={loading}
            />
            {error && <div className="error" role="alert">{error}</div>}
            <div className="modal-actions">
              <button type="button" className="btn-secondary" onClick={() => setShowReason(false)} disabled={loading}>חזרה</button>
              <button type="submit" disabled={loading}>{loading ? 'מעבד...' : 'אישור החייאה'}</button>
            </div>
          </form>
        ) : (
          <div className="modal-actions">
            <button type="button" onClick={() => setShowReason(true)} disabled={loading}>החייאת הספק הקיים</button>
            <button type="button" onClick={() => void onCreateNew()} disabled={loading}>יצירת ספק חדש</button>
            <button type="button" className="btn-secondary" onClick={onCancel} disabled={loading}>ביטול</button>
          </div>
        )}
      </div>
    </div>
  );
}

function SupplierFormModal({
  supplier,
  onClose,
  onSaved,
}: {
  supplier: SupplierDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = supplier !== null;
  const [name, setName] = useState(supplier?.name ?? '');
  const [registrationNumber, setRegistrationNumber] = useState(supplier?.registrationNumber ?? '');
  const initialPhone = parsePhoneValue(supplier?.phone ?? '');
  const [phonePrefix, setPhonePrefix] = useState(initialPhone.prefix);
  const [phoneNumber, setPhoneNumber] = useState(initialPhone.number);
  const [accountingCode, setAccountingCode] = useState(supplier?.accountingCode ?? '');
  const [email, setEmail] = useState(supplier?.email ?? '');
  const [address, setAddress] = useState(supplier?.address ?? '');
  const [bankDetails, setBankDetails] = useState<BankDetailsValues>({
    bankNumber: supplier?.bankNumber ?? '',
    bankName: findBankByNumber(supplier?.bankNumber ?? '')?.name ?? '',
    branchNumber: supplier?.branchNumber ?? '',
    accountNumber: supplier?.accountNumber ?? '',
    accountHolderName: supplier?.accountHolderName ?? '',
  });
  const [error, setError] = useState('');
  const [nameError, setNameError] = useState<string | null>(null);
  const [registrationError, setRegistrationError] = useState<string | null>(null);
  const [phoneErrors, setPhoneErrors] = useState<PhoneFieldErrors>({});
  const [accountingCodeError, setAccountingCodeError] = useState<string | null>(null);
  const [emailError, setEmailError] = useState<string | null>(null);
  const [bankErrors, setBankErrors] = useState<BankFieldErrors>({});
  const [loading, setLoading] = useState(false);
  const [inactiveConflict, setInactiveConflict] = useState<InactiveSupplierConflictDetails | null>(null);
  const [pendingCreatePayload, setPendingCreatePayload] = useState<CreateSupplierPayload | null>(null);

  const handleBankChange = useCallback((patch: Partial<BankDetailsValues>) => {
    setBankDetails((prev) => ({ ...prev, ...patch }));
  }, []);

  function handleRegistrationBlur() {
    setRegistrationError(validateSupplierRegistrationNumber(registrationNumber));
  }

  function handleRegistrationChange(value: string) {
    setRegistrationNumber(value);
    if (registrationError !== null) {
      setRegistrationError(validateSupplierRegistrationNumber(value));
    }
  }

  function handleClose(e?: MouseEvent) {
    if (e) e.stopPropagation();
    if (loading) return;
    onClose();
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
      setBankErrors((prev) => ({ ...prev, [field]: errors[field] ?? null }));
      return errors;
    }
    setBankErrors(errors);
    return errors;
  }

  function handlePhoneBlur() {
    setPhoneErrors(validateOptionalPhoneParts(phonePrefix, phoneNumber));
  }

  function handlePhonePrefixChange(value: string) {
    setPhonePrefix(value);
    if (hasPhoneErrors(phoneErrors)) {
      setPhoneErrors(validateOptionalPhoneParts(value, phoneNumber));
    }
  }

  function handlePhoneNumberChange(value: string) {
    setPhoneNumber(value);
    if (hasPhoneErrors(phoneErrors)) {
      setPhoneErrors(validateOptionalPhoneParts(phonePrefix, value));
    }
  }

  function handleEmailBlur() {
    setEmailError(validateOptionalEmail(email));
  }

  function handleEmailChange(value: string) {
    setEmail(value);
    if (emailError !== null) {
      setEmailError(validateOptionalEmail(value));
    }
  }

  function buildCreatePayload(): CreateSupplierPayload {
    const trimmedReg = registrationNumber.trim();
    const phoneValue = joinPhoneValue(phonePrefix, phoneNumber);
    const bankEmpty = isBankAllEmpty(
      bankDetails.bankNumber,
      bankDetails.branchNumber,
      bankDetails.accountNumber,
      bankDetails.accountHolderName,
      resolveBankName(),
    );
    const payload: CreateSupplierPayload = {
      name: name.trim(),
      registrationNumber: trimmedReg,
      phone: phoneValue || null,
      accountingCode: accountingCode.trim(),
      email: email.trim() || null,
      address: address.trim() || null,
    };
    if (!bankEmpty) {
      payload.bankNumber = bankDetails.bankNumber.trim();
      payload.branchNumber = bankDetails.branchNumber.trim();
      payload.accountNumber = bankDetails.accountNumber.trim();
      payload.accountHolderName = bankDetails.accountHolderName.trim();
    }
    return payload;
  }

  async function submitCreate(payload: CreateSupplierPayload) {
    await createSupplier(payload);
    onSaved();
    onClose();
  }

  async function handleRestoreExisting(reason: string) {
    if (!inactiveConflict) return;
    setLoading(true);
    setError('');
    try {
      await restoreSupplier(inactiveConflict.existingSupplierId, inactiveConflict.existingVersion, reason);
      setInactiveConflict(null);
      setPendingCreatePayload(null);
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  async function handleCreateDespiteInactive() {
    if (!pendingCreatePayload) return;
    setLoading(true);
    setError('');
    try {
      await submitCreate({ ...pendingCreatePayload, acknowledgeInactiveDuplicate: true });
      setInactiveConflict(null);
      setPendingCreatePayload(null);
    } catch (err) {
      if (err instanceof SupplierApiError) {
        setError(err.message);
      } else {
        setError(err instanceof Error ? err.message : 'שגיאת מערכת');
      }
    } finally {
      setLoading(false);
    }
  }

  const SUPPLIER_FOCUS_ORDER = [
    'supplier-name',
    'supplier-reg',
    'supplier-accounting-code',
    'supplier-email',
    'supplier-phone-prefix',
    'supplier-phone-number',
    'supplier-bank-number',
    'supplier-bank-name',
    'supplier-branch-number',
    'supplier-account-number',
    'supplier-account-holder',
  ];

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError('');
    if (name.trim().length < 2) {
      setNameError('שם ספק הוא שדה חובה');
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setNameError(null);

    const regErr = validateSupplierRegistrationNumber(registrationNumber);
    if (regErr) {
      setRegistrationError(regErr);
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setRegistrationError(null);

    const phoneErrs = validateOptionalPhoneParts(phonePrefix, phoneNumber);
    if (hasPhoneErrors(phoneErrs)) {
      setPhoneErrors(phoneErrs);
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setPhoneErrors({});

    const trimmedAccountingCode = accountingCode.trim();
    if (trimmedAccountingCode.length === 0) {
      setAccountingCodeError('קוד בהנהלת חשבונות הוא שדה חובה');
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    if (trimmedAccountingCode.length > 50) {
      setAccountingCodeError('קוד בהנהלת חשבונות חייב להיות עד 50 תווים');
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setAccountingCodeError(null);

    const emailErr = validateOptionalEmail(email);
    if (emailErr) {
      setEmailError(emailErr);
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setEmailError(null);

    const bErrs = validateBank(true);
    if (Object.values(bErrs).some(Boolean)) {
      focusFirstInvalidField(SUPPLIER_FOCUS_ORDER);
      return;
    }
    setLoading(true);
    try {
      if (isEdit && supplier) {
        const trimmedReg = registrationNumber.trim();
        const phoneValue = joinPhoneValue(phonePrefix, phoneNumber);
        const bankEmpty = isBankAllEmpty(
          bankDetails.bankNumber,
          bankDetails.branchNumber,
          bankDetails.accountNumber,
          bankDetails.accountHolderName,
          resolveBankName(),
        );
        const updatePayload: UpdateSupplierPayload = {
          name: name.trim(),
          registrationNumber: trimmedReg,
          phone: phoneValue || null,
          accountingCode: trimmedAccountingCode,
          email: email.trim() || null,
          address: address.trim() || null,
        };
        const hadBank = !isBankAllEmpty(
          supplier.bankNumber ?? '',
          supplier.branchNumber ?? '',
          supplier.accountNumber ?? '',
          supplier.accountHolderName ?? '',
        );
        const bankChanged = bankEmpty
          ? hadBank
          : bankDetails.bankNumber.trim() !== (supplier.bankNumber ?? '')
            || bankDetails.branchNumber.trim() !== (supplier.branchNumber ?? '')
            || bankDetails.accountNumber.trim() !== (supplier.accountNumber ?? '')
            || bankDetails.accountHolderName.trim() !== (supplier.accountHolderName ?? '');
        if (bankChanged) {
          if (bankEmpty) {
            updatePayload.bankNumber = null;
            updatePayload.branchNumber = null;
            updatePayload.accountNumber = null;
            updatePayload.accountHolderName = null;
          } else {
            updatePayload.bankNumber = bankDetails.bankNumber.trim();
            updatePayload.branchNumber = bankDetails.branchNumber.trim();
            updatePayload.accountNumber = bankDetails.accountNumber.trim();
            updatePayload.accountHolderName = bankDetails.accountHolderName.trim();
          }
        }
        await updateSupplier(supplier.id, supplier.version, updatePayload);
      } else {
        const payload = buildCreatePayload();
        await submitCreate(payload);
      }
    } catch (err) {
      if (err instanceof SupplierApiError && err.code === 'INACTIVE_SUPPLIER_SAME_REGISTRATION' && err.inactiveConflict) {
        setPendingCreatePayload(buildCreatePayload());
        setInactiveConflict(err.inactiveConflict);
        return;
      }
      if (err instanceof SupplierApiError) {
        setError(err.message);
        return;
      }
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
    <ModalShell
      title={isEdit ? 'עריכת ספק' : 'ספק חדש'}
      wide
      loading={loading}
      onClose={handleClose}
      onSubmit={handleSubmit}
      formError={error}
      footer={(
        <>
          <button type="button" className="btn-secondary" onClick={() => handleClose()} disabled={loading}>ביטול</button>
          <button type="submit" disabled={loading}>{loading ? 'שומר...' : 'שמור'}</button>
        </>
      )}
    >
      {isEdit && supplier && (
        <>
          <label>קוד ספק</label>
          <input type="text" value={supplier.supplierCode} disabled readOnly />
        </>
      )}
      <FormField id="supplier-name" label={<>שם <span className="field-required">*</span></>} error={nameError}>
        <input
          id="supplier-name"
          type="text"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            if (nameError) setNameError(null);
          }}
          disabled={loading}
          maxLength={200}
          aria-invalid={nameError ? true : undefined}
        />
      </FormField>
      <FormField
        id="supplier-reg"
        label={<>מספר עוסק / ח.פ. <span className="field-required">*</span></>}
        error={registrationError}
      >
        <input
          id="supplier-reg"
          type="text"
          value={registrationNumber}
          onChange={(e) => handleRegistrationChange(e.target.value)}
          onBlur={handleRegistrationBlur}
          disabled={loading}
          inputMode="numeric"
          maxLength={9}
          aria-invalid={registrationError !== null}
          aria-describedby={registrationError ? 'supplier-reg-error' : undefined}
        />
      </FormField>
      <FormField
        id="supplier-accounting-code"
        label={<>קוד בהנהלת חשבונות <span className="field-required">*</span></>}
        error={accountingCodeError}
      >
        <input
          id="supplier-accounting-code"
          type="text"
          value={accountingCode}
          onChange={(e) => {
            setAccountingCode(e.target.value);
            if (accountingCodeError) setAccountingCodeError(null);
          }}
          disabled={loading}
          maxLength={50}
          aria-invalid={accountingCodeError ? true : undefined}
        />
      </FormField>
      <FormField id="supplier-email" label="כתובת אימייל" error={emailError}>
        <input
          id="supplier-email"
          type="email"
          value={email}
          onChange={(e) => handleEmailChange(e.target.value)}
          onBlur={handleEmailBlur}
          disabled={loading}
          maxLength={254}
          aria-invalid={emailError ? true : undefined}
        />
      </FormField>
      <FormField id="supplier-phone-prefix" label="טלפון" error={phoneErrors.prefix || phoneErrors.number}>
        <PhoneInputGroup
          idPrefix="supplier-phone"
          prefix={phonePrefix}
          number={phoneNumber}
          disabled={loading}
          prefixError={phoneErrors.prefix}
          numberError={phoneErrors.number}
          onPrefixChange={handlePhonePrefixChange}
          onNumberChange={handlePhoneNumberChange}
          onPrefixBlur={handlePhoneBlur}
          onNumberBlur={handlePhoneBlur}
        />
      </FormField>
      <label htmlFor="supplier-address">כתובת</label>
      <input id="supplier-address" type="text" value={address} onChange={(e) => setAddress(e.target.value)} disabled={loading} maxLength={300} />
      <BankDetailsFields
        idPrefix="supplier"
        values={bankDetails}
        defaultAccountHolderName={name.trim()}
        accountHolderHint="(נלקח אוטומטית משם הספק, ניתן לעריכה במידת הצורך)"
        disabled={loading}
        fieldErrors={bankErrors}
        onChange={handleBankChange}
        onBlurField={(field) => {
          validateBank(false, field === 'bankName' ? 'bankName' : field);
        }}
      />
    </ModalShell>
    {inactiveConflict && (
      <InactiveSupplierConflictModal
        conflict={inactiveConflict}
        loading={loading}
        onRestore={handleRestoreExisting}
        onCreateNew={handleCreateDespiteInactive}
        onCancel={() => {
          if (loading) return;
          setInactiveConflict(null);
          setPendingCreatePayload(null);
        }}
      />
    )}
    </>
  );
}

function StatusActionDialog({
  title,
  supplier,
  action,
  onClose,
  onDone,
}: {
  title: string;
  supplier: SupplierDto;
  action: (id: string, version: number, reason: string) => Promise<SupplierDto>;
  onClose: () => void;
  onDone: () => void;
}) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await action(supplier.id, supplier.version, reason);
      onDone();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <h2>{title}</h2>
        <p><strong>{supplier.supplierCode}</strong> — {supplier.name}</p>
        <form onSubmit={handleSubmit}>
          <label htmlFor="supplier-action-reason">סיבה (חובה)</label>
          <textarea id="supplier-action-reason" value={reason} onChange={(e) => setReason(e.target.value)} required minLength={3} maxLength={500} rows={4} disabled={loading} />
          {error && <div className="error" role="alert">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>ביטול</button>
            <button type="submit" disabled={loading}>{loading ? 'מעבד...' : 'אישור'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function SuppliersPage({ user }: SuppliersPageProps) {
  const [data, setData] = useState<SupplierListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editTarget, setEditTarget] = useState<SupplierDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<SupplierDto | null>(null);
  const [restoreTarget, setRestoreTarget] = useState<SupplierDto | null>(null);

  const load = useCallback(async () => {
    setError('');
    try {
      setData(await listSuppliers());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const canEdit = hasPermission(user, PERMISSION_KEYS.suppliersEdit);
  const canCreate = hasPermission(user, PERMISSION_KEYS.suppliersCreate);
  const canDeactivate = hasPermission(user, PERMISSION_KEYS.suppliersDeactivate);
  const canRestore = hasPermission(user, PERMISSION_KEYS.suppliersRestore);

  return (
    <div>
      {data && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-label">סה״כ ספקים</span>
            <span className="summary-value">{data.summary.total}</span>
          </div>
          <div className="summary-card summary-active">
            <span className="summary-label">פעילים</span>
            <span className="summary-value">{data.summary.active}</span>
          </div>
          <div className="summary-card summary-suspended">
            <span className="summary-label">לא פעילים</span>
            <span className="summary-value">{data.summary.inactive}</span>
          </div>
        </div>
      )}

      <div className="toolbar">
        {canCreate && (
          <button type="button" onClick={() => setShowCreate(true)}>ספק חדש</button>
        )}
        <button type="button" className="btn-secondary" onClick={load}>רענן</button>
      </div>

      {error && <div className="error" role="alert">{error}</div>}

      {loading ? (
        <p>טוען ספקים...</p>
      ) : (
        <div className="table-wrap">
          <table className="org-table">
            <thead>
              <tr>
                <th>קוד</th>
                <th>שם</th>
                <th>ח.פ./עוסק</th>
                <th>קוד הנהלת חשבונות</th>
                <th>אימייל</th>
                <th>טלפון</th>
                <th>חשבון בנק</th>
                <th>סטטוס</th>
                <th>פעולות</th>
              </tr>
            </thead>
            <tbody>
              {(data?.suppliers ?? []).length === 0 && (
                <tr><td colSpan={9} className="empty-row">אין ספקים להצגה</td></tr>
              )}
              {(data?.suppliers ?? []).map((s) => (
                <tr key={s.id} className={s.status === 'inactive' ? 'row-disabled' : undefined}>
                  <td><code>{s.supplierCode}</code></td>
                  <td>{s.name}</td>
                  <td>{s.registrationNumber ?? '—'}</td>
                  <td>{s.accountingCode ?? '—'}</td>
                  <td>{s.email ?? '—'}</td>
                  <td>{s.phone ?? '—'}</td>
                  <td><code>{maskSupplierBank(s)}</code></td>
                  <td>
                    <span className={`status-badge status-${s.status === 'inactive' ? 'suspended' : s.status}`}>
                      {translateStatus(s.status)}
                    </span>
                  </td>
                  <td className="actions-cell">
                    {s.status === 'active' && canEdit && (
                      <button type="button" className="btn-small" onClick={() => setEditTarget(s)}>ערוך</button>
                    )}
                    {s.status === 'active' && canDeactivate && (
                      <button type="button" className="btn-small btn-danger" onClick={() => setDeactivateTarget(s)}>השבת</button>
                    )}
                    {s.status === 'inactive' && canRestore && (
                      <button type="button" className="btn-small" onClick={() => setRestoreTarget(s)}>שחזר</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCreate && (
        <SupplierFormModal supplier={null} onClose={() => setShowCreate(false)} onSaved={load} />
      )}
      {editTarget && (
        <SupplierFormModal supplier={editTarget} onClose={() => setEditTarget(null)} onSaved={load} />
      )}
      {deactivateTarget && (
        <StatusActionDialog
          title="השבתת ספק"
          supplier={deactivateTarget}
          action={deactivateSupplier}
          onClose={() => setDeactivateTarget(null)}
          onDone={load}
        />
      )}
      {restoreTarget && (
        <StatusActionDialog
          title="שחזור ספק"
          supplier={restoreTarget}
          action={restoreSupplier}
          onClose={() => setRestoreTarget(null)}
          onDone={load}
        />
      )}
    </div>
  );
}
