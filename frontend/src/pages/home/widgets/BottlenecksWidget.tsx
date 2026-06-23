import type { HomeBottleneckAlert, HomeNavigationTarget } from '../../../api/workflow';
import { statusSemanticCardClass, statusSemanticLabel } from '../workflowStatus';
import { KpiStatusIcon } from './KpiStatusIcon';

interface BottlenecksWidgetProps {
  title: string;
  alerts: HomeBottleneckAlert[];
  onNavigate: (target: HomeNavigationTarget) => void;
}

export function BottlenecksWidget({ title, alerts, onNavigate }: BottlenecksWidgetProps) {
  if (alerts.length === 0) return null;

  return (
    <section className="home-widget home-bottlenecks-widget" aria-label="צווארי בקבוק">
      <div className="home-panel home-bottlenecks-panel">
        {title && (
          <div className="home-widget-section-header">
            <h2 className="home-widget-section-title">{title}</h2>
          </div>
        )}
        <ul className="home-bottlenecks-list">
          {alerts.map((alert) => {
            const statusClass = statusSemanticCardClass(alert.statusSemantic);
            const statusLabel = statusSemanticLabel(alert.statusSemantic);

            return (
              <li key={alert.alertKey}>
                <button
                  type="button"
                  data-alert={alert.alertKey}
                  className={`home-bottleneck-item ${statusClass}`}
                  onClick={() => onNavigate(alert.navigationTarget)}
                >
                  <span className="home-bottleneck-count" aria-label={`${alert.count} פריטים`}>
                    {alert.count}
                  </span>
                  <div className="home-bottleneck-body">
                    <span className={`home-bottleneck-status-label ${statusClass}`}>{statusLabel}</span>
                    <span className="home-bottleneck-title">{alert.title}</span>
                    <span className="home-bottleneck-description">{alert.description}</span>
                  </div>
                  <span className="home-bottleneck-icon-wrap" aria-hidden="true">
                    <KpiStatusIcon semantic={alert.statusSemantic} />
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
  );
}
