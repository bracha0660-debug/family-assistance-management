export interface StructuredAddress {
  city: string;
  street: string;
  houseNumber: string;
  apartment: string;
  entrance: string;
  floor: string;
}

export const EMPTY_STRUCTURED_ADDRESS: StructuredAddress = {
  city: '',
  street: '',
  houseNumber: '',
  apartment: '',
  entrance: '',
  floor: '',
};

const KEY_CITY = '\u05E2\u05D9\u05E8';
const KEY_STREET = '\u05E8\u05D7\u05D5\u05D1';
const KEY_HOUSE = '\u05DE\u05E1\u05E4\u05E8';
const KEY_APT = '\u05D3\u05D9\u05E8\u05D4';
const KEY_ENTRANCE = '\u05DB\u05E0\u05D9\u05E1\u05D4';
const KEY_FLOOR = '\u05E7\u05D5\u05DE\u05D4';

function partValue(parts: Map<string, string>, key: string): string {
  return parts.get(key) ?? '';
}

export function parseFamilyAddress(raw: string | null | undefined): StructuredAddress {
  if (!raw?.trim()) return { ...EMPTY_STRUCTURED_ADDRESS };

  const trimmed = raw.trim();
  if (!trimmed.includes(':')) {
    return { ...EMPTY_STRUCTURED_ADDRESS, city: trimmed };
  }

  const parts = new Map<string, string>();
  for (const segment of trimmed.split(';')) {
    const idx = segment.indexOf(':');
    if (idx <= 0) continue;
    const key = segment.slice(0, idx).trim();
    const value = segment.slice(idx + 1).trim();
    if (key.length > 0 && value.length > 0) {
      parts.set(key, value);
    }
  }

  return {
    city: partValue(parts, KEY_CITY),
    street: partValue(parts, KEY_STREET),
    houseNumber: partValue(parts, KEY_HOUSE),
    apartment: partValue(parts, KEY_APT),
    entrance: partValue(parts, KEY_ENTRANCE),
    floor: partValue(parts, KEY_FLOOR),
  };
}

export function formatFamilyAddress(address: StructuredAddress): string | null {
  const segments: string[] = [];
  if (address.city.trim()) segments.push(`${KEY_CITY}: ${address.city.trim()}`);
  if (address.street.trim()) segments.push(`${KEY_STREET}: ${address.street.trim()}`);
  if (address.houseNumber.trim()) segments.push(`${KEY_HOUSE}: ${address.houseNumber.trim()}`);
  if (address.apartment.trim()) segments.push(`${KEY_APT}: ${address.apartment.trim()}`);
  if (address.entrance.trim()) segments.push(`${KEY_ENTRANCE}: ${address.entrance.trim()}`);
  if (address.floor.trim()) segments.push(`${KEY_FLOOR}: ${address.floor.trim()}`);

  if (segments.length === 0) return null;
  const formatted = segments.join('; ');
  return formatted.length > 300 ? formatted.slice(0, 300) : formatted;
}

export function hasAddressInput(address: StructuredAddress): boolean {
  return Object.values(address).some((v) => v.trim().length > 0);
}
