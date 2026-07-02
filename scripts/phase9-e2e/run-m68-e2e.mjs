import { chromium } from 'playwright';
import { writeFileSync } from 'fs';

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const API = process.env.E2E_API_URL ?? 'http://localhost:8080';
const ORG_ID = process.env.E2E_ORG_ID ?? 'dbae6c14-bd14-4da1-a731-5d51bb10f4ac';

const results = [];

function record(step, pass, notes = '') {
  results.push({ step, result: pass ? 'PASS' : 'FAIL', notes });
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
  const [decisionsRes, familiesRes, typesRes] = await Promise.all([
    api('/api/v1/org/committee-decisions', { token }),
    api('/api/v1/org/families', { token }),
    api('/api/v1/org/assistance-types', { token }),
  ]);
  const decisions = decisionsRes.json.decisions ?? [];
  const families = familiesRes.json.families ?? [];
  const types = (typesRes.json.assistanceTypes ?? []).filter((t) => t.status === 'active');
  const familyById = Object.fromEntries(families.map((f) => [f.id, f]));
  const familyWithBank = families.find((f) => f.status === 'active' && hasCompleteBank(f));
  return {
    decisions,
    familyById,
    familyWithBank,
    typeId: types[0]?.id,
    submitted: decisions.find((d) => d.status === 'submitted'),
    returned: decisions.find((d) => d.status === 'returned_for_revision'),
    draftWithBank: decisions.find((d) => d.status === 'draft' && hasCompleteBank(familyById[d.familyId])),
  };
}

async function getDecision(token, id) {
  const res = await api(`/api/v1/org/committee-decisions/${id}`, { token });
  return res.json?.decision ?? res.json;
}

async function createDraft(token, familyId, summary) {
  const res = await api('/api/v1/org/committee-decisions', {
    token,
    method: 'POST',
    body: {
      familyId,
      meetingDate: '2026-06-25',
      summary,
    },
  });
  if (res.status >= 400) throw new Error(`create draft failed: ${res.status} ${res.text}`);
  return res.json.decision;
}

async function addItem(token, decision, body) {
  const res = await api(`/api/v1/org/committee-decisions/${decision.id}/items`, {
    token,
    method: 'POST',
    ifMatch: decision.version,
    body,
  });
  if (res.status >= 400) throw new Error(`add item failed: ${res.status} ${res.text}`);
  return res.json;
}

async function submitDecision(token, decision) {
  const res = await api(`/api/v1/org/committee-decisions/${decision.id}/submit`, {
    token,
    method: 'POST',
    ifMatch: decision.version,
  });
  if (res.status >= 400) throw new Error(`submit failed: ${res.status} ${res.text}`);
  return res.json.decision;
}

async function rejectForRevision(token, decision, reason = 'M68 E2E return') {
  const res = await api(`/api/v1/org/committee-decisions/${decision.id}/reject`, {
    token,
    method: 'POST',
    ifMatch: decision.version,
    body: { reason, returnForRevision: true },
  });
  if (res.status >= 400) throw new Error(`reject failed: ${res.status} ${res.text}`);
  return res.json.decision;
}

async function deleteDecision(token, decision) {
  return api(`/api/v1/org/committee-decisions/${decision.id}`, {
    token,
    method: 'DELETE',
    ifMatch: decision.version,
  });
}

async function createBrowser(token) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  await context.addInitScript((t) => sessionStorage.setItem('FAM.Session', t), token);
  const page = await context.newPage();
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

async function openDecision(page, code) {
  await page.locator('table.org-table tbody tr').filter({ hasText: code })
    .locator('button', { hasText: 'פתח' }).click();
  await page.waitForSelector('.modal-card', { timeout: 10000 });
  await page.waitForTimeout(300);
}

async function rowVisible(page, code) {
  return page.locator('table.org-table tbody tr').filter({ hasText: code }).count().then((n) => n > 0);
}

async function fillPayeeName(page, value) {
  const el = page.locator('#item-payee-name');
  await el.click();
  await el.fill('');
  await el.pressSequentially(value, { delay: 15 });
  await el.blur();
  await page.waitForTimeout(100);
}

const token = await loginAndEnterOrg();
const data = await loadOrgData(token);
if (!data.familyWithBank || !data.typeId) {
  throw new Error('Missing family with bank or assistance type');
}

let submitted = data.submitted;
let returned = data.returned;
const cleanupIds = [];

