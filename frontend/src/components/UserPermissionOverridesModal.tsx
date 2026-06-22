import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  getPermissionCatalog,
  translatePermissionCategory,
  type PermissionCatalogItem,
} from '../api/permissions';
import type { OrgUserDto } from '../api/orgUsers';
import {
  getUserPermissionOverrides,
  translateScope,
  translateSourceTag,
  updateUserPermissionOverrides,
  type EffectiveGrant,
  type UserPermissionOverride,
  type UserPermissionOverrideInput,
  type UserPermissionOverridesResponse,
} from '../api/userPermissionOverrides';
import { ModalShell } from './ModalShell';

interface UserPermissionOverridesModalProps {
  user: OrgUserDto;
  onClose: () => void;
  onSaved?: () => void;
}

type OverrideDraft = Record<string, { effect: 'none' | 'grant' | 'deny'; scope?: string }>;

function overridesToDraft(overrides: UserPermissionOverride[]): OverrideDraft {
  const draft: OverrideDraft = {};
  for (const o of overrides) {
    draft[o.permissionKey] = {
      effect: o.effect,
      scope: o.scope ?? 'organization',
    };
  }
  return draft;
}

function draftToInputs(draft: OverrideDraft): UserPermissionOverrideInput[] {
  return Object.entries(draft)
    .filter(([, v]) => v.effect !== 'none')
    .map(([permissionKey, v]) => ({
      permissionKey,
      effect: v.effect as 'grant' | 'deny',
      ...(v.effect === 'grant' ? { scope: v.scope ?? 'organization' } : {}),
    }));
}

function buildEffectivePreview(
  roleGrants: UserPermissionOverridesResponse['roleGrants'],
  draft: OverrideDraft,
): Map<string, EffectiveGrant> {
  const roleMap = new Map(roleGrants.map((g) => [g.permissionKey, g.scope]));
  const effective = new Map<string, string>(roleMap);

  for (const [key, entry] of Object.entries(draft)) {
    if (entry.effect === 'grant') {
      effective.set(key, entry.scope ?? 'organization');
    } else if (entry.effect === 'deny') {
      effective.delete(key);
    }
  }

  const result = new Map<string, EffectiveGrant>();
  for (const [key, scope] of effective) {
    const roleScope = roleMap.get(key);
    const draftEntry = draft[key];
    let sourceTag: EffectiveGrant['sourceTag'] = 'role';
    if (draftEntry?.effect === 'deny') {
      sourceTag = 'deny';
    } else if (draftEntry?.effect === 'grant') {
      sourceTag = roleScope && roleScope !== draftEntry.scope ? 'grant_override' : 'grant';
    } else if (!roleScope) {
      sourceTag = 'none';
    }
    result.set(key, { permissionKey: key, scope, sourceTag });
  }

  for (const [key, entry] of Object.entries(draft)) {
    if (entry.effect === 'deny' && !result.has(key)) {
      result.set(key, { permissionKey: key, scope: '', sourceTag: 'deny' });
    }
  }

  return result;
}

