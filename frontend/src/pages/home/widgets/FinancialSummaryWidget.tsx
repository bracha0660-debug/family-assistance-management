import type { HomeFinancialMetric, HomeNavigationTarget } from '../../../api/workflow';
import { formatGeneratedAt, formatIls } from '../formatIls';
import { statusSemanticCardClass } from '../workflowStatus';
import { FinancialMetricIcon } from './FinancialMetricIcon';

interface FinancialSummaryWidgetProps {
  title: string;
  metrics: HomeFinancialMetric[];
  generatedAt: string;
  onNavigate: (target: HomeNavigationTarget) => void;
}

export function FinancialSummaryWidget({
  title,
  metrics,
  generatedAt,
  onNavigate,
}: FinancialSummaryWidgetProps) {
  if (metrics.length === 0) return null;

  return (
    <section className="home-widget home-financial-widget" aria-label="תמונת מצב כספית">
      <div className="home-panel home-financial-panel">
        {title && <h2 className="home-widget-title">{title}</h2>}
        <div className="home-financial-metrics">
          {metrics.map((metric) => {
            const statusClass = statusSemanticCardClass(metric.statusSemantic);
            const content = (
              <>
                <div className={`home-financial-metric-icon-wrap ${statusClass}`} aria-hidden="true">
                  <FinancialMetricIcon metricKey={metric.metricKey} />
                </div>
                <div className="home-financial-metric-body">
                  <span className="home-financial-metric-label">{metric.title}</span>
                  <span className={`home-financial-metric-value home-status-metric ${statusClass}`}>
                    {formatIls(metric.amount)}
                  </span>
                </div>
              </>
            );

            if (metric.navigationTarget) {
              return (
                <button
                  key={metric.metricKey}
                  type="button"
                  className={`home-financial-metric home-financial-metric-btn ${statusClass}`}
                  onClick={() => onNavigate(metric.navigationTarget!)}
                >
                  {content}
                </button>
              );
            }

            return (
              <div key={metric.metricKey} className={`home-financial-metric ${statusClass}`}>
                {content}
              </div>
            );
          })}
        </div>
        <p className="home-financial-footer">
          הנתונים נכונים ל-{formatGeneratedAt(generatedAt)}
        </p>
      </div>
    </section>
  );
}