try {
  if (!submitted) {
    let draft = await createDraft(token, data.familyWithBank.id, 'M68 submitted seed');
    cleanupIds.push(draft.id);
    await addItem(token, draft, {
      assistanceTypeId: data.typeId,
      amount: 100,
      paymentTarget: 'family',
      paymentMethod: 'check',
      payeeName: data.familyWithBank.familyLastName,
    });
    draft = await getDecision(token, draft.id);
    submitted = await submitDecision(token, draft);
  }

  if (!returned) {
    let draft = await createDraft(token, data.familyWithBank.id, 'M68 returned seed');
    cleanupIds.push(draft.id);
    await addItem(token, draft, {
      assistanceTypeId: data.typeId,
      amount: 150,
      paymentTarget: 'family',
      paymentMethod: 'check',
      payeeName: data.familyWithBank.familyLastName,
    });
    draft = await getDecision(token, draft.id);
    draft = await submitDecision(token, draft);
    returned = await rejectForRevision(token, draft);
  }
} catch (err) {
  record('SETUP', false, err instanceof Error ? err.message : String(err));
}

const { browser, page } = await createBrowser(token);

try {
  await gotoDecisions(page);

  // M1 — draft delete via UI
  let deleteDraft = await createDraft(token, data.familyWithBank.id, 'M68 delete M1');
  cleanupIds.push(deleteDraft.id);
  await addItem(token, deleteDraft, {
    assistanceTypeId: data.typeId,
    amount: 50,
    paymentTarget: 'family',
    paymentMethod: 'check',
    payeeName: data.familyWithBank.familyLastName,
  });
  deleteDraft = await getDecision(token, deleteDraft.id);
  await page.reload();
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'החלטות ועדה' }).click();
  await page.waitForSelector('table.org-table tbody tr', { timeout: 15000 });

  let confirmText = '';
  page.once('dialog', async (dialog) => {
    confirmText = dialog.message();
    await dialog.accept();
  });
  await openDecision(page, deleteDraft.decisionCode);
  const deleteBtnVisible = await page.getByRole('button', { name: 'מחק החלטה' }).isVisible();
  await page.getByRole('button', { name: 'מחק החלטה' }).click();
  await page.waitForTimeout(1200);
  const modalClosed = !(await page.locator('.modal-card').isVisible().catch(() => false));
  const rowGone = !(await rowVisible(page, deleteDraft.decisionCode));
  const confirmOk = confirmText.includes('למחוק לצמיתות') && confirmText.includes('אינה ניתנת לשחזור');
  record('M1', deleteBtnVisible && modalClosed && rowGone && confirmOk,
    `deleteBtn=${deleteBtnVisible} modalClosed=${modalClosed} rowGone=${rowGone} confirm="${confirmText.replace(/\n/g, ' | ')}"`);

  // M2 — non-draft: no delete, no cancel
  await openDecision(page, submitted.decisionCode);
  const deleteCountSubmitted = await page.getByRole('button', { name: 'מחק החלטה' }).count();
  const cancelCountSubmitted = await page.getByRole('button', { name: 'בטל החלטה' }).count();
  record('M2', deleteCountSubmitted === 0 && cancelCountSubmitted === 0,
    `delete=${deleteCountSubmitted} cancel=${cancelCountSubmitted} status=${submitted.status}`);
  await page.getByRole('button', { name: 'סגור' }).click();
  await page.waitForTimeout(400);

  // M3 — returned_for_revision editable, no delete
  await openDecision(page, returned.decisionCode);
  const deleteCountReturned = await page.getByRole('button', { name: 'מחק החלטה' }).count();
  const meetingEditable = await page.locator('#edit-meeting-date').isEnabled();
  record('M3', deleteCountReturned === 0 && meetingEditable,
    `delete=${deleteCountReturned} meetingEditable=${meetingEditable} status=${returned.status}`);
  await page.getByRole('button', { name: 'סגור' }).click();
  await page.waitForTimeout(400);

  // M4 — hard delete removes decision from list (API verify)
  let m4Draft = await createDraft(token, data.familyWithBank.id, 'M68 delete M4');
  await addItem(token, m4Draft, {
    assistanceTypeId: data.typeId,
    amount: 75,
    paymentTarget: 'family',
    paymentMethod: 'check',
    payeeName: data.familyWithBank.familyLastName,
  });
  m4Draft = await getDecision(token, m4Draft.id);
  await page.reload();
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'החלטות ועדה' }).click();
  await page.waitForSelector('table.org-table tbody tr', { timeout: 15000 });
  const delRes = await deleteDecision(token, m4Draft);
  await page.reload();
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'החלטות ועדה' }).click();
  await page.waitForSelector('table.org-table tbody tr', { timeout: 15000 });
  const getRes = await api(`/api/v1/org/committee-decisions/${m4Draft.id}`, { token });
  const m4RowGone = !(await rowVisible(page, m4Draft.decisionCode));
  record('M4', delRes.status === 204 && getRes.status === 404 && m4RowGone,
    `deleteStatus=${delRes.status} getStatus=${getRes.status} rowGone=${m4RowGone}`);

  // M5 — M66 family bank read-only
  const regressionDraft = data.draftWithBank ?? await createDraft(token, data.familyWithBank.id, 'M68 M5/M6');
  if (!data.draftWithBank) cleanupIds.push(regressionDraft.id);
  await openDecision(page, regressionDraft.decisionCode);
  await page.locator('#item-assistance-type').selectOption(data.typeId);
  await page.locator('#item-payment-target').selectOption('family');
  await page.locator('#item-payment-method').selectOption('bank_transfer');
  await page.waitForTimeout(250);
  const bankField = page.locator('#item-bank-details');
  const readonlyFamily = await bankField.evaluate((el) => el.tagName === 'INPUT' && el.readOnly && el.classList.contains('committee-bank-readonly'));
  const bankLabel = (await page.locator('.committee-item-form__field--bank-details label').textContent())?.trim() ?? '';
  record('M5', readonlyFamily && bankLabel.includes('פרטי בנק'),
    `readonly=${readonlyFamily} label="${bankLabel}"`);

  // M6 — M67 other transfer popover (fresh row to avoid D8 target-change confirm)
  await page.getByRole('button', { name: 'סגור' }).click();
  await page.waitForTimeout(400);
  await openDecision(page, regressionDraft.decisionCode);
  await page.locator('#item-assistance-type').selectOption(data.typeId);
  await page.locator('#item-payment-target').selectOption('other');
  await fillPayeeName(page, 'M68 popover');
  await page.locator('#item-payment-method').selectOption('bank_transfer');
  await page.waitForSelector('.committee-transfer-popover', { timeout: 5000 });
  const popoverVisible = await page.locator('.committee-transfer-popover').isVisible();
  await page.locator('#transfer-branch-number').fill('123');
  await page.locator('#transfer-account-number').fill('456789');
  await page.locator('#transfer-bank-number').click();
  await page.locator('#transfer-bank-number').pressSequentially('12', { delay: 20 });
  await page.waitForTimeout(200);
  await page.locator('.committee-transfer-popover').getByRole('button', { name: 'שמור פרטי העברה' }).click();
  await page.waitForSelector('.committee-transfer-popover', { state: 'hidden', timeout: 5000 });
  const summary = (await page.locator('#item-bank-details').textContent())?.trim() ?? '';
  await page.locator('#item-amount').fill('100');
  await page.getByRole('button', { name: 'הוסף שורה' }).click();
  await page.waitForTimeout(800);
  const rowCount = await page.locator('.committee-items-table tbody tr:not(.empty-row)').count();
  record('M6', popoverVisible && summary.length > 0 && rowCount > 0,
    `popover=${popoverVisible} summary="${summary}" rows=${rowCount}`);
} catch (err) {
  results.push({ step: 'ERROR', result: 'FAIL', notes: err instanceof Error ? err.message : String(err) });
} finally {
  await browser.close();
  for (const id of cleanupIds) {
    const d = await getDecision(token, id).catch(() => null);
    if (d?.status === 'draft') {
      await deleteDecision(token, d).catch(() => {});
    }
  }
}

const mSteps = results.filter((r) => String(r.step).startsWith('M'));
const passCount = mSteps.filter((r) => r.result === 'PASS').length;
const summary = {
  passCount,
  total: 6,
  ok: passCount === 6 && !results.some((r) => r.step === 'ERROR' || r.step === 'SETUP'),
  results,
};
writeFileSync(new URL('./m68-e2e-results.json', import.meta.url), JSON.stringify(summary, null, 2));
console.log(JSON.stringify(summary, null, 2));
process.exit(summary.ok ? 0 : 1);
