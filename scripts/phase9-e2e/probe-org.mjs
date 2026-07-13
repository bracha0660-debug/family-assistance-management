const API = 'http://localhost:8080';
const ORG_ID = 'dbae6c14-bd14-4da1-a731-5d51bb10f4ac';

async function api(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  if (options.token) headers['X-FAM-Session'] = options.token;
  const res = await fetch(`${API}${path}`, { ...options, headers });
  const text = await res.text();
  let body;
  try { body = text ? JSON.parse(text) : null; } catch { body = text; }
  return { status: res.status, body };
}

const login = await api('/api/v1/auth/login', {
  method: 'POST',
  body: JSON.stringify({ username: 'superadmin', password: 'ChangeMe123!' }),
});
const token = login.body.sessionToken;
const enter = await api(`/api/v1/admin/organizations/${ORG_ID}/enter`, { method: 'POST', token });
const token2 = enter.body?.sessionToken || token;

const families = await api('/api/v1/org/families', { token: token2 });
console.log('families', families.body?.families?.length);
const withBank = families.body?.families?.filter(f => f.bankNumber && f.branchNumber && f.accountNumber);
const noBank = families.body?.families?.filter(f => !f.bankNumber && f.status === 'active');
console.log('withBank', withBank?.length, withBank?.[0]?.familyLastName);
console.log('noBank', noBank?.length, noBank?.[0]?.familyLastName);

const suppliers = await api('/api/v1/org/suppliers', { token: token2 });
const supWithBank = suppliers.body?.suppliers?.filter(s => s.bankNumber && s.status === 'active');
const supNoBank = suppliers.body?.suppliers?.filter(s => !s.bankNumber && s.status === 'active');
console.log('suppliers', suppliers.body?.suppliers?.length, 'withBank', supWithBank?.length, 'noBank', supNoBank?.length);

const types = await api('/api/v1/org/assistance-types', { token: token2 });
console.log('types', types.body?.assistanceTypes?.map(t => ({ id: t.id, name: t.name, related: t.relatedSuppliers?.length })));

const decisions = await api('/api/v1/org/committee-decisions', { token: token2 });
console.log('decisions status', decisions.status, 'count', decisions.body?.decisions?.length);
const drafts = decisions.body?.decisions?.filter(d => d.status === 'draft');
console.log('drafts', drafts?.length, drafts?.[0]?.decisionCode);
