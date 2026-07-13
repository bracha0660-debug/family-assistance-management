import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import {
  listAssistanceItemHistory,
  type AssistanceItemHistoryEventDto,
} from '../api/assistanceItems';
import { KpiStatusIcon } from '../pages/home/widgets/KpiStatusIcon';
import { HistoryValueTransition } from './history/HistoryValueTransition';
import {
  historyDisplayValue,
  historyEventLabelHe,
  historyFieldLabelHe,
} from './history/historyLabels';

function historyEventClass(eventType: string): string {
  switch (eventType) {
    case 'approved':
      return 'history-event--approved';
    case 'marked_paid':
    case 'reference_entered':
      return 'history-event--paid';
    case 'process_completed':
      return 'history-event--completed';
    case 'suspended':
      return 'history-event--on-hold';
    case 'returned':
      return 'history-event--returned';
    case 'rejected':
      return 'history-event--rejected';
    case 'submitted':
    case 'resubmitted':
      return 'history-event--pending-approval';
    case 'export_batch_created':
      return 'history-event--pending-execution';
    case 'item_edited':
    case 'item_created':
    case 'amount_changed':
    case 'supplier_changed':
      return 'history-event--draft';
    case 'export_item_cancelled':
    case 'export_batch_cancelled':
      return 'history-event--cancel';
    default:
      return 'history-event--neutral';
  }
}

function historySemantic(eventType: string): string {
  switch (eventType) {
    case 'approved':
      return 'approved';
    case 'marked_paid':
    case 'reference_entered':
      return 'paid';
    case 'process_completed':
      return 'completed';
    case 'rejected':
    case 'export_item_cancelled':
    case 'export_batch_cancelled':
      return 'rejected';
    case 'suspended':
      return 'on_hold';
    case 'returned':
      return 'returned_for_treatment';
    case 'submitted':
    case 'resubmitted':
      return 'pending_approval';
    case 'export_batch_created':
      return 'pending_execution';
    case 'item_edited':
    case 'item_created':
    case 'amount_changed':
    case 'supplier_changed':
      return 'draft';
    default:
      return 'draft';
  }
}

const POPOVER_WIDTH = 300;
const VIEWPORT_PAD = 8;

function positionNearAnchor(anchor: HTMLElement, panel: HTMLElement): { top: number; left: number } {
  const rect = anchor.getBoundingClientRect();
  const panelHeight = panel.offsetHeight || 420;
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  // Prefer opening toward the page center from the icon (icon is usually on the left in RTL tables).
  let left = rect.right + 8;
  if (left + POPOVER_WIDTH > vw - VIEWPORT_PAD) {
    left = rect.left - POPOVER_WIDTH - 8;
  }
  left = Math.max(VIEWPORT_PAD, Math.min(left, vw - POPOVER_WIDTH - VIEWPORT_PAD));

  let top = rect.top;
  if (top + panelHeight > vh - VIEWPORT_PAD) {
    top = Math.max(VIEWPORT_PAD, vh - panelHeight - VIEWPORT_PAD);
  }
  top = Math.max(VIEWPORT_PAD, top);

  return { top, left };
}

