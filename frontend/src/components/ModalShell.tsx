import type { FormEvent, MouseEvent, ReactNode } from 'react';
import { ValidatedControl } from './FieldValidation';

interface ModalShellProps {
  title: string;
  /** When true, omit the visible title (history window §5.2). Provide ariaLabel. */
  hideTitle?: boolean;
  /** Accessible name when hideTitle is set. */
  ariaLabel?: string;
  hint?: string;
  wide?: boolean;
  extraWide?: boolean;
  sizeClassName?: string;
  headerActions?: ReactNode;
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
  hideTitle = false,
  ariaLabel,
  hint,
  wide = false,
  extraWide = false,
  sizeClassName,
  headerActions,
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
    sizeClassName ?? (extraWide ? 'modal-extra-wide' : wide ? 'modal-wide' : ''),
  ].filter(Boolean).join(' ');
  const bodyClass = ['modal-body', bodyClassName].filter(Boolean).join(' ');
  const headerClass = headerActions
    ? 'modal-header modal-header-with-actions'
    : 'modal-header';
  const showHeader = !hideTitle || Boolean(headerActions) || Boolean(hint);

  const inner = (
    <>
      {showHeader && (
        <div className={headerClass}>
          <div className="modal-header__title-group">
            {!hideTitle && <h2 id="modal-title">{title}</h2>}
            {hint && <p className="hint-text">{hint}</p>}
          </div>
          {headerActions && (
            <div className="modal-header__actions">
              {headerActions}
            </div>
          )}
        </div>
      )}
      <div className={bodyClass}>
        {children}
        {formError && <div className="error" role="alert">{formError}</div>}
      </div>
      <div className="modal-footer modal-actions">
        {footer}
      </div>
    </>
  );

  const labelledBy = hideTitle ? undefined : 'modal-title';
  const label = hideTitle ? (ariaLabel || title || 'דיאלוג') : undefined;

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div
        className={cardClass}
        onClick={handleCardClick}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        aria-label={label}
      >
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
  helperText?: string;
  children: ReactNode;
  className?: string;
}

export function FormField({ id, label, error, helperText, children, className }: FormFieldProps) {
  const invalid = Boolean(error);
  return (
    <div className={['form-field', className].filter(Boolean).join(' ')}>
      <label htmlFor={id}>{label}</label>
      <ValidatedControl error={invalid ? error : null} errorId={`${id}-error`}>
        {children}
      </ValidatedControl>
      {helperText && !invalid && (
        <p className="bank-field-hint">{helperText}</p>
      )}
    </div>
  );
}
