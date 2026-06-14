import { useEffect, useState } from 'react';
import { getMe } from './api/auth';
import type { UserDto } from './api/auth';
import { setSessionExpiredHandler } from './api/client';
import { CoordinatorDashboard } from './pages/CoordinatorDashboard';
import { DashboardPage } from './pages/DashboardPage';
import { FinanceDashboard } from './pages/FinanceDashboard';
import { LoginPage } from './pages/LoginPage';
import { ManagerDashboard } from './pages/ManagerDashboard';
import { OrgAdminDashboard } from './pages/OrgAdminDashboard';
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
    return <SuperAdminDashboard user={user} onLogout={() => setUser(null)} />;
  }

  if (user.role === 'OrganizationAdministrator') {
    return <OrgAdminDashboard user={user} onLogout={() => setUser(null)} />;
  }

  if (user.role === 'Coordinator') {
    return <CoordinatorDashboard user={user} onLogout={() => setUser(null)} />;
  }

  if (user.role === 'Finance') {
    return <FinanceDashboard user={user} onLogout={() => setUser(null)} />;
  }

  if (user.role === 'Manager') {
    return <ManagerDashboard user={user} onLogout={() => setUser(null)} />;
  }

  return <DashboardPage user={user} onLogout={() => setUser(null)} />;
}

export default App;