export function AssistanceItemHistoryModal({
  assistanceItemId,
  anchorEl,
  onClose,
}: {
  assistanceItemId: string;
  /** History icon button — panel anchors next to this element. */
  anchorEl: HTMLElement | null;
  onClose: () => void;
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null);
  const [events, setEvents] = useState<AssistanceItemHistoryEventDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [mappingError, setMappingError] = useState('');
  const pageSize = 25;

  async function loadPage(offset: number, append: boolean) {
    if (append) setLoadingMore(true);
    else setLoading(true);
    setError('');
    try {
      const page = await listAssistanceItemHistory(assistanceItemId, {
        limit: pageSize,
        offset,
      });
      setTotal(page.total);
      setEvents((prev) => {
        if (!append) return page.events;
        const seen = new Set(prev.map((e) => e.id));
        return [...prev, ...page.events.filter((e) => !seen.has(e.id))];
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאה בטעינת היסטוריה');
    } finally {
      setLoading(false);
      setLoadingMore(false);
    }
  }

  useEffect(() => {
    void loadPage(0, false);
  }, [assistanceItemId]);

  useEffect(() => {
    for (const event of events) {
      const eventLabel = historyEventLabelHe(event.eventType, event.eventDescriptionHe);
      if (!eventLabel) {
        setMappingError(`חסר מיפוי עברי לסוג אירוע: ${event.eventType}`);
        return;
      }
      for (const change of event.fieldChanges) {
        const label = historyFieldLabelHe(change.fieldKey, change.fieldLabelHe);
        if (!label) {
          setMappingError(`חסר מיפוי עברי לשדה: ${change.fieldKey}`);
          return;
        }
      }
    }
    setMappingError('');
  }, [events]);

  useLayoutEffect(() => {
    function update() {
      if (!anchorEl || !panelRef.current) return;
      setCoords(positionNearAnchor(anchorEl, panelRef.current));
    }
    update();
    window.addEventListener('resize', update);
    window.addEventListener('scroll', update, true);
    return () => {
      window.removeEventListener('resize', update);
      window.removeEventListener('scroll', update, true);
    };
  }, [anchorEl, events, loading, loadingMore, error]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    function onPointer(e: MouseEvent) {
      const panel = panelRef.current;
      const target = e.target as Node | null;
      if (!target) return;
      if (panel?.contains(target)) return;
      if (anchorEl?.contains(target)) return;
      onClose();
    }
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onPointer);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onPointer);
    };
  }, [anchorEl, onClose]);

  return (
    <div className="history-popover-root" role="presentation">
      <div className="history-popover-backdrop" aria-hidden="true" />
      <div
        ref={panelRef}
        className="history-popover"
        role="dialog"
        aria-label="היסטוריית פריט סיוע"
        data-testid="assistance-item-history-popover"
        style={coords ? { top: coords.top, left: coords.left } : { visibility: 'hidden', top: 0, left: 0 }}
      >
        <div className="history-popover__header">
          <span className="history-popover__header-label">היסטוריה</span>
          <button type="button" className="history-popover__close" onClick={onClose} aria-label="סגור">
            ×
          </button>
        </div>

        <div className="history-popover__body">
          {loading && events.length === 0 && (
            <p className="assistance-item-history__empty">טוען…</p>
          )}
          {events.length === 0 && !loading && !error && (
            <p className="assistance-item-history__empty">אין אירועי היסטוריה להצגה</p>
          )}
          {error && events.length === 0 && (
            <p className="assistance-item-history__error">{error}</p>
          )}
          {error && events.length > 0 && (
            <div className="assistance-item-history__error">
              {error}
              <button type="button" className="btn-small btn-action-neutral" onClick={() => void loadPage(events.length, true)}>
                נסה שוב
              </button>
            </div>
          )}
          {mappingError && (
            <p className="assistance-item-history__error">{mappingError}</p>
          )}
          {!mappingError && events.length > 0 && (
            <div className="assistance-item-history" data-testid="assistance-item-history">
              {events.map((event, index) => {
                const eventLabel = historyEventLabelHe(event.eventType, event.eventDescriptionHe);
                if (!eventLabel) return null;
                return (
                  <div key={event.id} className="assistance-item-history__step">
                    {index > 0 && (
                      <div className="assistance-item-history__connector" aria-hidden="true">
                        <span className="assistance-item-history__connector-line" />
                        <span className="assistance-item-history__connector-arrow">↓</span>
                      </div>
                    )}
                    <article
                      className={`assistance-item-history__station ${historyEventClass(event.eventType)}`}
                    >
                      <div className="assistance-item-history__meta">
                        <span className="assistance-item-history__icon" aria-hidden="true">
                          <KpiStatusIcon semantic={historySemantic(event.eventType)} />
                        </span>
                        <span>
                          {new Date(event.occurredAt).toLocaleString('he-IL')}
                          {' · '}
                          {event.actorDisplayName || 'מערכת'}
                        </span>
                      </div>
                      <div className="assistance-item-history__title">{eventLabel}</div>
                      {event.reason && <div className="hint-text">סיבה: {event.reason}</div>}
                      {event.fieldChanges.map((change) => {
                        const fieldLabel = historyFieldLabelHe(change.fieldKey, change.fieldLabelHe);
                        if (!fieldLabel) return null;
                        return (
                          <div key={change.id} className="assistance-item-history__change">
                            <span className="assistance-item-history__change-label">{fieldLabel}:</span>
                            <HistoryValueTransition
                              previousValue={historyDisplayValue(change.previousValue)}
                              newValue={historyDisplayValue(change.newValue)}
                            />
                          </div>
                        );
                      })}
                    </article>
                  </div>
                );
              })}
            </div>
          )}
          {events.length < total && !mappingError && (
            <button
              type="button"
              className="btn-small btn-action-neutral history-popover__more"
              disabled={loadingMore}
              onClick={() => void loadPage(events.length, true)}
            >
              {loadingMore ? 'טוען…' : 'טען עוד'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
