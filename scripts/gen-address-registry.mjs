import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const data = [
  {
    name: '\u05EA\u05DC \u05D0\u05D1\u05D9\u05D1-\u05D9\u05E4\u05D5',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D3\u05D9\u05D6\u05E0\u05D2\u05D5\u05E3',
      '\u05E9\u05D3\u05E8\u05D5\u05EA \u05E8\u05D5\u05D8\u05E9\u05D9\u05DC\u05D3',
      '\u05D0\u05DC\u05E0\u05D1\u05D9',
      '\u05D1\u05DF \u05D9\u05D4\u05D5\u05D3\u05D4',
      '\u05D0\u05E8\u05DC\u05D5\u05D6\u05D5\u05E8\u05D5\u05D1',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D1\u05D9\u05D0\u05DC\u05D9\u05E7',
      '\u05D0\u05D1\u05DF \u05D2\u05D1\u05D9\u05E8\u05D5\u05DC',
      '\u05D4\u05DE\u05DC\u05DA \u05D2\u05D0\u05D5\u05E8\u05D2',
    ],
  },
  {
    name: '\u05D9\u05E8\u05D5\u05E9\u05DC\u05D9\u05DD',
    streets: [
      '\u05D9\u05E4\u05D5',
      '\u05D4\u05DE\u05DC\u05DA \u05D2\u05D0\u05D5\u05E8\u05D2',
      '\u05D1\u05DF \u05D9\u05D4\u05D5\u05D3\u05D4',
      '\u05E2\u05D6\u05D4',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05E9\u05DE\u05D5\u05D0\u05DC \u05D4\u05E0\u05D1\u05D9\u05D0',
      '\u05D1\u05E6\u05DC\u05D0\u05DC',
      '\u05D4\u05E8\u05D1 \u05DE\u05D0\u05D9\u05E8',
      '\u05D0\u05D2\u05E8\u05D5\u05DF',
      '\u05D4\u05E0\u05D1\u05D9\u05D0\u05D9\u05DD',
    ],
  },
  {
    name: '\u05D7\u05D9\u05E4\u05D4',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D4\u05E0\u05E9\u05D9\u05D0',
      '\u05D4\u05D7\u05DC\u05D5\u05E5',
      '\u05D4\u05E0\u05D1\u05D9\u05D0\u05D9\u05DD',
      '\u05DE\u05D5\u05E8\u05D9\u05E1',
      '\u05D1\u05DC\u05E4\u05D5\u05E8',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D4\u05D2\u05E4\u05DF',
      '\u05E9\u05D3\u05E8\u05D5\u05EA \u05D4\u05E0\u05E9\u05D9\u05D0',
      '\u05D4\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA',
    ],
  },
  {
    name: '\u05E8\u05D0\u05E9\u05D5\u05DF \u05DC\u05E6\u05D9\u05D5\u05DF',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05E8\u05D5\u05D8\u05E9\u05D9\u05DC\u05D3',
      '\u05D9\u05E8\u05D5\u05E9\u05DC\u05D9\u05DD',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D1\u05D9\u05D0\u05DC\u05D9\u05E7',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
    ],
  },
  {
    name: '\u05E4\u05EA\u05D7 \u05EA\u05E7\u05D5\u05D5\u05D4',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D1\u05DC\u05E4\u05D5\u05E8',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D7\u05D9\u05D9\u05DD \u05D0\u05D1\u05D9\u05D1',
      '\u05D0\u05D9\u05D9\u05DC\u05D5\u05DF',
    ],
  },
  {
    name: '\u05D7\u05D5\u05DC\u05D5\u05DF',
    streets: [
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D9\u05D5\u05E1\u05E3 \u05E7\u05DC\u05D5\u05E1\u05E0\u05E8',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D2\u05D5\u05DC\u05D3\u05D4 \u05DE\u05D9\u05D9\u05E8',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D1\u05D9\u05D0\u05DC\u05D9\u05E7',
    ],
  },
  {
    name: '\u05D1\u05D0\u05E8 \u05E9\u05D1\u05E2',
    streets: [
      '\u05E8\u05D2\u05E8',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D1\u05DC\u05E4\u05D5\u05E8',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D1\u05DF \u05D2\u05D5\u05E8\u05D9\u05D5\u05DF',
    ],
  },
  {
    name: '\u05E0\u05EA\u05E0\u05D9\u05D4',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D1\u05DC\u05E4\u05D5\u05E8',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D1\u05DF \u05E2\u05DE\u05D9',
      '\u05D4\u05E9\u05D5\u05E7',
    ],
  },
  {
    name: '\u05E8\u05DE\u05EA \u05D2\u05DF',
    streets: [
      '\u05D1\u05D9\u05D0\u05DC\u05D9\u05E7',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D0\u05D9\u05D1\u05D0 \u05D7\u05DF',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
    ],
  },
  {
    name: '\u05D0\u05E9\u05D3\u05D5\u05D3',
    streets: [
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E1\u05D5\u05E7\u05D5\u05DC\u05D5\u05D1',
      '\u05D4\u05D4\u05D2\u05E0\u05D4',
      '\u05D1\u05DC\u05E4\u05D5\u05E8',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D0\u05D7\u05D3 \u05D4\u05E2\u05DD',
      '\u05D4\u05E8\u05D1 \u05D9\u05D4\u05D5\u05D3\u05D4 \u05D4\u05E9\u05E0\u05D9',
      '\u05D4\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA',
    ],
  },
  {
    name: '\u05D1\u05E0\u05D9 \u05D1\u05E8\u05E7',
    streets: [
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D5\u05D9\u05E6\u05DE\u05DF',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05E8\u05D1\u05DF \u05D2\u05D5\u05E8\u05D9\u05D5\u05DF',
      '\u05D0\u05D1\u05DF \u05D2\u05D1\u05D9\u05E8\u05D5\u05DC',
      '\u05D7\u05D6\u05DF',
      '\u05D0\u05D9\u05D9\u05DC\u05D5\u05DF',
      '\u05D4\u05E8\u05D1 \u05D9\u05D4\u05D5\u05D3\u05D4',
      '\u05D1\u05D9\u05EA \u05D4\u05E8\u05D1',
    ],
  },
  {
    name: '\u05DE\u05D5\u05D3\u05D9\u05E2\u05D9\u05DF-\u05DE\u05DB\u05D1\u05D9\u05DD-\u05E8\u05E2\u05D5\u05EA',
    streets: [
      '\u05D0\u05D9\u05D9\u05DC\u05D5\u05DF \u05D4\u05E0\u05E9\u05D9\u05D0',
      '\u05D4\u05E8\u05E6\u05DC',
      '\u05D6\u05D0\u05D1\u05D5\u05D8\u05D9\u05E0\u05E1\u05E7\u05D9',
      '\u05D4\u05D4\u05E1\u05EA\u05D3\u05E8\u05D5\u05EA',
      '\u05D4\u05E0\u05E9\u05D9\u05D0',
      '\u05D1\u05DF \u05E9\u05DE\u05D0\u05D9',
      '\u05D4\u05E9\u05D5\u05E7',
      '\u05D4\u05E2\u05DE\u05E7',
      '\u05D4\u05E8\u05D1 \u05D4\u05E8\u05D9\u05E5',
      '\u05D4\u05E8\u05D1 \u05D4\u05D2\u05D3\u05D5\u05DC',
    ],
  },
];

