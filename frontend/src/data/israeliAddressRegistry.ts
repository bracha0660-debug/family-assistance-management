/**
 * Israeli locality + street registry (optional UI suggestions only).
 *
 * Not a live official feed — city/street fields accept free text.
 * Future production sources if wired via backend proxy:
 * - data.gov.il — CBS / Ministry of Interior locality & street open datasets
 * - Israel Post — official but no public free REST API
 *
 * Bundled subset for typeahead without external API keys.
 */

export interface LocalityEntry {
  name: string;
  streets: readonly string[];
}

export const LOCALITY_REGISTRY: readonly LocalityEntry[] = [
  {
    "name": "תל אביב-יפו",
    "streets": [
      "הרצל",
      "דיזנגוף",
      "שדרות רוטשילד",
      "אלנבי",
      "בן יהודה",
      "ארלוזורוב",
      "ויצמן",
      "ביאליק",
      "אבן גבירול",
      "המלך גאורג"
    ]
  },
  {
    "name": "ירושלים",
    "streets": [
      "יפו",
      "המלך גאורג",
      "בן יהודה",
      "עזה",
      "הרצל",
      "שמואל הנביא",
      "בצלאל",
      "הרב מאיר",
      "אגרון",
      "הנביאים"
    ]
  },
  {
    "name": "חיפה",
    "streets": [
      "הרצל",
      "הנשיא",
      "החלוץ",
      "הנביאים",
      "מוריס",
      "בלפור",
      "ויצמן",
      "הגפן",
      "שדרות הנשיא",
      "העצמאות"
    ]
  },
  {
    "name": "ראשון לציון",
    "streets": [
      "הרצל",
      "רוטשילד",
      "ירושלים",
      "ויצמן",
      "סוקולוב",
      "זאבוטינסקי",
      "אחד העם",
      "ביאליק",
      "ההסתדרות",
      "ההגנה"
    ]
  },
  {
    "name": "פתח תקווה",
    "streets": [
      "הרצל",
      "ויצמן",
      "זאבוטינסקי",
      "אחד העם",
      "ההסתדרות",
      "סוקולוב",
      "בלפור",
      "ההגנה",
      "חיים אביב",
      "איילון"
    ]
  },
  {
    "name": "חולון",
    "streets": [
      "ויצמן",
      "יוסף קלוסנר",
      "ההסתדרות",
      "סוקולוב",
      "ההגנה",
      "גולדה מייר",
      "הרצל",
      "זאבוטינסקי",
      "אחד העם",
      "ביאליק"
    ]
  },
  {
    "name": "באר שבע",
    "streets": [
      "רגר",
      "הרצל",
      "ויצמן",
      "ההסתדרות",
      "סוקולוב",
      "ההגנה",
      "בלפור",
      "זאבוטינסקי",
      "אחד העם",
      "בן גוריון"
    ]
  },
  {
    "name": "נתניה",
    "streets": [
      "הרצל",
      "ויצמן",
      "ההסתדרות",
      "סוקולוב",
      "ההגנה",
      "בלפור",
      "זאבוטינסקי",
      "אחד העם",
      "בן עמי",
      "השוק"
    ]
  },
  {
    "name": "רמת גן",
    "streets": [
      "ביאליק",
      "ויצמן",
      "זאבוטינסקי",
      "ההסתדרות",
      "סוקולוב",
      "ההגנה",
      "איבא חן",
      "הרצל",
      "זאבוטינסקי",
      "אחד העם"
    ]
  },
  {
    "name": "אשדוד",
    "streets": [
      "הרצל",
      "ויצמן",
      "ההסתדרות",
      "סוקולוב",
      "ההגנה",
      "בלפור",
      "זאבוטינסקי",
      "אחד העם",
      "הרב יהודה השני",
      "העצמאות"
    ]
  },
  {
    "name": "בני ברק",
    "streets": [
      "זאבוטינסקי",
      "הרצל",
      "ויצמן",
      "ההסתדרות",
      "רבן גוריון",
      "אבן גבירול",
      "חזן",
      "איילון",
      "הרב יהודה",
      "בית הרב"
    ]
  },
  {
    "name": "מודיעין-מכבים-רעות",
    "streets": [
      "איילון הנשיא",
      "הרצל",
      "זאבוטינסקי",
      "ההסתדרות",
      "הנשיא",
      "בן שמאי",
      "השוק",
      "העמק",
      "הרב הריץ",
      "הרב הגדול"
    ]
  }
];

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
