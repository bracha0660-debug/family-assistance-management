import historyIconUrl from '../../assets/history-icon.png';

/** Icon-only history control — outside business-action group (§4.2.0 / §20). */
export function HistoryIconButton({
  disabled,
  onClick,
}: {
  disabled?: boolean;
  onClick: (anchor: HTMLElement) => void;
}) {
  return (
    <button
      type="button"
      className="btn-icon-history"
      disabled={disabled}
      title="היסטוריה"
      aria-label="היסטוריה"
      onClick={(e) => onClick(e.currentTarget)}
      data-testid="history-icon-button"
    >
      <img
        className="btn-icon-history__glyph"
        src={historyIconUrl}
        alt=""
        width={18}
        height={18}
        draggable={false}
      />
    </button>
  );
}