const header = `/**
 * Israeli locality + street registry (UI lookup).
 *
 * Recommended production data sources:
 * - data.gov.il — CBS / Ministry of Interior locality & street open datasets
 * - Israel Post — official but no public free REST API
 *
 * Bundled subset for typeahead without external API keys.
 */

export interface LocalityEntry {
  name: string;
  streets: readonly string[];
}

export const LOCALITY_REGISTRY: readonly LocalityEntry[] = ${JSON.stringify(data, null, 2)};

export function localityNames(): string[] {
  return LOCALITY_REGISTRY.map((l) => l.name);
}

export function streetsForLocality(city: string): string[] {
  const entry = LOCALITY_REGISTRY.find((l) => l.name === city);
  return entry ? [...entry.streets] : [];
}

export function filterLocalities(query: string, limit = 12): string[] {
  const q = query.trim();
  if (!q) return localityNames().slice(0, limit);
  return localityNames()
    .filter((name) => name.includes(q))
    .slice(0, limit);
}

export function filterStreets(city: string, query: string, limit = 15): string[] {
  const streets = streetsForLocality(city);
  const q = query.trim();
  if (!q) return streets.slice(0, limit);
  return streets.filter((s) => s.includes(q)).slice(0, limit);
}

export function isKnownLocality(city: string): boolean {
  return LOCALITY_REGISTRY.some((l) => l.name === city);
}

export function isKnownStreet(city: string, street: string): boolean {
  return streetsForLocality(city).includes(street);
}
`;

const outPath = path.join(__dirname, '..', 'frontend', 'src', 'data', 'israeliAddressRegistry.ts');
fs.writeFileSync(outPath, header, 'utf8');
console.log('Wrote', outPath);
