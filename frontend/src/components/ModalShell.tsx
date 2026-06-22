import type { FormEvent, MouseEvent, ReactNode } from 'react';
import { ValidatedControl } from './FieldValidation';

interface ModalShellProps {
  title: string;
  hint?: string;
  wide?: boolean;
  extraWide?: boolean;
  bodyClassName?: string;
  loading?: boolean;
  onClose: (e?: MouseEvent) => void;
  onSubmit?: (e: FormEvent) => void;
  formNoValidate?: boolean;
  footer: ReactNode;
  formError?: string;
  children: ReactNode;
}

export function ModalShell({
  title,
  hint,
  wide = false,
  extraWide = false,
  bodyClassName,
  loading = false,
  onClose,
  onSubmit,
  formNoValidate = true,
  footer,
  formError,
  children,
}: ModalShellProps) {
  function handleOverlayClick(e: MouseEvent) {
    e.stopPropagation();
    if (loading) return;
    onClose(e);
  }

  function handleCardClick(e: MouseEvent) {
    e.stopPropagation();
  }

  const cardClass = [
    'modal-card',
    'modal-scrollable',
    extraWide ? 'modal-extra-wide' : wide ? 'modal-wide' : '',
  ].filter(Boolean).join(' ');
  const bodyClass = ['modal-body', bodyClassName].filter(Boolean).join(' ');

  const inner = (
    <>
      <div className="modal-header">
        <h2 id="modal-title">{title}</h2>
        {hint && <p className="hint-text">{hint}</p>}
      </div>
      <div className={bodyClass}>
        {children}
        {formError && <div className="error" role="alert">{formError}</div>}
      </div>
      <div className="modal-footer modal-actions">
        {footer}
      </div>
    </>
  );

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div className={cardClass} onClick={handleCardClick} role="dialog" aria-modal="true" aria-labelledby="modal-title">
        {onSubmit ? (
          <form className="modal-form" onSubmit={onSubmit} noValidate={formNoValidate}>
            {inner}
          </form>
        ) : inner}
      </div>
    </div>
  );
}

interface FormFieldProps {
  id: string;
  label: ReactNode;
  error?: string | null;
  children: ReactNode;
  className?: string;
}

export function FormField({ id, label, error, children, className }: FormFieldProps) {
  const invalid = Boolean(error);
  return (
    <div className={['form-field', className].filter(Boolean).join(' ')}>
      <label htmlFor={id}>{label}</label>
      <ValidatedControl error={invalid ? error : null} errorId={`${id}-error`}>
        {children}
      </ValidatedControl>
    </div>
  );
}
