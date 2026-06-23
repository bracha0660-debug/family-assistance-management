import type { HomeKpiCard, HomeNavigationTarget } from '../../../api/workflow';
import { statusSemanticCardClass } from '../workflowStatus';
import { KpiStatusIcon } from './KpiStatusIcon';

interface KpiCardsWidgetProps {
  cards: HomeKpiCard[];
  onNavigate: (target: HomeNavigationTarget) => void;
}

export function KpiCardsWidget({ cards, onNavigate }: KpiCardsWidgetProps) {
  if (cards.length === 0) return null;

  return (
    <section className="home-widget home-kpi-widget" aria-label="מדדי תפעול">
      <div className="home-kpi-grid">
        {cards.map((card) => (
          <article
            key={card.kpiKey}
            className={`home-kpi-card home-status-card ${statusSemanticCardClass(card.statusSemantic)}`}
          >
            <div className="home-kpi-card-icon-wrap" aria-hidden="true">
              <KpiStatusIcon semantic={card.statusSemantic} />
            </div>
            <div className="home-kpi-card-body">
              <p className="home-kpi-card-count">{card.count}</p>
              <h3 className="home-kpi-card-title">{card.title}</h3>
              <p className="home-kpi-card-subtitle">{card.subtitle}</p>
            </div>
            <button
              type="button"
              className="home-kpi-card-link"
              onClick={() => onNavigate(card.navigationTarget)}
            >
              לצפייה ›
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}
