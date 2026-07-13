import puppeteer from 'puppeteer-core';

const web = 'http://localhost:3000';
const api = 'http://localhost:8080';
const ts = Date.now();
const userPwd = `HDUser-${ts}!`;
const orgCode = `HD-${ts}`;
const mgrUser = `hd.mgr.${ts}`;
const coordUser = `hd.coord.${ts}`;
const out = process.argv[2] ?? 'docs/screenshots/home-dashboard-phase6-recent-activity.png';

async function curlJson(method, path, body, cookieJar, ifMatch) {
  const headers = { 'Content-Type': 'application/json', ...(cookieJar ? { Cookie: cookieJar } : {}) };
  if (ifMatch) headers['If-Match'] = String(ifMatch);
  const res = await fetch(`${api}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined });
  const setCookie = typeof res.headers.getSetCookie === 'function' ? res.headers.getSetCookie() : [];
  let json = null;
  try { json = JSON.parse(await res.text()); } catch { /* empty */ }
  return { json, setCookie };
}

function mergeCookies(existing, setCookie) {
  const map = new Map();
  for (const part of (existing || '').split(';').map((s) => s.trim()).filter(Boolean)) {
    const [k, ...rest] = part.split('=');
    map.set(k, rest.join('='));
  }
  for (const c of setCookie || []) {
    const [pair] = c.split(';');
    const [k, ...rest] = pair.split('=');
    map.set(k, rest.join('='));
  }
  return [...map.entries()].map(([k, v]) => `${k}=${v}`).join('; ');
}

let cookies = '';
let r = await curlJson('POST', '/api/v1/auth/login', { username: 'superadmin', password: 'ChangeMe123!' });
cookies = mergeCookies(cookies, r.setCookie);
r = await curlJson('POST', '/api/v1/admin/organizations', { name: 'HD Org', code: orgCode }, cookies);
cookies = mergeCookies(cookies, r.setCookie);
const orgId = r.json.organization.id;
const adminUser = `hd.admin.${ts}`;
await curlJson('POST', `/api/v1/admin/organizations/${orgId}/admin`, { username: adminUser, password: `HDAdmin-${ts}!`, fullName: 'HD Admin' }, cookies);
r = await curlJson('POST', '/api/v1/auth/login', { username: adminUser, password: `HDAdmin-${ts}!` });
cookies = mergeCookies(cookies, r.setCookie);
const roles = (await curlJson('GET', '/api/v1/org/roles', null, cookies)).json.roles;
const mgrRole = roles.find((x) => x.factoryPresetKey === 'preset_manager');
const coordRole = roles.find((x) => x.factoryPresetKey === 'preset_coordinator');
await curlJson('POST', '/api/v1/org/users', { username: mgrUser, password: userPwd, fullName: 'Mgr', organizationRoleId: mgrRole.id }, cookies);
await curlJson('POST', '/api/v1/org/users', { username: coordUser, password: userPwd, fullName: 'Coord', organizationRoleId: coordRole.id }, cookies);
r = await curlJson('POST', '/api/v1/auth/login', { username: adminUser, password: `HDAdmin-${ts}!` });
cookies = mergeCookies(cookies, r.setCookie);
const typeCreate = await curlJson('POST', '/api/v1/org/assistance-types', { typeCode: `HD-T-${ts}`, name: 'Food', frequency: 'one_time' }, cookies);
const typeId = typeCreate.json.assistanceType.id;
r = await curlJson('POST', '/api/v1/auth/login', { username: coordUser, password: userPwd });
let coordCookies = mergeCookies('', r.setCookie);
const fam = await curlJson('POST', '/api/v1/org/families', { familyLastName: 'HD Family', bankNumber: '12', branchNumber: '345', accountNumber: '1234567', accountHolderName: 'Holder' }, coordCookies);
coordCookies = mergeCookies(coordCookies, fam.setCookie);
const familyId = fam.json.family.id;
const dec = await curlJson('POST', '/api/v1/org/committee-decisions', { familyId, meetingDate: '2026-07-01' }, coordCookies);
coordCookies = mergeCookies(coordCookies, dec.setCookie);
const decisionId = dec.json.decision.id;
let decVersion = dec.json.decision.version;
const item = await curlJson('POST', `/api/v1/org/committee-decisions/${decisionId}/items`, { assistanceTypeId: typeId, amount: 100, paymentTarget: 'family', paymentMethod: 'check' }, coordCookies, decVersion);
coordCookies = mergeCookies(coordCookies, item.setCookie);
decVersion = item.json.decisionVersion ?? item.json.decision?.version;
await curlJson('POST', `/api/v1/org/committee-decisions/${decisionId}/submit`, {}, coordCookies, decVersion);

const browser = await puppeteer.launch({
  executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  headless: true,
  args: ['--no-sandbox'],
});
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 1100 });
await page.goto(web, { waitUntil: 'domcontentloaded', timeout: 60000 });
await page.waitForSelector('#username', { timeout: 15000 });
await page.type('#username', mgrUser, { delay: 10 });
await page.type('#password', userPwd, { delay: 10 });
await page.click('button[type="submit"]');
await page.waitForFunction(() => document.body.innerText.includes('פעילות אחרונה'), { timeout: 60000 });
await new Promise((resolve) => setTimeout(resolve, 1500));
await page.screenshot({ path: out, fullPage: true });
await browser.close();
console.log(`saved ${out}`);
