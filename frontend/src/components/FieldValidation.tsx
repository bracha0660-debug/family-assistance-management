import type { ReactNode } from 'react';

interface FieldValidationTooltipProps {
  id: string;
  message: string;
}

/** Shared inline validation popover (orange icon + Hebrew message). */
export function FieldValidationTooltip({ id, message }: FieldValidationTooltipProps) {
  return (
    <div id={id} className="field-validation-tooltip" role="alert">
      <span className="field-validation-icon" aria-hidden="true">!</span>
      <span className="field-validation-text">{message}</span>
    </div>
  );
}

interface ValidatedControlProps {
  error?: string | null;
  errorId: string;
  children: ReactNode;
  className?: string;
}

/** Wraps a control; shows tooltip when `error` is set. */
export function ValidatedControl({ error, errorId, children, className }: ValidatedControlProps) {
  return (
    <div className={['validated-field-control', className].filter(Boolean).join(' ')}>
      {children}
      {error && <FieldValidationTooltip id={errorId} message={error} />}
    </div>
  );
}
