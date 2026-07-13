const API = 'http://localhost:8080';

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
console.log('login', login.status, JSON.stringify(login.body, null, 2));
const token = login.body?.sessionToken;
if (!token) process.exit(1);

const orgs = await api('/api/v1/admin/organizations', { token });
console.log('orgs', orgs.status, JSON.stringify(orgs.body, null, 2));

if (orgs.body?.organizations?.length) {
  const orgId = orgs.body.organizations[0].id;
  const enter = await api(`/api/v1/admin/organizations/${orgId}/enter`, { method: 'POST', token });
  console.log('enter', enter.status, JSON.stringify(enter.body?.user?.actingOrganizationId));
  const token2 = enter.body?.sessionToken || token;

  const families = await api('/api/v1/org/families', { token: token2 });
  console.log('families', families.status, families.body?.families?.length, families.body?.families?.slice(0, 2));

  const suppliers = await api('/api/v1/org/suppliers', { token: token2 });
  console.log('suppliers', suppliers.status, suppliers.body?.suppliers?.length);

  const types = await api('/api/v1/org/assistance-types', { token: token2 });
  console.log('types', types.status, types.body?.assistanceTypes?.length);

  const decisions = await api('/api/v1/org/committee-decisions', { token: token2 });
  console.log('decisions', decisions.status, decisions.body?.decisions?.length, decisions.body?.decisions?.slice(0, 2));
}
