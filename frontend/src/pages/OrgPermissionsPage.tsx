import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  getPermissionCatalog,
  getOrgRole,
  listOrgRoles,
  resetRoleGrants,
  translatePermissionCategory,
  updateRoleGrants,
  type OrganizationRoleListItem,
  type PermissionCatalogItem,
  type RoleGrant,
} from '../api/permissions';

interface OrgPermissionsPageProps {
  onPermissionsChanged?: () => void;
}

type GrantDraft = Record<string, string | null>;

function grantsToDraft(grants: RoleGrant[]): GrantDraft {
  const draft: GrantDraft = {};
  for (const g of grants) draft[g.permissionKey] = g.scope;
  return draft;
}

function draftToGrants(catalog: PermissionCatalogItem[], draft: GrantDraft): RoleGrant[] {
  return Object.entries(draft)
    .filter(([, scope]) => scope !== null)
    .map(([permissionKey, scope]) => ({ permissionKey, scope: scope! }))
    .filter((g) => {
      const item = catalog.find((c) => c.permissionKey === g.permissionKey);
      return item?.scopeApplies !== false || g.scope === 'organization';
    });
}

export function OrgPermissionsPage({ onPermissionsChanged }: OrgPermissionsPageProps) {
  const [catalog, setCatalog] = useState<PermissionCatalogItem[]>([]);
  const [roles, setRoles] = useState<OrganizationRoleListItem[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState('');
  const [draft, setDraft] = useState<GrantDraft>({});
  const [baseline, setBaseline] = useState<GrantDraft>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showReason, setShowReason] = useState<'save' | 'reset' | null>(null);
  const [reason, setReason] = useState('');

  const loadRoles = useCallback(async () => {
    setError('');
    try {
      const [catalogData, rolesData] = await Promise.all([
        getPermissionCatalog(),
        listOrgRoles(),
      ]);
      setCatalog(catalogData);
      setRoles(rolesData);
      if (!selectedRoleId && rolesData.length > 0) {
        setSelectedRoleId(rolesData[0].id);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, [selectedRoleId]);

  useEffect(() => {
    loadRoles();
  }, [loadRoles]);

  const loadRoleGrants = useCallback(async (roleId: string) => {
    if (!roleId) return;
    try {
      const role = await getOrgRole(roleId);
      const next = grantsToDraft(role.grants);
      setDraft(next);
      setBaseline(next);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    }
  }, []);

  useEffect(() => {
    if (selectedRoleId) {
      loadRoleGrants(selectedRoleId);
    }
  }, [selectedRoleId, loadRoleGrants]);

  const selectedRole = useMemo(
    () => roles.find((r) => r.id === selectedRoleId),
    [roles, selectedRoleId],
  );

  const groupedCatalog = useMemo(() => {
    const groups = new Map<string, PermissionCatalogItem[]>();
    for (const item of catalog) {
      const list = groups.get(item.category) ?? [];
      list.push(item);
      groups.set(item.category, list);
    }
    return [...groups.entries()].map(([category, items]) => ({
      category,
      label: translatePermissionCategory(category),
      items: items.sort((a, b) => a.sortOrder - b.sortOrder),
    }));
  }, [catalog]);

  function setGrantValue(key: string, scope: string | null) {
    setDraft((prev) => {
      const next = { ...prev };
      if (scope === null) delete next[key];
      else next[key] = scope;
      return next;
    });
  }

  function renderScopeControl(item: PermissionCatalogItem) {
    const current = draft[item.permissionKey] ?? null;

    if (!item.scopeApplies) {
      const checked = current === 'organization';
      return (
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={checked}
            onChange={() => setGrantValue(item.permissionKey, checked ? null : 'organization')}
          />
          <span>{item.displayNameHe}</span>
          <code className="permission-key-hint">{item.permissionKey}</code>
        </label>
      );
    }

    if (!item.supportsMyRecords) {
      const checked = current === 'organization';
      return (
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={checked}
            onChange={() => setGrantValue(item.permissionKey, checked ? null : 'organization')}
          />
          <span>{item.displayNameHe}</span>
          <code className="permission-key-hint">{item.permissionKey}</code>
        </label>
      );
    }

    return (
      <div className="permission-scope-row">
        <span className="permission-scope-label">{item.displayNameHe}</span>
        <code className="permission-key-hint">{item.permissionKey}</code>
        <select
          value={current ?? 'off'}
          onChange={(e) => {
            const v = e.target.value;
            setGrantValue(item.permissionKey, v === 'off' ? null : v);
          }}
        >
          <option value="off">כבוי</option>
          <option value="my_records">הרשומות שלי</option>
          <option value="organization">כל הארגון</option>
        </select>
      </div>
    );
  }

  async function handleSave() {
    if (!showReason || !selectedRoleId) return;
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      const updated = showReason === 'reset'
        ? await resetRoleGrants(selectedRoleId, reason.trim())
        : await updateRoleGrants(selectedRoleId, draftToGrants(catalog, draft), reason.trim());
      const next = grantsToDraft(updated.grants);
      setDraft(next);
      setBaseline(next);
      setSuccess('הרשאות עודכנו בהצלחה');
      setShowReason(null);
      setReason('');
      onPermissionsChanged?.();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setSaving(false);
    }
  }

  const unchanged = JSON.stringify(baseline) === JSON.stringify(draft);

  if (loading) return <p>טוען הרשאות...</p>;

  return (
    <div className="permissions-page">
      <p className="hint-text">
        הגדרת הרשאות לפי תפקיד. שינוי תבנית אינו משנה התאמות אישיות קיימות. הרשאות סופיות = תבנית + התאמות משתמש.
        שינוי הרשאות דורש ציון סיבה ונרשם ביומן הפעילות.
      </p>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      <div className="permissions-role-tabs">
        {roles.map((role) => (
          <button
            key={role.id}
            type="button"
            className={`tab-button ${selectedRoleId === role.id ? 'tab-active' : ''}`}
            onClick={() => setSelectedRoleId(role.id)}
          >
            {role.name}
            {role.status === 'disabled' && ' (מושבת)'}
          </button>
        ))}
      </div>

      {selectedRole?.factoryPresetKey && (
        <p className="hint-text">
          תפקיד מבוסס תבנית ({selectedRole.factoryPresetKey}) — ניתן לערוך או לאפס לברירת מחדל.
        </p>
      )}

      <div className="permissions-groups">
        {groupedCatalog.map((group) => (
          <section key={group.category} className="permissions-group-card">
            <h3>{group.label}</h3>
            <div className="permissions-checkboxes">
              {group.items.map((item) => (
                <div key={item.permissionKey}>{renderScopeControl(item)}</div>
              ))}
            </div>
          </section>
        ))}
      </div>

      <div className="permissions-actions">
        <button
          type="button"
          className="btn-primary"
          disabled={unchanged || saving}
          onClick={() => setShowReason('save')}
        >
          שמור הרשאות
        </button>
        {selectedRole?.factoryPresetKey && (
          <button
            type="button"
            className="btn-small"
            disabled={saving}
            onClick={() => setShowReason('reset')}
          >
            איפוס לברירת מחדל
          </button>
        )}
      </div>

      {showReason && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <h2>{showReason === 'reset' ? 'איפוס לברירת מחדל' : 'שמירת הרשאות'}</h2>
            <p>יש לציין סיבה לשינוי מהותי:</p>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={4}
              maxLength={500}
              placeholder="סיבת השינוי"
            />
            <div className="modal-actions">
              <button type="button" onClick={() => { setShowReason(null); setReason(''); }}>
                ביטול
              </button>
              <button
                type="button"
                className="btn-primary"
                disabled={saving || reason.trim().length < 3}
                onClick={handleSave}
              >
                {saving ? 'שומר...' : 'אישור'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
