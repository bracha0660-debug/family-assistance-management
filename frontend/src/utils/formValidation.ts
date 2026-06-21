export function focusFirstInvalidField(fieldIds: string[]): boolean {
  for (const id of fieldIds) {
    const el = document.getElementById(id);
    if (!el) continue;

    const invalidByAria = el.getAttribute('aria-invalid') === 'true';
    const invalidByNative = el instanceof HTMLInputElement
      || el instanceof HTMLSelectElement
      || el instanceof HTMLTextAreaElement
      ? !el.checkValidity()
      : false;

    if (invalidByAria || invalidByNative) {
      el.focus();
      el.scrollIntoView({ block: 'center', behavior: 'smooth' });
      return true;
    }
  }
  return false;
}
