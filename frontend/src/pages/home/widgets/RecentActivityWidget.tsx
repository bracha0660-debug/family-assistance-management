import type { HomeNavigationTarget, HomeRecentActivityEntry } from '../../../api/workflow';
import { formatRelativeTime } from '../formatRelativeTime';
import { statusSemanticCardClass } from '../workflowStatus';
import { KpiStatusIcon } from './KpiStatusIcon';

interface RecentActivityWidgetProps {
  title: string;
  entries: HomeRecentActivityEntry[];
  onNavigate: (target: HomeNavigationTarget) => void;
}

export function RecentActivityWidget({ title, entries, onNavigate }: RecentActivityWidgetProps) {
  if (entries.length === 0) return null;

  return (
    <section className="home-widget home-activity-widget" aria-label="פעילות אחרונה">
      <div className="home-panel home-activity-panel">
        {title && (
          <div className="home-widget-section-header">
            <h2 className="home-widget-section-title">{title}</h2>
          </div>
        )}
        <ol className="home-activity-timeline">
          {entries.map((entry) => {
            const statusClass = statusSemanticCardClass(entry.statusSemantic);

            const content = (
              <>
                <span className={`home-activity-status-label ${statusClass}`}>{entry.statusLabel}</span>
                <span className="home-activity-primary">
                  <span className="home-activity-decision-code">{entry.decisionCode}</span>
                  {entry.familyName && (
                    <span className="home-activity-family-name">{entry.familyName}</span>
                  )}
                </span>
                <span className="home-activity-meta">
                  {entry.actorName && (
                    <span className="home-activity-actor">{entry.actorName}</span>
                  )}
                  <time className="home-activity-time" dateTime={entry.occurredAt}>
                    {formatRelativeTime(entry.occurredAt)}
                  </time>
                </span>
              </>
            );

            return (
              <li key={entry.entryKey} className={`home-activity-item ${statusClass}`}>
                <span className={`home-activity-icon-wrap ${statusClass}`} aria-hidden="true">
                  <KpiStatusIcon semantic={entry.statusSemantic} />
                </span>
                {entry.navigationTarget ? (
                  <button
                    type="button"
                    data-entry={entry.entryKey}
                    className="home-activity-body home-activity-body-btn"
                    onClick={() => onNavigate(entry.navigationTarget!)}
                  >
                    {content}
                  </button>
                ) : (
                  <div className="home-activity-body">{content}</div>
                )}
              </li>
            );
          })}
        </ol>
      </div>
    </section>
  );
}
