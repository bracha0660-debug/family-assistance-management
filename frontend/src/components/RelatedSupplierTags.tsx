import type { RelatedSupplierDto } from '../api/assistanceTypes';

export type RelatedSupplierRef = RelatedSupplierDto;

interface RelatedSupplierTagsProps {
  suppliers: RelatedSupplierRef[];
  editable?: boolean;
  onRemove?: (supplierId: string) => void;
  disabled?: boolean;
  emptyLabel?: string;
  compact?: boolean;
}

export function RelatedSupplierTags({
  suppliers,
  editable = false,
  onRemove,
  disabled = false,
  emptyLabel = '—',
  compact = false,
}: RelatedSupplierTagsProps) {
  if (suppliers.length === 0) {
    return <span className="hint-text">{emptyLabel}</span>;
  }

  const containerClass = compact
    ? 'related-supplier-tags related-supplier-tags--compact'
    : 'related-supplier-tags';

  return (
    <div className={containerClass}>
      {suppliers.map((supplier) => (
        <span key={supplier.id} className="related-supplier-tag">
          <span className="related-supplier-tag-label">{supplier.name}</span>
          {editable && onRemove && (
            <button
              type="button"
              className="related-supplier-tag-remove"
              onClick={() => onRemove(supplier.id)}
              disabled={disabled}
              aria-label={`הסר ${supplier.name}`}
            >
              ×
            </button>
          )}
        </span>
      ))}
    </div>
  );
}
