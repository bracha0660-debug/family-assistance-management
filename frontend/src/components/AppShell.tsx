import { useState, type ReactNode } from 'react';

interface AppShellTab<T extends string> {
  id: T;
  label: string;
}

interface AppShellUser {
  fullName: string;
  organizationName?: string | null;
}

interface AppShellProps<T extends string> {
  brandTitle: string;
  brandLogoSrc?: string;
  brandLogoAlt?: string;
  homeTabId?: T;
  pageTitle: string;
  user: AppShellUser;
  tabs: AppShellTab<T>[];
  activeTab: T;
  onTabChange: (id: T) => void;
  onLogout: () => void;
  onExitOrg?: () => void;
  children: ReactNode;
}

function userInitials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2);
  return parts[0].charAt(0) + parts[1].charAt(0);
}

function HomeIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z" />
    </svg>
  );
}

function OrganizationLogoPlaceholder() {
  return (
    <span className="app-shell-brand-logo-placeholder" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z" />
      </svg>
    </span>
  );
}

function TabIcon({ tabId }: { tabId: string }) {
  switch (tabId) {
    case 'workflow':
      return <HomeIcon />;
    case 'organizations':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z" />
        </svg>
      );
    case 'users':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5s-3 1.34-3 3 1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5C15 14.17 10.33 13 8 13zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z" />
        </svg>
      );
    case 'families':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8h5z" />
        </svg>
      );
    case 'types':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14H7v-2h5v2zm5-4H7v-2h10v2zm0-4H7V7h10v2z" />
        </svg>
      );
    case 'suppliers':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zM6 18.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm13.5-9 1.96 2.5H17V9.5h2.5zm-1.5 9c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z" />
        </svg>
      );
    case 'decisions':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M1 21h12v2H1v-2zM5.24 8.07l2.83-2.83 14.14 14.14-2.83 2.83L5.24 8.07zM12.32 1l5.66 5.66-2.83 2.83-5.66-5.66L12.32 1zM3.83 9.48l5.66 5.66-2.83 2.83L1 12.32l2.83-2.84z" />
        </svg>
      );
    case 'payments':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z" />
        </svg>
      );
    case 'activity':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V10h14v10zm0-12H5V6h14v2z" />
        </svg>
      );
    case 'permissions':
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z" />
        </svg>
      );
    default:
      return (
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z" />
        </svg>
      );
  }
}

