import { chromium } from 'playwright';

const BASE = 'http://localhost:3000';
const ORG = 'dbae6c14-bd14-4da1-a731-5d51bb10f4ac';
const API = 'http://localhost:8080';

async function api(path, opts = {}) {
  const h = { 'Content-Type': 'application/json', ...(opts.token ? { 'X-FAM-Session': opts.token } : {}) };
  const r = await fetch(`${API}${path}`, { method: opts.method || 'GET', headers: h, body: opts.body ? JSON.stringify(opts.body) : undefined });
  return r.json();
}

const login = await api('/api/v1/auth/login', { method: 'POST', body: { username: 'superadmin', password: 'ChangeMe123!' } });
const enter = await api(`/api/v1/admin/organizations/${ORG}/enter`, { method: 'POST', token: login.sessionToken });
const token = enter.sessionToken || login.sessionToken;
const decisions = await api('/api/v1/org/committee-decisions', { token });
const draft = decisions.decisions.filter((d) => d.status === 'draft').slice(-1)[0];
const types = await api('/api/v1/org/assistance-types', { token });
const typeId = types.assistanceTypes.find((t) => t.status === 'active').id;

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
await context.addInitScript((t) => sessionStorage.setItem('FAM.Session', t), token);
const page = await context.newPage();
await page.goto(BASE);
await page.waitForLoadState('networkidle');
if (await page.getByRole('button', { name: 'כניסה' }).first().isVisible().catch(() => false)) {
  await page.getByRole('button', { name: 'כניסה' }).first().click();
  await page.waitForTimeout(800);
}
await page.getByRole('button', { name: 'החלטות ועדה' }).click();
await page.waitForSelector('table.org-table tbody tr');
await page.locator('table.org-table tbody tr').filter({ hasText: draft.decisionCode })
  .locator('button', { hasText: 'פתח' }).click();
await page.waitForSelector('#item-assistance-type');

const rowsBefore = await page.locator('.committee-items-table tbody tr').evaluateAll((trs) =>
  trs.filter((r) => !r.querySelector('.empty-row')).length,
);

await page.locator('#item-assistance-type').selectOption(typeId);
await page.locator('#item-payment-target').selectOption('other');
await page.locator('#item-payee-name').pressSequentially('מוטב E2E', { delay: 15 });
await page.locator('#item-payment-method').selectOption('bank_transfer');
await page.locator('#transfer-bank-number').waitFor({ state: 'visible' });
await page.locator('#transfer-bank-number').fill('12');
await page.locator('#transfer-branch-number').fill('123');
await page.locator('#transfer-account-number').fill('456789');
await page.locator('.modal-overlay').last().getByRole('button', { name: 'שמור' }).click();
await page.locator('#transfer-bank-number').waitFor({ state: 'hidden' });
await page.waitForTimeout(300);
console.log('transfer btn', await page.locator('#item-transfer-details').textContent());
console.log('method', await page.locator('#item-payment-method').inputValue());
await page.locator('#item-amount').fill('75');
await page.getByRole('button', { name: 'הוסף שורה' }).click();
await page.waitForTimeout(3000);
const rowsAfter = await page.locator('.committee-items-table tbody tr').evaluateAll((trs) =>
  trs.filter((r) => !r.querySelector('.empty-row')).length,
);
console.log('rows', rowsBefore, '->', rowsAfter);
console.log('add error', await page.locator('#item-form-error').textContent().catch(() => ''));
const texts = await page.locator('.committee-items-table tbody tr').last().locator('td').allTextContents();
console.log('last row cells', texts);
await browser.close();
