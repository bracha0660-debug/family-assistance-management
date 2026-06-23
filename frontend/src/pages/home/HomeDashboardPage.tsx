import { useCallback, useEffect, useState } from 'react';
import {
  getWorkflowDashboard,
  parseFinancialSummaryWidget,
  parseKpiCardsWidget,
  type HomeNavigationTarget,
  type HomeWidget,
} from '../../api/workflow';
import { FinancialSummaryWidget } from './widgets/FinancialSummaryWidget';
import { KpiCardsWidget } from './widgets/KpiCardsWidget';

interface HomeDashboardPageProps {
  onNavigate: (target: HomeNavigationTarget) => void;
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

  return (
    <div className="home-dashboard-page">
      <header className="home-dashboard-header">
        <h1 className="home-dashboard-title">מסך הבית</h1>
        <p className="home-dashboard-subtitle">תמונת מצב עדכנית של הפעילות בארגון</p>
      </header>

      {widgets.map((widget) => {
        if (widget.type === 'kpi_cards') {
          const cards = parseKpiCardsWidget(widget);
          return (
            <KpiCardsWidget
              key={widget.id}
              cards={cards}
              onNavigate={onNavigate}
            />
          );
        }
        if (widget.type === 'financial_summary') {
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
        }
        return null;
      })}
    </div>
  );
}
