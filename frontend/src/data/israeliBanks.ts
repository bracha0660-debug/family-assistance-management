export interface IsraeliBank {
  number: string;
  name: string;
}

/** Israeli bank codes for UI sync (number ↔ name). */
export const ISRAELI_BANKS: readonly IsraeliBank[] = [
  { number: '4', name: 'בנק יהב לעובדי המדינה בע"מ' },
  { number: '9', name: 'בנק הדואר' },
  { number: '10', name: 'בנק לאומי לישראל בע"מ' },
  { number: '11', name: 'בנק דיסקונט לישראל בע"מ' },
  { number: '12', name: 'בנק הפועלים בע"מ' },
  { number: '13', name: 'בנק אגוד לישראל בע"מ' },
  { number: '14', name: 'בנק אוצר החייל בע"מ' },
  { number: '17', name: 'בנק מרכנתיל דיסקונט בע"מ' },
  { number: '20', name: 'בנק מזרחי טפחות בע"מ' },
  { number: '31', name: 'בנק הבינלאומי הראשון לישראל בע"מ' },
  { number: '46', name: 'בנק מסד בע"מ' },
  { number: '52', name: 'בנק פועלי אגודת ישראל בע"מ' },
  { number: '54', name: 'בנק ירושלים בע"מ' },
];

function normalizeBankNumber(value: string): string {
  const digits = value.replace(/\D/g, '');
  if (digits.length === 0) return '';
  return String(parseInt(digits, 10));
}

export function findBankByNumber(bankNumber: string): IsraeliBank | undefined {
  const normalized = normalizeBankNumber(bankNumber);
  if (!normalized) return undefined;
  return ISRAELI_BANKS.find((b) => b.number === normalized);
}

export function findBankByName(bankName: string): IsraeliBank | undefined {
  const trimmed = bankName.trim();
  if (!trimmed) return undefined;
  return ISRAELI_BANKS.find((b) => b.name === trimmed);
}

export function isKnownBankName(bankName: string): boolean {
  return findBankByName(bankName) !== undefined;
}

export function filterBanks(query: string): IsraeliBank[] {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) return [...ISRAELI_BANKS];
  return ISRAELI_BANKS.filter(
    (bank) => bank.name.toLowerCase().includes(trimmed) || bank.number.includes(trimmed),
  );
}

export function isKnownBankNumber(bankNumber: string): boolean {
  return findBankByNumber(bankNumber) !== undefined;
}
