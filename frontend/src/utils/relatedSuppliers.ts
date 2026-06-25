import type { AssistanceTypeDto } from '../api/assistanceTypes';
import type { SupplierDto } from '../api/suppliers';

export interface PartitionedSuppliers {
  recommended: SupplierDto[];
  other: SupplierDto[];
}

export function partitionSuppliersForAssistanceType(
  types: AssistanceTypeDto[],
  suppliers: SupplierDto[],
  assistanceTypeId: string,
): PartitionedSuppliers {
  const active = suppliers.filter((s) => s.status === 'active');
  if (!assistanceTypeId) {
    return { recommended: [], other: active };
  }

  const selectedType = types.find((t) => t.id === assistanceTypeId);
  const recommendedIds = new Set(
    (selectedType?.relatedSuppliers ?? []).map((r) => r.id),
  );

  return {
    recommended: active.filter((s) => recommendedIds.has(s.id)),
    other: active.filter((s) => !recommendedIds.has(s.id)),
  };
}