export function AppShell<T extends string>({
  brandTitle,
  brandLogoSrc,
  brandLogoAlt = 'לוגו הארגון',
  homeTabId,
  pageTitle,
  user,
  tabs,
  activeTab,
  onTabChange,
  onLogout,
  onExitOrg,
  children,
}: AppShellProps<T>) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  function handleTabChange(id: T) {
    onTabChange(id);
    setSidebarOpen(false);
  }

  function handleBrandClick() {
    if (homeTabId) {
      handleTabChange(homeTabId);
    }
  }

  const navTabs = homeTabId ? tabs.filter((t) => t.id !== homeTabId) : tabs;
  const isHomeActive = homeTabId != null && activeTab === homeTabId;

  const roleLabel = user.organizationName
    ? `${user.fullName} — ${user.organizationName}`
    : user.fullName;

  return (
    <div
      className={`app-shell${sidebarOpen ? ' app-shell-sidebar-open' : ''}${sidebarCollapsed ? ' app-shell-sidebar-collapsed' : ''}`}
    >
      {sidebarOpen && (
        <button
          type="button"
          className="app-shell-backdrop"
          aria-label="סגור תפריט"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      <aside className="app-shell-sidebar">
        <button
          type="button"
          className="app-shell-sidebar-collapse-btn"
          onClick={() => setSidebarCollapsed((collapsed) => !collapsed)}
          aria-label={sidebarCollapsed ? 'הרחב תפריט' : 'כווץ תפריט'}
          title={sidebarCollapsed ? 'הרחב תפריט' : 'כווץ תפריט'}
        >
          <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12l4.58-4.59z" />
          </svg>
        </button>

        <div className={`app-shell-brand${homeTabId ? ' app-shell-brand--logo' : ''}`}>
          {homeTabId ? (
            <button
              type="button"
              className={`app-shell-brand-logo-btn${isHomeActive ? ' app-shell-brand-logo-btn-active' : ''}`}
              onClick={handleBrandClick}
              aria-label="לוח בקרה"
              title="לוח בקרה"
            >
              {brandLogoSrc ? (
                <img
                  src={brandLogoSrc}
                  alt={brandLogoAlt}
                  className="app-shell-brand-logo"
                />
              ) : (
                <OrganizationLogoPlaceholder />
              )}
            </button>
          ) : (
            <>
              <span className="app-shell-brand-dot" aria-hidden="true" />
              {brandTitle}
            </>
          )}
        </div>

        <nav className="app-shell-nav" aria-label="ניווט במערכת">
          {homeTabId && (
            <button
              type="button"
              className={`app-shell-nav-item app-shell-nav-item-home${isHomeActive ? ' app-shell-nav-item-active' : ''}`}
              onClick={() => handleTabChange(homeTabId)}
              title={sidebarCollapsed ? 'מסך הבית' : undefined}
            >
              <span className="app-shell-nav-content">
                <span className={`app-shell-nav-icon${isHomeActive ? ' app-shell-nav-icon-active' : ''}`}>
                  <HomeIcon />
                </span>
                <span className="app-shell-nav-label">מסך הבית</span>
              </span>
            </button>
          )}
          {navTabs.map((t) => {
            const isActive = t.id === activeTab;
            return (
              <button
                key={t.id}
                type="button"
                className={`app-shell-nav-item${isActive ? ' app-shell-nav-item-active' : ''}`}
                onClick={() => handleTabChange(t.id)}
                title={sidebarCollapsed ? t.label : undefined}
              >
                <span className="app-shell-nav-content">
                  <span className={`app-shell-nav-icon${isActive ? ' app-shell-nav-icon-active' : ''}`}>
                    <TabIcon tabId={t.id} />
                  </span>
                  <span className="app-shell-nav-label">{t.label}</span>
                </span>
              </button>
            );
          })}
        </nav>

        <div className="app-shell-sidebar-footer">
          {onExitOrg && (
            <button
              type="button"
              className="app-shell-sidebar-footer-btn"
              onClick={onExitOrg}
              title={sidebarCollapsed ? 'יציאה מארגון' : undefined}
            >
              <span className="app-shell-sidebar-footer-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" fill="currentColor">
                  <path d="M10.09 15.59 11.5 17l5-5-5-5-1.41 1.41L12.67 11H3v2h9.67l-1.58 1.59zM19 3H5c-1.1 0-2 .9-2 2v4h2V5h14v14H5v-4H3v4c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z" />
                </svg>
              </span>
              <span className="app-shell-sidebar-footer-label">יציאה מארגון</span>
            </button>
          )}
          <button
            type="button"
            className="app-shell-sidebar-footer-btn app-shell-sidebar-logout"
            onClick={onLogout}
            title={sidebarCollapsed ? 'יציאה' : undefined}
          >
            <span className="app-shell-sidebar-footer-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5-5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z" />
              </svg>
            </span>
            <span className="app-shell-sidebar-footer-label">יציאה</span>
          </button>
        </div>
      </aside>

      <div className="app-shell-body">
        <header className="app-shell-header">
          <div className="app-shell-header-start">
            <button
              type="button"
              className="app-shell-menu-btn"
              aria-label="פתח תפריט"
              aria-expanded={sidebarOpen}
              onClick={() => setSidebarOpen((open) => !open)}
            >
              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path d="M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z" />
              </svg>
            </button>
            <h1 className="app-shell-page-title">{pageTitle}</h1>
          </div>

          <div className="app-shell-header-actions">
            <div className="app-shell-user">
              <span className="app-shell-avatar" aria-hidden="true">
                {userInitials(user.fullName)}
              </span>
              <span className="app-shell-user-name">{roleLabel}</span>
            </div>
          </div>
        </header>

        <main className="app-shell-main">{children}</main>
      </div>
    </div>
  );
}
