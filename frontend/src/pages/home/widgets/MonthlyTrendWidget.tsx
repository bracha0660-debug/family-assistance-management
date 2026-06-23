import type { HomeMonthlyTrendPoint } from '../../../api/workflow';
import { formatIls } from '../formatIls';

interface MonthlyTrendWidgetProps {
  title: string;
  subtitle: string;
  points: HomeMonthlyTrendPoint[];
}

const CHART_WIDTH = 400;
const CHART_HEIGHT = 132;
const PADDING_X = 14;
const PADDING_TOP = 16;
const PADDING_BOTTOM = 24;

function formatTrendLabel(amount: number): string {
  if (amount >= 1_000_000) {
    return `₪${(amount / 1_000_000).toLocaleString('he-IL', { maximumFractionDigits: 1 })}M`;
  }
  if (amount >= 1_000) {
    return `₪${Math.round(amount / 1_000).toLocaleString('he-IL')}K`;
  }
  return formatIls(amount);
}

export function MonthlyTrendWidget({ title, subtitle, points }: MonthlyTrendWidgetProps) {
  if (points.length === 0) return null;

  const maxAmount = Math.max(...points.map((p) => p.amount), 1);
  const plotWidth = CHART_WIDTH - PADDING_X * 2;
  const plotHeight = CHART_HEIGHT - PADDING_TOP - PADDING_BOTTOM;
  const stepX = points.length > 1 ? plotWidth / (points.length - 1) : 0;

  const coordinates = points.map((point, index) => {
    const x = PADDING_X + stepX * index;
    const ratio = point.amount / maxAmount;
    const y = PADDING_TOP + plotHeight - ratio * plotHeight;
    return { point, x, y };
  });

  const polyline = coordinates.map(({ x, y }) => `${x},${y}`).join(' ');

  return (
    <section className="home-widget home-trend-widget" aria-label="מגמה חודשית">
      <div className="home-panel home-trend-panel">
        <div className="home-widget-section-header home-trend-header">
          {title && <h2 className="home-widget-section-title">{title}</h2>}
          {subtitle && <p className="home-widget-section-subtitle">{subtitle}</p>}
        </div>
        <div className="home-trend-chart-wrap">
          <svg
            className="home-trend-chart"
            viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
            role="img"
            aria-label={`${title}: ${subtitle}`}
          >
            <line
              x1={PADDING_X}
              y1={PADDING_TOP + plotHeight}
              x2={CHART_WIDTH - PADDING_X}
              y2={PADDING_TOP + plotHeight}
              className="home-trend-axis"
            />
            <polyline
              points={polyline}
              className="home-trend-line"
              fill="none"
            />
            {coordinates.map(({ point, x, y }) => (
              <g key={point.monthKey}>
                <text
                  x={x}
                  y={y - 8}
                  className="home-trend-value-label"
                  textAnchor="middle"
                >
                  {formatTrendLabel(point.amount)}
                </text>
                <circle cx={x} cy={y} r="4" className="home-trend-point" />
                <text
                  x={x}
                  y={CHART_HEIGHT - 8}
                  className="home-trend-month-label"
                  textAnchor="middle"
                >
                  {point.labelHe}
                </text>
              </g>
            ))}
          </svg>
        </div>
      </div>
    </section>
  );
}
