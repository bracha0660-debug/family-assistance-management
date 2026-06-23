import { useCallback, useEffect, useState } from 'react';
import {
  getWorkflowDashboard,
  parseBottlenecksWidget,
  parseFinancialSummaryWidget,
  parseKpiCardsWidget,
  parseMonthlyTrendWidget,
  parseRecentActivityWidget,
  type HomeNavigationTarget,
  type HomeWidget,
} from '../../api/workflow';
import { BottlenecksWidget } from './widgets/BottlenecksWidget';
import { FinancialSummaryWidget } from './widgets/FinancialSummaryWidget';
import { KpiCardsWidget } from './widgets/KpiCardsWidget';
import { MonthlyTrendWidget } from './widgets/MonthlyTrendWidget';
import { RecentActivityWidget } from './widgets/RecentActivityWidget';

interface HomeDashboardPageProps {
  onNavigate: (target: HomeNavigationTarget) => void;
}

function renderBottomWidget(
  widget: HomeWidget,
  onNavigate: (target: HomeNavigationTarget) => void,
) {
  if (widget.type === 'bottlenecks') {
    const alerts = parseBottlenecksWidget(widget);
    return (
      <BottlenecksWidget
        key={widget.id}
        title={widget.title}
        alerts={alerts}
        onNavigate={onNavigate}
      />
    );
  }
  if (widget.type === 'monthly_trend') {
    const trend = parseMonthlyTrendWidget(widget);
    if (!trend) return null;
    return (
      <MonthlyTrendWidget
        key={widget.id}
        title={widget.title}
        subtitle={trend.subtitle}
        points={trend.points}
      />
    );
  }
  if (widget.type === 'recent_activity') {
    const entries = parseRecentActivityWidget(widget);
    return (
      <RecentActivityWidget
        key={widget.id}
        title={widget.title}
        entries={entries}
        onNavigate={onNavigate}
      />
    );
  }
  return null;
}

export function HomeDashboardPage({ onNavigate }: HomeDashboardPageProps) {
  const [widgets, setWidgets] = useState<HomeWidget[]>([]);
  const [generatedAt, setGeneratedAt] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setError('');
    try {
      const data = await getWorkflowDashboard();
      setWidgets(data.home.widgets);
      setGeneratedAt(data.home.generatedAt);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return <p className="home-dashboard-loading">טוען...</p>;
  }

  if (error) {
    return <p className="error" role="alert">{error}</p>;
  }

  const kpiWidgets = widgets.filter((w) => w.type === 'kpi_cards');
  const financialWidgets = widgets.filter((w) => w.type === 'financial_summary');
  const bottomWidgets = widgets.filter((w) =>
    w.type === 'monthly_trend' || w.type === 'bottlenecks' || w.type === 'recent_activity',
  );

  return (
    <div className="home-dashboard-page">
      <header className="home-dashboard-header">
        <h1 className="home-dashboard-title">מסך הבית</h1>
        <p className="home-dashboard-subtitle">תמונת מצב עדכנית של הפעילות בארגון</p>
      </header>

      {kpiWidgets.length > 0 && (
        <div className="home-dashboard-row home-dashboard-row-kpi">
          {kpiWidgets.map((widget) => {
            const cards = parseKpiCardsWidget(widget);
            return (
              <KpiCardsWidget
                key={widget.id}
                cards={cards}
                onNavigate={onNavigate}
              />
            );
          })}
        </div>
      )}

      {financialWidgets.length > 0 && (
        <div className="home-dashboard-row home-dashboard-row-financial">
          {financialWidgets.map((widget) => {
            const metrics = parseFinancialSummaryWidget(widget);
            return (
              <FinancialSummaryWidget
                key={widget.id}
                title={widget.title}
                metrics={metrics}
                generatedAt={generatedAt}
                onNavigate={onNavigate}
              />
            );
          })}
        </div>
      )}

      {bottomWidgets.length > 0 && (
        <div className="home-dashboard-row home-dashboard-bottom-grid">
          {bottomWidgets.map((widget) => (
            <div key={widget.id} className={`home-dashboard-bottom-cell home-dashboard-bottom-cell-${widget.type}`}>
              {renderBottomWidget(widget, onNavigate)}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