export function UserPermissionOverridesModal({ user, onClose, onSaved }: UserPermissionOverridesModalProps) {
  const [catalog, setCatalog] = useState<PermissionCatalogItem[]>([]);
  const [roleGrants, setRoleGrants] = useState<UserPermissionOverridesResponse['roleGrants']>([]);
  const [draft, setDraft] = useState<OverrideDraft>({});
  const [baseline, setBaseline] = useState<OverrideDraft>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [showConfirm, setShowConfirm] = useState(false);
  const [onlyOverrides, setOnlyOverrides] = useState(false);

  const load = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const [catalogData, data] = await Promise.all([
        getPermissionCatalog(),
        getUserPermissionOverrides(user.id),
      ]);
      setCatalog(catalogData);
      setRoleGrants(data.roleGrants);
      const next = overridesToDraft(data.overrides);
      setDraft(next);
      setBaseline(next);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setLoading(false);
    }
  }, [user.id]);

  useEffect(() => {
    load();
  }, [load]);

  const roleGrantMap = useMemo(
    () => new Map(roleGrants.map((g) => [g.permissionKey, g.scope])),
    [roleGrants],
  );

  const effectivePreview = useMemo(
    () => buildEffectivePreview(roleGrants, draft),
    [roleGrants, draft],
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

  const unchanged = JSON.stringify(baseline) === JSON.stringify(draft);

  function setOverride(key: string, effect: 'none' | 'grant' | 'deny', scope?: string) {
    setDraft((prev) => {
      const next = { ...prev };
      if (effect === 'none') {
        delete next[key];
      } else {
        next[key] = { effect, scope: scope ?? roleGrantMap.get(key) ?? 'organization' };
      }
      return next;
    });
  }

  function renderPersonalControl(item: PermissionCatalogItem) {
    const current = draft[item.permissionKey];
    const effect = current?.effect ?? 'none';

    const effectSelect = (
      <select
        value={effect}
        onChange={(e) => {
          const v = e.target.value as 'none' | 'grant' | 'deny';
          if (v === 'none') setOverride(item.permissionKey, 'none');
          else if (v === 'deny') setOverride(item.permissionKey, 'deny');
          else setOverride(item.permissionKey, 'grant', current?.scope ?? 'organization');
        }}
        className={`override-effect override-effect-${effect}`}
      >
        <option value="none">ללא</option>
        <option value="grant">הענק</option>
        <option value="deny">שלול</option>
      </select>
    );

    if (effect !== 'grant') {
      return effectSelect;
    }

    if (!item.scopeApplies || !item.supportsMyRecords) {
      return (
        <div className="override-personal-cell">
          {effectSelect}
          <span className="override-scope-label">ארגון</span>
        </div>
      );
    }

    return (
      <div className="override-personal-cell">
        {effectSelect}
        <select
          value={current?.scope ?? 'organization'}
          onChange={(e) => setOverride(item.permissionKey, 'grant', e.target.value)}
        >
          <option value="my_records">הרשומות שלי</option>
          <option value="organization">כל הארגון</option>
        </select>
      </div>
    );
  }

  async function handleSave() {
    setSaving(true);
    setError('');
    try {
      await updateUserPermissionOverrides(user.id, draftToInputs(draft));
      setShowConfirm(false);
      onSaved?.();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאת מערכת');
    } finally {
      setSaving(false);
    }
  }

  function handleResetOverrides() {
    setDraft({});
  }

  const visibleRows = groupedCatalog.flatMap((group) =>
    group.items
      .filter((item) => !onlyOverrides || (draft[item.permissionKey]?.effect ?? 'none') !== 'none')
      .map((item) => ({ ...item, groupLabel: group.label })),
  );

  return (
    <>
      <ModalShell
        title={`הרשאות משתמש — ${user.fullName}`}
        hint={user.organizationRoleName ? `תפקיד: ${user.organizationRoleName}` : undefined}
        extraWide
        loading={loading || saving}
        onClose={onClose}
        formError={error}
        footer={
          <>
            <button type="button" className="btn-primary" disabled={unchanged || saving || loading} onClick={() => setShowConfirm(true)}>
              שמור
            </button>
            <button type="button" className="btn-secondary" disabled={saving} onClick={onClose}>
              ביטול
            </button>
            <button type="button" className="btn-small" disabled={saving || Object.keys(draft).length === 0} onClick={handleResetOverrides}>
              איפוס התאמות
            </button>
          </>
        }
      >
        {loading ? (
          <p>טוען הרשאות...</p>
        ) : (
          <>
            <label className="checkbox-label override-filter">
              <input
                type="checkbox"
                checked={onlyOverrides}
                onChange={(e) => setOnlyOverrides(e.target.checked)}
              />
              <span>הצג רק התאמות</span>
            </label>

            <div className="table-wrap">
              <table className="org-table override-matrix-table">
                <thead>
                  <tr>
                    <th>הרשאה</th>
                    <th>מהתפקיד (ירושה)</th>
                    <th>התאמה אישית</th>
                    <th>אפקטיבי</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRows.length === 0 && (
                    <tr>
                      <td colSpan={4} className="empty-row">אין הרשאות להצגה</td>
                    </tr>
                  )}
                  {visibleRows.map((item) => {
                    const inheritedScope = roleGrantMap.get(item.permissionKey);
                    const effective = effectivePreview.get(item.permissionKey);
                    const personal = draft[item.permissionKey];
                    const redundantDeny = personal?.effect === 'deny' && !inheritedScope;

                    return (
                      <tr key={item.permissionKey}>
                        <td>
                          <span>{item.displayNameHe}</span>
                          <code className="permission-key-hint">{item.permissionKey}</code>
                        </td>
                        <td className="override-inherited-cell">
                          {inheritedScope ? `✓ ${translateScope(inheritedScope)}` : '—'}
                        </td>
                        <td>
                          {renderPersonalControl(item)}
                          {redundantDeny && (
                            <span className="hint-text override-hint">כבר חסר בתפקיד</span>
                          )}
                        </td>
                        <td className="override-effective-cell">
                          {effective && effective.sourceTag !== 'deny' ? (
                            <>
                              <strong>✓ {translateScope(effective.scope)}</strong>
                              {effective.sourceTag !== 'none' && (
                                <span className="override-source-tag">{translateSourceTag(effective.sourceTag)}</span>
                              )}
                            </>
                          ) : personal?.effect === 'deny' ? (
                            <>
                              <strong>—</strong>
                              <span className="override-source-tag">{translateSourceTag('deny')}</span>
                            </>
                          ) : (
                            '—'
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </>
        )}
      </ModalShell>

      {showConfirm && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <h2>שמירת הרשאות</h2>
            <p>האם לשמור את שינויי ההרשאות?</p>
            <div className="modal-actions">
              <button type="button" onClick={() => setShowConfirm(false)} disabled={saving}>
                ביטול
              </button>
              <button type="button" className="btn-primary" disabled={saving} onClick={handleSave}>
                {saving ? 'שומר...' : 'שמירה'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
