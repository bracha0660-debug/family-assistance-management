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
const draft = decisions.decisions.find((d) => d.status === 'draft');
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

await page.locator('#item-assistance-type').selectOption(typeId);
await page.locator('#item-payment-target').selectOption('other');

const payee = page.locator('#item-payee-name');
console.log('payee tag', await payee.evaluate((el) => el.tagName));
await payee.click();
await payee.fill('');
await payee.pressSequentially('Payee Test', { delay: 20 });
await payee.blur();
console.log('payee value after fill', await payee.inputValue());

await page.locator('#item-payment-method').selectOption('bank_transfer');
await page.waitForTimeout(500);
console.log('method value', await page.locator('#item-payment-method').inputValue());
console.log('transfer input count', await page.locator('#transfer-bank-number').count());
console.log('modal overlays', await page.locator('.modal-overlay').count());
console.log('item error', await page.locator('#item-form-error').textContent().catch(() => ''));
console.log('transfer btn visible', await page.locator('#item-transfer-details').isVisible().catch(() => false));
console.log('transfer btn text', await page.locator('#item-transfer-details').textContent().catch(() => ''));

await browser.close();
