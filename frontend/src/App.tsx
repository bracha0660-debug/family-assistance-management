import { useEffect, useState } from 'react';
import { getMe } from './api/auth';
import type { UserDto } from './api/auth';
import { setSessionExpiredHandler } from './api/client';
import { LoginPage } from './pages/LoginPage';
import { OrgAdminDashboard } from './pages/OrgAdminDashboard';
import { OrgUserDashboard } from './pages/OrgUserDashboard';
import { SuperAdminDashboard } from './pages/SuperAdminDashboard';

function App() {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setSessionExpiredHandler(() => setUser(null));
    return () => setSessionExpiredHandler(null);
  }, []);

  useEffect(() => {
    getMe()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <div className="loading">טוען...</div>;
  }

  if (!user) {
    return <LoginPage onLogin={setUser} />;
  }

  if (user.role === 'SuperAdmin') {
    if (user.actingOrganizationId) {
      return (
        <OrgAdminDashboard
          user={user}
          onLogout={() => setUser(null)}
          onUserUpdated={setUser}
        />
      );
    }
    return (
      <SuperAdminDashboard
        user={user}
        onLogout={() => setUser(null)}
        onUserUpdated={setUser}
      />
    );
  }

  if (user.role === 'OrganizationAdministrator') {
    return (
      <OrgAdminDashboard
        user={user}
        onLogout={() => setUser(null)}
        onUserUpdated={setUser}
      />
    );
  }

  if (user.role === 'OrganizationUser' || user.role === 'Coordinator' || user.role === 'Manager' || user.role === 'Finance') {
    return <OrgUserDashboard user={user} onLogout={() => setUser(null)} onUserUpdated={setUser} />;
  }

  return <div className="loading">תפקיד לא מוכר</div>;
}

export default App;
