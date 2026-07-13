import type { WorkflowStatusSemantic } from '../workflowStatus';

const ICON_PROPS = {
  className: 'home-kpi-card-icon',
  viewBox: '0 0 24 24',
  fill: 'currentColor',
  'aria-hidden': true as const,
};

/** KPI status icons — bold flat glyphs matching the approved dashboard reference. */
export function KpiStatusIcon({ semantic }: { semantic: string }) {
  switch (semantic as WorkflowStatusSemantic) {
    case 'draft':
      return (
        <svg {...ICON_PROPS}>
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm-1 1.5L18.5 9H15a1 1 0 0 1-1-1V3.5zM8 10.5h8v2H8v-2zm0 4h8v2H8v-2zm0-7h4.5v3H8V7.5z" />
        </svg>
      );
    case 'pending_approval':
      return (
        <svg {...ICON_PROPS}>
          <path d="M12 2a10 10 0 1 0 10 10A10.011 10.011 0 0 0 12 2zm0 2a8 8 0 1 1 0 16 8 8 0 0 1 0-16zm-.75 3v5.2l4.4 2.6.95-1.6-3.6-2.1V7h-1.75z" />
        </svg>
      );
    case 'returned_for_treatment':
      return (
        <svg {...ICON_PROPS}>
          <path d="M12 4.5V2L6.5 7.5 12 13V9.5c2.76 0 5 2.24 5 5s-2.24 5-5 5-5-2.24-5-5H6c0 3.87 3.13 7 7 7s7-3.13 7-7-3.13-7-7-7z" />
        </svg>
      );
    case 'on_hold':
      return (
        <svg {...ICON_PROPS}>
          <path d="M8 5.5h3.5v13H8v-13zm4.5 0H16v13h-3.5v-13z" />
        </svg>
      );
    case 'pending_execution':
      return (
        <svg {...ICON_PROPS}>
          <path d="M20 4.5H4A2.5 2.5 0 0 0 1.5 7v10A2.5 2.5 0 0 0 4 19.5h16a2.5 2.5 0 0 0 2.5-2.5V7A2.5 2.5 0 0 0 20 4.5zM4 7.5h16V9H4V7.5zm0 3.5h16v6.5H4V11z" />
        </svg>
      );
    case 'paid':
      return (
        <svg {...ICON_PROPS}>
          <path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z" />
        </svg>
      );
    case 'approved':
      return (
        <svg {...ICON_PROPS}>
          <path d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z" />
        </svg>
      );
    case 'completed':
      return (
        <svg {...ICON_PROPS}>
          <path d="M12 2a10 10 0 1 0 10 10A10.011 10.011 0 0 0 12 2zm-1.1 14.2-3.6-3.6 1.4-1.4 2.2 2.2 4.6-4.6 1.4 1.4-6 6z" />
        </svg>
      );
    case 'rejected':
      return (
        <svg {...ICON_PROPS}>
          <path d="M12 2a10 10 0 1 0 10 10A10.011 10.011 0 0 0 12 2zm3.5 12.8-1.4 1.4L12 13.4l-2.1 2.1-1.4-1.4 2.1-2.1-2.1-2.1 1.4-1.4 2.1 2.1 2.1-2.1 1.4 1.4-2.1 2.1 2.1 2.1z" />
        </svg>
      );
    default:
      return (
        <svg {...ICON_PROPS}>
          <path d="M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z" />
        </svg>
      );
  }
}
