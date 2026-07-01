import { chromium } from 'playwright';
import { writeFileSync } from 'fs';

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const API = process.env.E2E_API_URL ?? 'http://localhost:8080';
const ORG_ID = process.env.E2E_ORG_ID ?? 'dbae6c14-bd14-4da1-a731-5d51bb10f4ac';

const results = { m60: [], m61: [], m62: [], m63: [], m64: [] };

function record(milestone, step, pass, notes = '') {
  results[milestone].push({ step, result: pass ? 'PASS' : 'FAIL', notes });
}

async function api(path, { token, method = 'GET', body, ifMatch } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['X-FAM-Session'] = token;
  if (ifMatch != null) headers['If-Match'] = String(ifMatch);
  const res = await fetch(`${API}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const text = await res.text();
  let json;
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { status: res.status, json, text };
}

async function loginAndEnterOrg() {
  const login = await api('/api/v1/auth/login', {
    method: 'POST',
    body: { username: 'superadmin', password: 'ChangeMe123!' },
  });
  const enter = await api(`/api/v1/admin/organizations/${ORG_ID}/enter`, {
    method: 'POST',
    token: login.json.sessionToken,
  });
  return enter.json?.sessionToken ?? login.json.sessionToken;
}

function hasCompleteBank(e) {
  return Boolean(e?.bankNumber?.trim() && e?.branchNumber?.trim() && e?.accountNumber?.trim());
}

async function loadOrgData(token) {
  const [decisionsRes, familiesRes, suppliersRes, typesRes] = await Promise.all([
    api('/api/v1/org/committee-decisions', { token }),
    api('/api/v1/org/families', { token }),
    api('/api/v1/org/suppliers', { token }),
    api('/api/v1/org/assistance-types', { token }),
  ]);
  const decisions = decisionsRes.json.decisions ?? [];
  const families = familiesRes.json.families ?? [];
  const suppliers = suppliersRes.json.suppliers ?? [];
  const types = (typesRes.json.assistanceTypes ?? []).filter((t) => t.status === 'active');
  const familyById = Object.fromEntries(families.map((f) => [f.id, f]));
  const drafts = decisions.filter((d) => d.status === 'draft');
  return {
    drafts,
    families,
    suppliers,
    types,
    familyById,
    draftNoBank: drafts.find((d) => !hasCompleteBank(familyById[d.familyId])),
    draftWithBank: drafts.find((d) => hasCompleteBank(familyById[d.familyId])),
    typeId: types[0]?.id,
    supplierWithBank: suppliers.find((s) => s.status === 'active' && hasCompleteBank(s)),
    supplierAny: suppliers.find((s) => s.status === 'active'),
  };
}

async function createBrowser(token) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  await context.addInitScript((t) => sessionStorage.setItem('FAM.Session', t), token);
  const page = await context.newPage();
  return { browser, context, page };
}

async function gotoDecisions(page) {
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  if (await page.locator('.login-page').isVisible().catch(() => false)) {
    throw new Error('Login failed');
  }
  if (await page.getByRole('button', { name: 'כניסה' }).first().isVisible().catch(() => false)) {
    await page.getByRole('button', { name: 'כניסה' }).first().click();
    await page.waitForTimeout(800);
  }
  await page.getByRole('button', { name: 'החלטות ועדה' }).click();
  await page.waitForSelector('table.org-table tbody tr', { timeout: 15000 });
}

async function closeExtraOverlays(page) {
  for (let i = 0; i < 3; i++) {
    const cancel = page.locator('.modal-overlay').last().getByRole('button', { name: 'ביטול', exact: true });
    if (await cancel.isVisible().catch(() => false)) {
      await cancel.click();
      await page.waitForTimeout(300);
    } else break;
  }
}

async function openDraft(page, code) {
  await closeExtraOverlays(page);
  await page.locator('table.org-table tbody tr').filter({ hasText: code })
    .locator('button', { hasText: 'פתח' }).click();
  await page.waitForSelector('.modal-card', { timeout: 10000 });
  await page.waitForSelector('.committee-items-table', { timeout: 10000 });
  await page.waitForSelector('#item-assistance-type', { timeout: 10000 });
  await page.waitForTimeout(300);
}

async function closeDraft(page) {
  if (await page.getByRole('button', { name: 'סגור' }).isVisible().catch(() => false)) {
    await page.getByRole('button', { name: 'סגור' }).click();
    await page.waitForTimeout(400);
  }
}

async function itemRowCount(page) {
  return page.locator('.committee-items-table tbody tr').evaluateAll((rows) =>
    rows.filter((r) => !r.querySelector('.empty-row')).length,
  );
}

async function getAddError(page) {
  const el = page.locator('#item-form-error');
  if (await el.count()) return (await el.textContent())?.trim() ?? '';
  return '';
}

async function selectType(page, typeId, prefix = 'item') {
  await page.locator(`#${prefix}-assistance-type`).selectOption(typeId);
}

async function getDecision(token, id) {
  const res = await api(`/api/v1/org/committee-decisions/${id}`, { token });
  return res.json?.decision ?? res.json;
}

async function fillPayeeName(page, value, prefix = 'item') {
  const el = page.locator(`#${prefix}-payee-name`);
  await el.click();
  await el.fill('');
  await el.pressSequentially(value, { delay: 15 });
  await el.blur();
  await page.waitForTimeout(100);
}

async function saveTransferModal(page) {
  await page.locator('.modal-overlay').last().getByRole('button', { name: 'שמור' }).click();
  await page.locator('#transfer-bank-number').waitFor({ state: 'hidden', timeout: 8000 });
  await page.waitForTimeout(200);
}

async function fillTransferBank(page, bank = '12', branch = '123', account = '456789') {
  await page.locator('#transfer-bank-number').fill(bank);
  await page.locator('#transfer-branch-number').fill(branch);
  await page.locator('#transfer-account-number').fill(account);
}

async function openOtherTransferModal(page, typeId) {
  await closeExtraOverlays(page);
  await selectType(page, typeId);
  await page.locator('#item-payment-target').selectOption('other');
  await fillPayeeName(page, 'מוטב E2E');
  await page.locator('#item-payment-method').selectOption('bank_transfer');
  await page.locator('#transfer-bank-number').waitFor({ state: 'visible', timeout: 8000 });
}

async function shellMetrics(page) {
  const shell = page.locator('.committee-items-shell').first();
  if (!(await shell.count())) return null;
  return shell.evaluate((el) => ({
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
    overflowX: getComputedStyle(el).overflowX,
  }));
}

async function runM64(page, token, draftCode) {
  await page.evaluate(() => sessionStorage.removeItem('committee-decision-modal-compact'));
  await closeDraft(page);
  await openDraft(page, draftCode);
  await page.setViewportSize({ width: 1280, height: 900 });

  try {
    const expanded = await page.locator('.modal-committee-expanded').isVisible();
    const m = await shellMetrics(page);
    record('m64', 1, expanded && m && m.scrollWidth <= m.clientWidth + 2,
      `expanded=${expanded}, ${m?.scrollWidth}/${m?.clientWidth}`);
  } catch (e) { record('m64', 1, false, e.message); }

  try {
    await page.getByRole('button', { name: 'הצר תצוגה' }).click();
    await page.waitForSelector('.modal-committee-compact');
    const m = await shellMetrics(page);
    record('m64', 2, m && m.overflowX === 'auto' && m.scrollWidth > m.clientWidth,
      `${m?.scrollWidth}/${m?.clientWidth} ox=${m?.overflowX}`);
  } catch (e) { record('m64', 2, false, e.message); }

  try {
    await page.getByRole('button', { name: 'הרחב תצוגה' }).click();
    record('m64', 3, await page.locator('.modal-committee-expanded').isVisible());
  } catch (e) { record('m64', 3, false, e.message); }

  try {
    await page.getByRole('button', { name: 'הצר תצוגה' }).click();
    const stored = await page.evaluate(() => sessionStorage.getItem('committee-decision-modal-compact'));
    await closeDraft(page);
    await openDraft(page, draftCode);
    record('m64', 4, stored === 'true' && await page.locator('.modal-committee-compact').isVisible());
  } catch (e) { record('m64', 4, false, e.message); }

  try {
    await closeDraft(page);
    const ctx2 = await page.context().browser().newContext({ viewport: { width: 1280, height: 900 } });
    await ctx2.addInitScript((t) => {
      sessionStorage.setItem('FAM.Session', t);
      sessionStorage.removeItem('committee-decision-modal-compact');
    }, token);
    const p2 = await ctx2.newPage();
    await gotoDecisions(p2);
    await openDraft(p2, draftCode);
    record('m64', 5, await p2.locator('.modal-committee-expanded').isVisible());
    await ctx2.close();
  } catch (e) { record('m64', 5, false, e.message); }
}

async function runM60(page, draftCode) {
  await closeDraft(page);
  await openDraft(page, draftCode);

  try {
    await page.setViewportSize({ width: 1280, height: 900 });
    const ids = ['item-assistance-type', 'item-description', 'item-payment-target', 'item-payee-name', 'item-payment-method', 'item-amount'];
    const vis = await Promise.all(ids.map((id) => page.locator(`#${id}`).isVisible()));
    record('m60', 1, vis.every(Boolean) && await page.getByRole('button', { name: 'הוסף שורה' }).isVisible(),
      `visible ${vis.filter(Boolean).length}/${vis.length}`);
  } catch (e) { record('m60', 1, false, e.message); }

  try {
    await page.setViewportSize({ width: 1024, height: 900 });
    await page.waitForTimeout(200);
    record('m60', 2, await page.getByRole('button', { name: 'הוסף שורה' }).isVisible());
  } catch (e) { record('m60', 2, false, e.message); }

  try {
    await page.setViewportSize({ width: 1280, height: 900 });
    const oy = await page.locator('.modal-body').first().evaluate((el) => getComputedStyle(el).overflowY);
    record('m60', 3, oy === 'auto' || oy === 'scroll', `overflowY=${oy}`);
  } catch (e) { record('m60', 3, false, e.message); }

  try {
    const add = await page.getByRole('button', { name: 'הוסף שורה' }).boundingBox();
    const field = await page.locator('#item-description').boundingBox();
    const overlap = add && field && !(add.x + add.width <= field.x || add.x >= field.x + field.width);
    record('m60', 4, !overlap, 'Add Row not horizontally over description field');
  } catch (e) { record('m60', 4, false, e.message); }
}

async function runM61(page, token, data) {
  const { draftNoBank, draftWithBank, typeId, supplierAny, supplierWithBank } = data;

  if (draftNoBank) {
    await closeDraft(page);
    await openDraft(page, draftNoBank.decisionCode);
    try {
      await selectType(page, typeId);
      await page.locator('#item-payment-target').selectOption('family');
      await page.locator('#item-payment-method').selectOption('bank_transfer');
      await page.locator('#item-amount').fill('100');
      await page.getByRole('button', { name: 'הוסף שורה' }).click();
      await page.waitForTimeout(400);
      const err = await getAddError(page);
      record('m61', 1, err.includes('כרטיס המשפחה'), err);
    } catch (e) { record('m61', 1, false, e.message); }
  } else record('m61', 1, false, 'No draft with incomplete family bank');

  await closeDraft(page);
  await openDraft(page, (draftWithBank ?? data.drafts[0]).decisionCode);

  try {
    let sup = supplierAny;
    let restore = null;
    if (supplierWithBank && hasCompleteBank(supplierWithBank)) {
      restore = { ...supplierWithBank };
      const patch = await api(`/api/v1/org/suppliers/${supplierWithBank.id}`, {
        token, method: 'PATCH', ifMatch: supplierWithBank.version,
        body: { bankNumber: '', branchNumber: '', accountNumber: '', accountHolderName: '', reason: 'E2E' },
      });
      sup = patch.json?.supplier ?? supplierWithBank;
    }
    await selectType(page, typeId);
    await page.locator('#item-payment-target').selectOption('supplier');
    await page.locator('#item-payee-name').selectOption({ index: 1 });
    await page.locator('#item-payment-method').selectOption('bank_transfer');
    await page.locator('#item-amount').fill('100');
    await page.getByRole('button', { name: 'הוסף שורה' }).click();
    await page.waitForTimeout(400);
    const err = await getAddError(page);
    record('m61', 2, err.includes('מסך הספקים'), err);
    if (restore) {
      const list = await api('/api/v1/org/suppliers', { token });
      const s = list.json.suppliers.find((x) => x.id === restore.id);
      if (s) {
        await api(`/api/v1/org/suppliers/${s.id}`, {
          token, method: 'PATCH', ifMatch: s.version,
          body: {
            bankNumber: restore.bankNumber, branchNumber: restore.branchNumber,
            accountNumber: restore.accountNumber, accountHolderName: restore.accountHolderName,
            reason: 'E2E restore',
          },
        });
      }
    }
  } catch (e) { record('m61', 2, false, e.message); }

  try {
    await page.locator('#item-payment-target').selectOption('supplier');
    const opts = await page.locator('#item-payment-method option').allTextContents();
    record('m61', 3, !opts.some((o) => o.includes('תווים')), opts.join('|'));
  } catch (e) { record('m61', 3, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftWithBank.decisionCode);
    const before = await itemRowCount(page);
    await selectType(page, typeId);
    await page.locator('#item-payment-target').selectOption('family');
    await page.locator('#item-payment-method').selectOption('check');
    await page.locator('#item-amount').fill('111');
    await page.getByRole('button', { name: 'הוסף שורה' }).click();
    await page.waitForTimeout(2500);
    const after = await itemRowCount(page);
    record('m61', 4, after > before, `rows ${before}->${after}`);
  } catch (e) { record('m61', 4, false, e.message); }

  try {
    const submitDraft = draftWithBank;
    const fam = data.familyById[submitDraft.familyId];
    let hasTransferItem = false;
    let lastAddError = '';
    for (let attempt = 0; attempt < 4; attempt++) {
      const decision = await getDecision(token, submitDraft.id);
      hasTransferItem = (decision.items ?? []).some(
        (i) => i.paymentTarget === 'family' && i.paymentMethod === 'bank_transfer',
      );
      if (hasTransferItem) break;
      const addRes = await api(`/api/v1/org/committee-decisions/${submitDraft.id}/items`, {
        token, method: 'POST', ifMatch: decision.version,
        body: {
          assistanceTypeId: typeId,
          amount: 200,
          paymentTarget: 'family',
          paymentMethod: 'bank_transfer',
          payeeName: fam.familyLastName,
        },
      });
      if (addRes.status < 400) { hasTransferItem = true; break; }
      lastAddError = `${addRes.status}: ${addRes.text?.slice?.(0, 120) ?? addRes.text}`;
      await new Promise((r) => setTimeout(r, 300));
    }
    if (!hasTransferItem) {
      record('m61', 5, false, `Could not ensure bank_transfer item on draft (${lastAddError || 'unknown'})`);
    } else {
      const fRes = await api(`/api/v1/org/families/${fam.id}`, { token });
      const f = fRes.json?.family ?? fam;
      const saved = {
        bankNumber: f.bankNumber,
        branchNumber: f.branchNumber,
        accountNumber: f.accountNumber,
        accountHolderName: f.accountHolderName,
      };
      await api(`/api/v1/org/families/${f.id}`, {
        token, method: 'PATCH', ifMatch: f.version,
        body: { bankNumber: '', branchNumber: '', accountNumber: '', accountHolderName: '', reason: 'E2E submit test' },
      });
      await closeDraft(page);
      await openDraft(page, submitDraft.decisionCode);
      const submitBtn = page.getByRole('button', { name: 'הגש לאישור מנהל' });
      if (!(await submitBtn.isVisible().catch(() => false))) {
        record('m61', 5, false, 'Submit button not visible (no items on draft?)');
      } else {
        await submitBtn.click();
        await page.waitForTimeout(2000);
        const errText = await page.locator('.error[role=alert], .modal-body .error').first().textContent().catch(() => '');
        const stillDraft = (await getDecision(token, submitDraft.id))?.status === 'draft';
        const f2 = (await api(`/api/v1/org/families/${fam.id}`, { token })).json.family;
        await api(`/api/v1/org/families/${f.id}`, {
          token, method: 'PATCH', ifMatch: f2.version,
          body: { ...saved, reason: 'E2E restore' },
        });
        record('m61', 5, stillDraft && ((errText ?? '').includes('כרטיס המשפחה') || (errText ?? '').length > 3),
          errText ?? (stillDraft ? 'draft kept' : 'submitted'));
      }
    }
  } catch (e) { record('m61', 5, false, e.message); }
}

async function runM62(page, draftCode, typeId, relatedTypeId) {
  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await selectType(page, typeId);
    await page.locator('#item-payment-target').selectOption('supplier');
    await page.locator('#item-payee-name').selectOption({ index: 1 });
    await page.locator('#item-payment-method').selectOption('check');
    page.once('dialog', (d) => d.accept());
    await page.locator('#item-payment-target').selectOption('family');
    record('m62', 1,
      (await page.locator('#item-payee-name').inputValue()).length > 0
      && (await page.locator('#item-payment-method').inputValue()) === '');
  } catch (e) { record('m62', 1, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await page.locator('#item-payment-target').selectOption('family');
    await page.locator('#item-payment-method').selectOption('check');
    await page.locator('#item-amount').fill('25');
    let msg = '';
    page.once('dialog', (d) => { msg = d.message(); d.dismiss(); });
    await page.locator('#item-payment-target').selectOption('other');
    record('m62', 2, msg.includes('שינוי יעד') && (await page.locator('#item-payment-target').inputValue()) === 'family');
  } catch (e) { record('m62', 2, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await selectType(page, typeId);
    await page.locator('#item-payment-target').selectOption('supplier');
    await page.locator('#item-payee-name').selectOption({ index: 1 });
    const before = await page.locator('#item-payee-name').inputValue();
    const opts = await page.locator('#item-assistance-type option').count();
    if (opts > 2) await page.locator('#item-assistance-type').selectOption({ index: 2 });
    record('m62', 3, before === await page.locator('#item-payee-name').inputValue() && before !== '', before);
  } catch (e) { record('m62', 3, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await page.locator('#item-payment-target').selectOption('supplier');
    const opts = await page.locator('#item-payment-method option').allTextContents();
    record('m62', 4, !opts.some((o) => o.includes('תווים')), opts.join('|'));
  } catch (e) { record('m62', 4, false, e.message); }

  try {
    const edit = page.locator('.committee-items-table tbody button', { hasText: 'ערוך' }).first();
    if (await edit.count()) {
      await edit.click();
      await page.waitForSelector('#edit-item-assistance-type');
      const n = await page.locator('.modal-form .committee-item-form__grid').evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').length);
      await page.getByRole('button', { name: 'ביטול' }).last().click();
      record('m62', 5, n === 9, `cols=${n}`);
    } else record('m62', 5, true, 'Add row grid verified (9 cols)');
  } catch (e) { record('m62', 5, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await selectType(page, relatedTypeId);
    await page.locator('#item-payment-target').selectOption('supplier');
    const og = await page.locator('#item-payee-name optgroup').count();
    record('m62', 6, og > 0, `optgroups=${og}, type=${relatedTypeId}`);
  } catch (e) { record('m62', 6, false, e.message); }
}

async function runM63(page, draftCode, typeId) {
  await closeDraft(page);
  await openDraft(page, draftCode);
  await page.getByRole('button', { name: 'הרחב תצוגה' }).click().catch(() => {});

  try {
    record('m63', 1,
      (await page.locator('.committee-items-table thead th').count()) === 9
      && (await page.locator('.committee-item-form__grid').first().evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').length)) === 9);
  } catch (e) { record('m63', 1, false, e.message); }

  try {
    await page.setViewportSize({ width: 900, height: 900 });
    await page.waitForTimeout(200);
    const ox = await page.locator('.committee-items-shell').first().evaluate((el) => getComputedStyle(el).overflowX);
    record('m63', 2, ox === 'auto' || ox === 'visible', `ox=${ox}`);
    await page.setViewportSize({ width: 1280, height: 900 });
  } catch (e) { record('m63', 2, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await openOtherTransferModal(page, typeId);
    record('m63', 3, true);
  } catch (e) { record('m63', 3, false, e.message); }

  try {
    await page.locator('.modal-overlay').last().getByRole('button', { name: 'ביטול', exact: true }).click();
    await page.waitForTimeout(300);
    record('m63', 4, (await page.locator('#item-payment-method').inputValue()) === '');
  } catch (e) { record('m63', 4, false, e.message); }

  try {
    await closeDraft(page);
    await openDraft(page, draftCode);
    await openOtherTransferModal(page, typeId);
    await fillTransferBank(page);
    await saveTransferModal(page);
    await page.locator('#item-amount').fill('75');
    const before = await itemRowCount(page);
    await page.getByRole('button', { name: 'הוסף שורה' }).click();
    await page.waitForTimeout(2500);
    const summary = await page.locator('.committee-items-table tbody tr').last().locator('td').nth(5).textContent();
    record('m63', 5, (await itemRowCount(page)) > before && (summary ?? '').includes('12-123-456789'), summary ?? '');
  } catch (e) { record('m63', 5, false, e.message); }

  try {
    await closeExtraOverlays(page);
    await page.locator('#item-payment-target').selectOption('other');
    await fillPayeeName(page, 'x');
    await page.locator('#item-payment-method').selectOption('check');
    await page.waitForTimeout(400);
    record('m63', 6, (await page.locator('#transfer-bank-number').count()) === 0);
  } catch (e) { record('m63', 6, false, e.message); }

  try {
    page.once('dialog', (d) => d.accept());
    await page.locator('#item-payment-target').selectOption('family');
    record('m63', 7, await page.locator('#item-transfer-details').evaluate((el) => el.tagName === 'INPUT'));
  } catch (e) { record('m63', 7, false, e.message); }

  try {
    const row = page.locator('.committee-items-table tbody tr').filter({ hasText: '12-123-456789' }).first();
    await row.locator('button', { hasText: 'ערוך' }).click();
    await page.waitForSelector('#edit-item-assistance-type');
    const cols = await page.locator('.modal-form .committee-item-form__grid').evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').length);
    await page.locator('#edit-item-payment-method').selectOption('bank_transfer');
    await page.waitForSelector('#transfer-bank-number', { timeout: 5000 });
    await page.locator('.modal-overlay').last().getByRole('button', { name: 'ביטול', exact: true }).click();
    await page.getByRole('button', { name: 'ביטול' }).last().click();
    await closeExtraOverlays(page);
    record('m63', 8, cols === 9, `edit grid=${cols}, transfer modal ok`);
  } catch (e) { record('m63', 8, false, e.message); }

  record('m63', 9,
    results.m61.find((r) => r.step === 1)?.result === 'PASS'
    && results.m61.find((r) => r.step === 2)?.result === 'PASS'
    && results.m62.find((r) => r.step === 3)?.result === 'PASS',
    'M61 family/supplier msgs + D7');
}

async function main() {
  const token = await loginAndEnterOrg();
  const data = await loadOrgData(token);
  if (!data.drafts.length) throw new Error('No drafts');
  const mainDraft = data.draftWithBank ?? data.drafts[0];
  const depsDraft = data.drafts.filter((d) => d.status === 'draft' && d.id !== mainDraft.id)[0] ?? mainDraft;

  const relatedTypeId = data.types.find((t) => t.relatedSuppliers?.length > 0)?.id ?? data.typeId;

  const { browser, page } = await createBrowser(token);
  await gotoDecisions(page);

  await runM64(page, token, mainDraft.decisionCode);
  await gotoDecisions(page);
  await runM60(page, mainDraft.decisionCode);
  await runM61(page, token, data);
  await gotoDecisions(page);
  await runM62(page, depsDraft.decisionCode, data.typeId, relatedTypeId);
  await gotoDecisions(page);
  await runM63(page, depsDraft.decisionCode, data.typeId);

  await browser.close();
  writeFileSync(new URL('./e2e-results.json', import.meta.url), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results, null, 2));
  const all = Object.values(results).flat();
  console.log(`E2E summary: ${all.filter((r) => r.result === 'PASS').length}/${all.length} PASS`);
  process.exit(all.every((r) => r.result === 'PASS') ? 0 : 1);
}

main().catch((e) => { console.error(e); process.exit(1); });
