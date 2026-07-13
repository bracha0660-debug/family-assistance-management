/**
 * Previous → new value transition for RTL (§5.3 / §20).
 * Three explicit elements + bidi isolation so the browser cannot reverse mixed content.
 *
 * Visual (dir=rtl): original/previous on the right, ← , updated/new on the left.
 * Screen left→right example: 350 ₪ ← 75 ₪  (new 350, previous 75).
 */
export function HistoryValueTransition({
  previousValue,
  newValue,
}: {
  previousValue: string;
  newValue: string;
}) {
  return (
    <span className="history-value-transition" dir="rtl" data-testid="history-value-transition">
      <bdi className="assistance-item-history__change-prev" dir="ltr" data-testid="history-prev">
        {previousValue}
      </bdi>
      <span className="assistance-item-history__change-arrow" aria-hidden="true" data-testid="history-arrow">
        ←
      </span>
      <bdi className="assistance-item-history__change-new" dir="ltr" data-testid="history-new">
        {newValue}
      </bdi>
    </span>
  );
}
