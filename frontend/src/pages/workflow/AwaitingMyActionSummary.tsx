import type { AwaitingMyActionSummary } from '../../api/workflow';

interface AwaitingMyActionSummaryProps {
  summary: AwaitingMyActionSummary;
  onSectionClick?: (sectionId: string) => void;
}

export function AwaitingMyActionSummaryPanel({ summary, onSectionClick }: AwaitingMyActionSummaryProps) {
  if (summary.totalAwaitingMyAction === 0) {
    return (
      <section className="workflow-awaiting-summary" aria-label="ממתין לטיפול שלי">
        <h2>ממתין לטיפול שלי</h2>
        <p className="empty-row">אין פריטים הממתינים לטיפולך</p>
      </section>
    );
  }

  return (
    <section className="workflow-awaiting-summary" aria-label="ממתין לטיפול שלי">
      <div className="workflow-awaiting-header">
        <h2>ממתין לטיפול שלי</h2>
        <span className="workflow-awaiting-badge">{summary.totalAwaitingMyAction}</span>
      </div>
      <div className="workflow-awaiting-chips">
        {summary.bySection.map((item) => (
          <button
            key={item.sectionId}
            type="button"
            className="workflow-chip"
            onClick={() => onSectionClick?.(item.sectionId)}
          >
            {item.title} ({item.count})
          </button>
        ))}
      </div>
    </section>
  );
}
