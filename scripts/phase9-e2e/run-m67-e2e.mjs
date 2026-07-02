import { chromium } from 'playwright';
import { writeFileSync } from 'fs';

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const API = process.env.E2E_API_URL ?? 'http://localhost:8080';
const ORG_ID = process.env.E2E_ORG_ID ?? 'dbae6c14-bd14-4da1-a731-5d51bb10f4ac';

const results = [];

function record(step, pass, notes = '') {
  results.push({ step, result: pass ? 'PASS' : 'FAIL', notes });
}

async function api(path, { token, method = 'GET', body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['X-FAM-Session'] = token;
  const res = await fetch(`${API}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const text = await res.text();
  let json;
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { status: res.status, json, text };
}

function hasCompleteBank(e) {
  return Boolean(
    e?.bankNumber?.trim()
    && e?.branchNumber?.trim()
    && e?.accountNumber?.trim()
    && e?.accountHolderName?.trim(),
  );
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
    familyById,
    draftWithBank: drafts.find((d) => hasCompleteBank(familyById[d.familyId])),
    draftNoBank: drafts.find((d) => !hasCompleteBank(familyById[d.familyId])),
    typeId: types[0]?.id,
    supplierWithBank: suppliers.find((s) => s.status === 'active' && hasCompleteBank(s)),
  };
}

async function createBrowser(token) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  await context.addInitScript((t) => sessionStorage.setItem('FAM.Session', t), token);
  const page = await context.newPage();
  page.on('dialog', (dialog) => dialog.accept());
  return { browser, page };
}

async function gotoDecisions(page) {
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  if (await page.getByRole('button', { name: 'כניסה' }).first().isVisible().catch(() => false)) {
    await page.getByRole('button', { name: 'כניסה' }).first().click();
    await page.waitForTimeout(800);
  }
  await page.getByRole('button', { name: 'החלטות ועדה' }).click();
  await page.waitForSelector('table.org-table tbody tr', { timeout: 15000 });
}

async function openDraft(page, code) {
  await page.locator('table.org-table tbody tr').filter({ hasText: code })
    .locator('button', { hasText: 'פתח' }).click();
  await page.waitForSelector('.modal-card', { timeout: 10000 });
  await page.waitForSelector('#item-assistance-type', { timeout: 10000 });
  await page.waitForTimeout(300);
}

async function fillPayeeName(page, value) {
  const el = page.locator('#item-payee-name');
  await el.click();
  await el.fill('');
  await el.pressSequentially(value, { delay: 15 });
  await el.blur();
  await page.waitForTimeout(100);
}

async function prepareOtherTransferRow(page, typeId) {
  await page.locator('#item-assistance-type').selectOption(typeId);
  await page.locator('#item-payment-target').selectOption('other');
  await fillPayeeName(page, 'מוטב בדיקה M67');
  await page.locator('#item-payment-method').selectOption('bank_transfer');
  await page.waitForSelector('.committee-transfer-popover', { timeout: 5000 });
}

async function selectBankFromSearch(page, query) {
  const bankInput = page.locator('#transfer-bank-number');
  await bankInput.click();
  await bankInput.fill('');
  if (query.length <= 2 && /^\d+$/.test(query)) {
    await bankInput.pressSequentially(query, { delay: 20 });
    await page.waitForTimeout(200);
    return;
  }
  await bankInput.pressSequentially(query, { delay: 20 });
  await page.waitForSelector('.bank-combobox-list', { timeout: 3000 });
  await page.locator('.bank-combobox-list .bank-combobox-option').first().click();
}

const token = await loginAndEnterOrg();
const data = await loadOrgData(token);
const { browser, page } = await createBrowser(token);

try {
  await gotoDecisions(page);
  const draft = data.draftWithBank ?? data.drafts[0];
  if (!draft || !data.typeId) {
    throw new Error('Missing draft decision or assistance type');
  }
  await openDraft(page, draft.decisionCode);

  // M67.2 — expanded modal, no toggle, grid alignment, no field jump
  const toggleCount = await page.getByRole('button', { name: /הצר תצוגה|הרחב תצוגה/ }).count();
  const modalExpanded = page.locator('.modal-card.modal-committee-expanded');
  const modalStyles = await modalExpanded.evaluate((el) => {
    const cs = getComputedStyle(el);
    return { resize: cs.resize, minWidth: cs.minWidth };
  });
  const gridAlign = await page.locator('.committee-item-form__grid').evaluate((el) => getComputedStyle(el).alignItems);
  record('67.2-a', toggleCount === 0 && await modalExpanded.isVisible() && modalStyles.resize === 'horizontal',
    `toggle=${toggleCount} resize=${modalStyles.resize} minWidth=${modalStyles.minWidth}`);
  record('67.2-b', gridAlign === 'start', `alignItems=${gridAlign}`);

  await page.locator('#item-assistance-type').selectOption(data.typeId);
  await page.locator('#item-payment-target').selectOption('family');
  const methodYBefore = (await page.locator('#item-payment-method').boundingBox())?.y ?? 0;
  await page.locator('#item-payment-method').selectOption('vouchers');
  await page.waitForTimeout(200);
  const methodYAfterVoucher = (await page.locator('#item-payment-method').boundingBox())?.y ?? 0;
  const voucherVisible = await page.locator('#item-voucher-type').isVisible();
  record('67.2-c', voucherVisible && Math.abs(methodYAfterVoucher - methodYBefore) < 2,
    `voucherVisible=${voucherVisible} jump=${Math.abs(methodYAfterVoucher - methodYBefore)}`);

  const payeeYBefore = (await page.locator('#item-payee-name').boundingBox())?.y ?? 0;
  const hintVisible = await page.locator('.committee-item-form__field--payee .bank-field-hint').isVisible();
  const payeeYAfterHint = (await page.locator('#item-payee-name').boundingBox())?.y ?? 0;
  record('67.2-d', hintVisible && Math.abs(payeeYAfterHint - payeeYBefore) < 2,
    `hint=${hintVisible} jump=${Math.abs(payeeYAfterHint - payeeYBefore)}`);

  await page.locator('#item-payment-method').selectOption('');
  await page.locator('#item-payment-target').selectOption('');
  await page.waitForTimeout(200);

  // Step 1 — spacing / bounded entry zone
  const formBox = await page.locator('.committee-item-form').boundingBox();
  const tableBox = await page.locator('.committee-items-table').boundingBox();
  const formStyles = await page.locator('.committee-item-form').evaluate((el) => {
    const cs = getComputedStyle(el);
    return {
      marginBottom: cs.marginBottom,
      borderBottomWidth: cs.borderBottomWidth,
    };
  });
  const gapOk = formBox && tableBox && tableBox.y >= formBox.y + formBox.height - 2;
  const spacingOk = parseFloat(formStyles.marginBottom) >= 32 || parseFloat(formStyles.borderBottomWidth) >= 1;
  record(1, gapOk && spacingOk, `gap=${gapOk} spacing=${spacingOk}`);

  // Step 2 — inline popover (not modal)
  await prepareOtherTransferRow(page, data.typeId);
  const popoverVisible = await page.locator('.committee-transfer-popover').isVisible();
  const modalOverlayCount = await page.locator('.modal-overlay').count();
  record(2, popoverVisible && modalOverlayCount <= 1, `popover=${popoverVisible} overlays=${modalOverlayCount}`);
  const popoverWidthPx = await page.locator('.committee-transfer-popover').evaluate((el) => parseFloat(getComputedStyle(el).width));
  record('67.2-popover-width', popoverWidthPx >= 280, `widthPx=${popoverWidthPx}`);

  // Step 3 — bank search list-only
  const bankInput = page.locator('#transfer-bank-number');
  await bankInput.click();
  await bankInput.fill('');
  await bankInput.pressSequentially('הפוע', { delay: 25 });
  await page.waitForSelector('.bank-combobox-list', { timeout: 3000 });
  const options = await page.locator('.bank-combobox-option').allTextContents();
  const hasHapoalim = options.some((t) => t.includes('12') && t.includes('הפועלים'));
  await page.locator('.bank-combobox-option').filter({ hasText: 'הפועלים' }).first().click();
  const bankValue = await bankInput.inputValue();
  const listOnly = hasHapoalim && bankValue.includes('12') && bankValue.includes('הפועלים');
  await bankInput.fill('99999');
  await bankInput.blur();
  await page.waitForTimeout(150);
  const afterInvalid = await bankInput.inputValue();
  const rejectedFreeText = !afterInvalid.includes('99999');
  record(3, listOnly && rejectedFreeText, `options=${options.length} selected="${bankValue}" rejected99999=${rejectedFreeText}`);

  // Step 4 — save updates summary in place
  await selectBankFromSearch(page, '12');
  await page.locator('#transfer-branch-number').fill('655');
  await page.locator('#transfer-account-number').fill('295455');
  await page.locator('.committee-transfer-popover').getByRole('button', { name: 'שמור פרטי העברה' }).click();
  await page.waitForSelector('.committee-transfer-popover', { state: 'hidden', timeout: 5000 });
  const summaryAfterSave = (await page.locator('#item-bank-details').textContent())?.trim() ?? '';
  const saveOk = summaryAfterSave === '12-655-295455';
  record(4, saveOk, `summary="${summaryAfterSave}"`);

  // Step 5 — cancel restores prior values
  await page.locator('#item-bank-details').click();
  await page.waitForSelector('.committee-transfer-popover');
  await page.locator('#transfer-branch-number').fill('111');
  await page.locator('.committee-transfer-popover').getByRole('button', { name: 'ביטול' }).click();
  await page.waitForSelector('.committee-transfer-popover', { state: 'hidden' });
  const summaryAfterCancel = (await page.locator('#item-bank-details').textContent())?.trim() ?? '';
  record(5, summaryAfterCancel === '12-655-295455', `summary="${summaryAfterCancel}"`);

  // Step 6 — Add Row disabled while popover open
  await page.locator('#item-bank-details').click();
  await page.waitForSelector('.committee-transfer-popover');
  const addDisabled = await page.getByRole('button', { name: 'הוסף שורה' }).isDisabled();
  await page.locator('.committee-transfer-popover').getByRole('button', { name: 'ביטול' }).click();
  await page.waitForSelector('.committee-transfer-popover', { state: 'hidden', timeout: 5000 });
  record(6, addDisabled, `addDisabled=${addDisabled}`);

  // Reset row for regression steps (D8 confirm auto-accepted)
  await page.locator('#item-payment-target').selectOption('');
  await page.locator('#item-payment-method').selectOption('');
  await page.waitForTimeout(300);

  // Step 7 — family + bank_transfer read-only + add succeeds
  await page.locator('#item-assistance-type').selectOption(data.typeId);
  await page.locator('#item-description').fill('M67 regression family');
  await page.locator('#item-payment-target').selectOption('family');
  await page.locator('#item-payment-method').selectOption('bank_transfer');
  await page.waitForTimeout(200);
  const bankField = page.locator('#item-bank-details');
  const readonlyFamily = await bankField.evaluate((el) => el.tagName === 'INPUT' && el.readOnly && el.classList.contains('committee-bank-readonly'));
  await page.locator('#item-amount').fill('100');
  await page.getByRole('button', { name: 'הוסף שורה' }).click();
  await page.waitForTimeout(800);
  const familyErr = await page.locator('#item-form-error').textContent().catch(() => '');
  const familyBlocked = (familyErr ?? '').includes('כרטיס המשפחה');
  record(7, readonlyFamily && !familyBlocked, `readonly=${readonlyFamily} err="${familyErr?.trim() ?? ''}"`);

  // Step 8 — supplier + vouchers not available
  await page.locator('#item-assistance-type').selectOption(data.typeId);
  await page.locator('#item-description').fill('M67 supplier vouchers');
  await page.locator('#item-payment-target').selectOption('supplier');
  if (data.supplierWithBank) {
    await page.locator('#item-payee-name').selectOption(data.supplierWithBank.id);
  }
  const voucherOption = page.locator('#item-payment-method option[value="vouchers"]');
  const vouchersHidden = (await voucherOption.count()) === 0;
  record(8, vouchersHidden, `voucherOptionCount=${await voucherOption.count()}`);

  // Step 9 — family without bank blocks add
  if (data.draftNoBank) {
    await page.getByRole('button', { name: 'סגור' }).click();
    await page.waitForTimeout(400);
    await openDraft(page, data.draftNoBank.decisionCode);
    await page.locator('#item-assistance-type').selectOption(data.typeId);
    await page.locator('#item-description').fill('M67 no bank');
    await page.locator('#item-payment-target').selectOption('family');
    await page.locator('#item-payment-method').selectOption('bank_transfer');
    const missingDisplay = (await page.locator('#item-bank-details').inputValue()).includes('לא הוזן');
    await page.locator('#item-amount').fill('50');
    await page.getByRole('button', { name: 'הוסף שורה' }).click();
    await page.waitForTimeout(600);
    const noBankErr = await page.locator('#item-form-error').textContent().catch(() => '');
    const blocked = (noBankErr ?? '').includes('כרטיס המשפחה');
    record(9, missingDisplay && blocked, `display="${await page.locator('#item-bank-details').inputValue()}" err="${noBankErr?.trim() ?? ''}"`);
  } else {
    record(9, false, 'No draft without family bank in org');
  }
} catch (err) {
  results.push({ step: 'ERROR', result: 'FAIL', notes: err instanceof Error ? err.message : String(err) });
} finally {
  await browser.close();
}

const m67Steps = results.filter((r) => typeof r.step === 'number');
const m672Steps = results.filter((r) => String(r.step).startsWith('67.2'));
const passCount = m67Steps.filter((r) => r.result === 'PASS').length;
const pass672 = m672Steps.filter((r) => r.result === 'PASS').length;
const summary = {
  passCount,
  total: 9,
  pass672,
  total672: m672Steps.length,
  ok: passCount === 9 && pass672 === m672Steps.length && !results.some((r) => r.step === 'ERROR'),
  results,
};
writeFileSync(new URL('./m67-e2e-results.json', import.meta.url), JSON.stringify(summary, null, 2));
console.log(JSON.stringify(summary, null, 2));
process.exit(summary.ok ? 0 : 1);
