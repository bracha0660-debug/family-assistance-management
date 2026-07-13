const ICON_PROPS = {
  className: 'home-financial-metric-icon',
  viewBox: '0 0 24 24',
  fill: 'currentColor',
  'aria-hidden': true as const,
};

export function FinancialMetricIcon({ metricKey }: { metricKey: string }) {
  switch (metricKey) {
    case 'approved_this_month':
      return (
        <svg {...ICON_PROPS}>
          <path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z" />
        </svg>
      );
    case 'paid_this_month':
      return (
        <svg {...ICON_PROPS}>
          <path d="M20 4.5H4A2.5 2.5 0 0 0 1.5 7v10A2.5 2.5 0 0 0 4 19.5h16a2.5 2.5 0 0 0 2.5-2.5V7A2.5 2.5 0 0 0 20 4.5zM4 7.5h16V9H4V7.5zm0 3.5h16v6.5H4V11z" />
        </svg>
      );
    case 'awaiting_execution':
      // Same glyph as home KPI pending_execution (ממתין לתשלום)
      return (
        <svg {...ICON_PROPS}>
          <path d="M20 4.5H4A2.5 2.5 0 0 0 1.5 7v10A2.5 2.5 0 0 0 4 19.5h16a2.5 2.5 0 0 0 2.5-2.5V7A2.5 2.5 0 0 0 20 4.5zM4 7.5h16V9H4V7.5zm0 3.5h16v6.5H4V11z" />
        </svg>
      );
    case 'suspended':
      return (
        <svg {...ICON_PROPS}>
          <path d="M8 5.5h3.5v13H8v-13zm4.5 0H16v13h-3.5v-13z" />
        </svg>
      );
    default:
      return (
        <svg {...ICON_PROPS}>
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z" />
        </svg>
      );
  }
}
